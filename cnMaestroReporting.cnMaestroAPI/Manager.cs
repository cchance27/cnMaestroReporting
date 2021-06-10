using cnMaestroReporting.cnMaestroAPI.cnDataType;
using cnMaestroReporting.cnMaestroAPI.JsonType;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Extensions.Http;
using Polly.Contrib.WaitAndRetry;
using Polly.Wrap;

namespace cnMaestroReporting.cnMaestroAPI
{
public record cnAuthenticationResult(string access_token, int expires_in, string token_type);

public class Manager
    {
        private string apiURL { get; set; }
        private Settings settings { get; init; } = new Settings();
        private Dictionary<string, string> credentials { get; set;  }
        private FormUrlEncodedContent tokenCredentials { get; set; }
        private SemaphoreSlim taskThrottle { get; set; }
        private TextWriter outputLog { get; set; }
        private HttpClient client { get; set; }

        /// <summary>
        /// Constructor that creates a manager with all the basic values provided.
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="clientSecret"></param>
        /// <param name="apiDomain"></param>
        /// <param name="apiFetchLimit"></param>
        /// <param name="threads"></param>
        /// <param name="logger"></param>
        public Manager(string clientID, string clientSecret, string apiDomain, int apiFetchLimit = 100, int threads = 4, TextWriter? logger = null)
        {
            if (credentials == null)
                credentials = new();

            credentials["grant_type"] = "client_credentials";
            credentials["client_id"] = clientID;
            credentials["client_secret"] = clientSecret;

#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
            tokenCredentials = new(credentials);
#pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.

            if (taskThrottle == null)
                 taskThrottle = new(initialCount: threads);

            outputLog = logger ?? Console.Out;

            apiURL = $"https://{apiDomain}/api/v1/";

            if (client == null)
            {
                // Enable Gzip/Deflate support
                var handler = new HttpClientHandler();
                if (handler.SupportsAutomaticDecompression)
                {
                    handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                }

                client = new HttpClient(handler)
                {
                    BaseAddress = new Uri(apiURL)
                };
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            }
        }

        /// <summary>
        /// Creating a manager from a valid Settings Object, optional textlogger
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="logger"></param>

        public Manager(Settings _settings, TextWriter? logger = null) : this(_settings.ApiClientID, _settings.ApiClientSecret, _settings.ApiDomain, _settings.ApiPageLimit, _settings.ApiThreads, logger) {

            settings = _settings;
        }

        /// <summary>
    /// Connect to the API and grab our valid bearer token we will be using
    /// </summary>
        public async Task GetAccessToken()
        {
            client.DefaultRequestHeaders.Authorization = null;

            HttpRequestMessage request = new(HttpMethod.Post, "access/token")
            {
                Content = tokenCredentials
            };

            HttpResponseMessage response = await client.SendAsync(request);

            string responseText = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {

                cnAuthenticationResult? authenticationResult = JsonSerializer.Deserialize<cnAuthenticationResult>(responseText);
                client.DefaultRequestHeaders.Authorization = new("Bearer", authenticationResult?.access_token);
                return;
            } 

            outputLog.WriteLine($"Login Error Response: { responseText }");
        }

        /// <summary>
        /// This calls our API and we can provide limits, offsets, and filters as optional values.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="endPoint"></param>
        /// <param name="filter"></param>
        /// <param name="limit"></param>
        /// <param name="offset"></param>
        /// <returns></returns>

        private async Task<CnApiResponse<T>?> CallApiAsync<T>(string endPoint, string filter = "", int offset = 0) where T : ICnMaestroDataType

        {
            String url = $"{endPoint}?limit=100&offset={offset}";
            url = String.IsNullOrWhiteSpace(filter) ? url : url + "&" + filter; // If we have a filter add it to the url query
            outputLog.WriteLine($"Fetching: {url}");

            AsyncPolicyWrap<HttpResponseMessage> Policies = PoliciesWithTransientTimeoutAndLogin();

            HttpResponseMessage response = await Policies.ExecuteAsync(async () => await client.GetAsync(url));
            var responseString = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();

            // Add support for decimals to the JSON Parser
            var serializeOptions = new JsonSerializerOptions();
            serializeOptions.Converters.Add(new JsonConverters.DecimalConverter());
            if (responseString is not null)
                return JsonSerializer.Deserialize<CnApiResponse<T>>(responseString, serializeOptions);

            return null;
        }

        /// <summary>
        /// Generate a PolicyWrap that includes Transient error protection, auto relogin, and a overall timeout.
        /// </summary>
        /// <returns></returns>
        private AsyncPolicyWrap<HttpResponseMessage> PoliciesWithTransientTimeoutAndLogin()
        {
            // Setup HTTP Policies
            var retryDelay = Backoff.LinearBackoff(TimeSpan.FromMilliseconds(500), retryCount: 3, fastFirst: true);
            var transientPolicy = HttpPolicyExtensions.HandleTransientHttpError()
                .WaitAndRetryAsync(retryDelay,
                onRetryAsync: (ex, ctx, t1) =>
                {
                    outputLog.WriteLine($"Transient Error Occurred: {ex.Result}");
                    return Task.CompletedTask;
                });

            var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(10,
                onTimeoutAsync: (ctx, tsp, t1, t2) =>
                {
                    outputLog.WriteLine("Timeout has occurred");
                    return Task.CompletedTask;
                });

            var reloginPolicy = Policy.HandleResult<HttpResponseMessage>(r => r.StatusCode == HttpStatusCode.Forbidden || r.StatusCode == HttpStatusCode.BadRequest)
                .WaitAndRetryAsync(retryDelay,
                onRetryAsync: async (ex, retry) =>
                {
                    outputLog.WriteLine("Attempting Login...");
                    await GetAccessToken();
                });

            var HttpPolicies = Policy.WrapAsync(timeoutPolicy, transientPolicy, reloginPolicy);
            return HttpPolicies;
        }

        /// <summary>
        /// Call the API with auto expansion to get all results on all pages, not just the first page.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="endPoint"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public async Task<IList<T>> GetFullApiResultsAsync<T>(string endPoint, string filter = "") where T : ICnMaestroDataType
        {
            // T is the data type we're expecting (e.g. CnDevice)
            var taskList = new List<Task>();
            var offset = 0;

            ConcurrentBag<T> results = new();

            // firstCall is made so we get the count we need to pull
            var firstCall = await CallApiAsync<T>(endPoint, filter, offset);
            if (firstCall is null)
                throw new Exception("cnMaestro Returned no response");

            Array.ForEach<T>(firstCall.data, r => results.Add(r));

            // We've got our first page so we now know how many records we have and how many we need total
            var totalToFetch = firstCall.paging.total;
            var recordsFetching = firstCall.data.Length;

            if (totalToFetch > recordsFetching)
            {
                // We still have more to get let's start looping
                while (recordsFetching <= totalToFetch)
                {
                    await taskThrottle.WaitAsync();

                    taskList.Add(
                        // This will run in a new thread parallel on threadpool) 
                        Task.Run(async () =>
                        {
                            try
                            {
                                Interlocked.Add(ref offset, 100);

                                CnApiResponse<T>? result = await CallApiAsync<T>(endPoint, filter, offset);

                                if (result is not null)
                                    Array.ForEach<T>(result.data, r => results.Add(r));
                            }
                            finally
                            {
                                taskThrottle.Release();
                            }
                        }));

                    recordsFetching += firstCall.paging.limit;
                }
            }

            Task.WaitAll(taskList.ToArray());

            return results.ToList<T>();
        }

        #region ------ API Calls --------
        /// <summary>
        /// Returns a list of tasks that are fetching all of the networks
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public Task<IList<CnTower>> GetNetworksAsync(string filter = "") =>
            GetFullApiResultsAsync<CnTower>("networks", filter);

        /// <summary>
        /// Returns a a list of tasks that are fetching all of the towers available on the network
        /// </summary>
        /// <param name="network"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public Task<IList<CnTower>> GetTowersAsync() =>
            GetFullApiResultsAsync<CnTower>($"networks/{settings.Network}/towers");

        /// <summary>
        /// Returns a list of devices based on a filter
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public Task<IList<CnDevice>> GetMultipleDevicesAsync(string filter = "") =>
            GetFullApiResultsAsync<CnDevice>($"devices", TowerAndFiltersToQueryString(settings.Tower, filter));

        /// <summary>
        /// Return a single device by macaddress
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public async Task<CnDevice?> GetSingleDeviceAsync(string macAddress, string filter = "") => 
            (await GetFullApiResultsAsync<CnDevice>($"devices/{macAddress}", filter))
                .FirstOrDefault<CnDevice>();

        /// <summary>
        /// Return device current last reported statistics
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public async Task<CnStatistics?> GetDeviceStatsAsync(string macAddress, string filter = "") =>
            (await GetFullApiResultsAsync<CnStatistics>($"devices/{macAddress}/statistics", filter))
            .FirstOrDefault<CnStatistics>();

        /// <summary>
        /// Returns a list of devices statistics based on a filter
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public Task<IList<CnStatistics>> GetMultipleDevStatsAsync(string filter = "") => 
            GetFullApiResultsAsync<CnStatistics>($"devices/statistics", TowerAndFiltersToQueryString(settings.Tower, filter));

        /// <summary>
        /// Return a list of performance from a device between 2 dates, it's returned as an array of days and hours.
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public Task<IList<CnPerformance>> GetDevicePerfAsync(string macAddress, DateTime startTime, DateTime endTime) =>
            GetFullApiResultsAsync<CnPerformance>($"devices/{macAddress}/performance", 
                $"start_time={startTime:yyyy-MM-ddTHH:mm:ss.fffffffK}&stop_time={endTime:yyyy-MM-ddTHH:mm:ss.fffffffK}");
#endregion

        //TODO: Cleanup this and make it so we can pass a dictionary of filters instead of a string of filters.
        private static string TowerAndFiltersToQueryString(string towerFilter = "", string filter = "")
        {
            // TODO: Fix the hacky stuff and make it so that we can pass dictionary of filters instead of strings.
            // Hacky way of doing this but should work. Grabbing the tower to filter by from config and combining with any passed filters.
            if (String.IsNullOrWhiteSpace(filter) == false)
            {
                // We have a custom filter
                if (String.IsNullOrWhiteSpace(towerFilter) == false)
                {
                    // We also have a towerFilter from config
                    filter = "tower=" + Uri.EscapeDataString(towerFilter) + "&" + filter;
                }
            }
            else
            {
                // We don't have a custom filter
                if (String.IsNullOrWhiteSpace(towerFilter) == false)
                {
                    // We also have a towerFilter from config
                    filter = "tower=" + Uri.EscapeDataString(towerFilter);
                }
            }

            return filter;
        }

        public static Settings GenerateConfig(IConfigurationSection section)
        {
            Settings genSettings = new();
            section.Bind(genSettings);
            return genSettings;
        }
    }
}