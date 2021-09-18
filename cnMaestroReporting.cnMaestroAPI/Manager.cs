using cnMaestroReporting.cnMaestroAPI.cnDataType;
using cnMaestroReporting.cnMaestroAPI.JsonType;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Flurl;
using Flurl.Http;
using cnMaestroReporting.cnMaestroAPI.cnDataTypes;
using static cnMaestroReporting.cnMaestroAPI.FlurlExceptionChecks;

namespace cnMaestroReporting.cnMaestroAPI
{
public class Manager
    {
        private string apiURL { get; set; }
        private string apiBearer { get; set; }
        private Settings settings { get; set; }
        private SemaphoreSlim taskThrottle { get; set; }
        private TextWriter outputLog { get; set; }
        private FlurlClient flurlClient { get; set; }
        private IEnumerable<TimeSpan> retryDelay { get; set; }

        #region Constructors
        /// <summary>
        /// Helper function to setup our manager settings based on settings config.
        /// </summary>
        /// <param name="logger"></param>
        private void SetupManager(TextWriter? logger = null)
        {
            // Semaphore Limit for throttling our API Calls
            taskThrottle = new(initialCount: settings.ApiThreads);

            // Basic logging to console if we aren't passed one
            outputLog = logger ?? Console.Out;

            // Setup our FlurlClient for accessing the API
            apiURL = $"https://{settings.ApiDomain}/api/v1/";
            flurlClient = new FlurlClient(apiURL);

            // Setup our Polly Delay settings
            retryDelay = Backoff.LinearBackoff(TimeSpan.FromMilliseconds(500), retryCount: 3, fastFirst: true);

            apiBearer = "";
        }

        /// <summary>
        /// Clean settings object before constructor
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="logger"></param>
        public Manager(Settings _settings, TextWriter? logger = null) {

            settings = _settings;
            SetupManager(logger);
        }

        /// <summary>
        /// Config Section loading to settings before constructor
        /// </summary>
        /// <param name="section"></param>
        /// <param name="logger"></param>
        public Manager(IConfigurationSection section, TextWriter? logger = null)
        {
            settings = new();
            section.Bind(settings);
            SetupManager(logger);
            
        }
#endregion

        /// <summary>
        /// Connect to the API and grab our valid bearer token we will be using
        /// </summary>
        private async Task RefreshAccessToken()
        {
            outputLog.WriteLine($"Attemption To Login (Client-ID: {settings.ApiClientID})...");

            var transientPolicy = Policy
                .Handle<FlurlHttpException>(HandleTransient)
                .WaitAndRetryAsync(retryDelay, (i, t) => { Console.WriteLine($"Retrying Authentication, Transient Error... {i.Message}"); });
            
            var authResults = await transientPolicy.ExecuteAsync(
                async () => await apiURL
                .AppendPathSegment("access/token").WithClient(flurlClient)
                .PostUrlEncodedAsync(new { client_id = settings.ApiClientID, client_secret = settings.ApiClientSecret, grant_type = "client_credentials" })
                .ReceiveJson<cnAuthentication>());

            apiBearer = authResults.access_token ?? "";
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
            if (apiBearer == "")
                await RefreshAccessToken();

            var transientRecovery = Policy.Handle<FlurlHttpException>(HandleTransient)
                .WaitAndRetryAsync(retryDelay, onRetry: (ex, _, _) => outputLog.WriteLine("Retrying API Call, Transient Error..."));

            var authRecovery = Policy.Handle<FlurlHttpException>(HandleAuthFailure)
                .WaitAndRetryAsync(retryDelay, onRetryAsync: async (ex, _, _) =>
                {
                    outputLog.WriteLine($"Retrying because of Authentication Failure/Timeout: {ex.Message}");
                    await RefreshAccessToken();
                });

            var policies = Policy.WrapAsync(authRecovery, transientRecovery);

            var cnResponse = await policies.ExecuteAsync(async () =>
            {
                var request = filter != "" ? apiURL.SetQueryParams(filter) : (Url)apiURL;
                request.AppendPathSegment(endPoint).WithClient(flurlClient);
                request.SetQueryParam("limit", settings.ApiPageLimit).SetQueryParam("offset", offset);
                
                Console.WriteLine($"Using Bearer: {apiBearer}");
                Console.WriteLine($"Fetching: {request}");

                return await request.WithOAuthBearerToken(apiBearer).GetAsync().ReceiveJson<CnApiResponse<T>>();
            });
            
            return cnResponse;
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

            // Threadsafe Bag for storing results
            ConcurrentBag<T> results = new();

            // firstCall is made so we get the count we need to pull
            var firstCall = await CallApiAsync<T>(endPoint, filter, offset);
            if (firstCall is null)
                throw new Exception("cnMaestro Returned no response");

            // Parse our responses into an array of results.
            Array.ForEach<T>(firstCall.data, r => results.Add(r));

            // We've got our first page so we now know how many records we have and how many we need total
            var totalToFetch = firstCall.paging.total;
            var recordsFetching = firstCall.data.Length;

            if (totalToFetch <= recordsFetching)
            {
                // First page had all of our results, we're done return.
                return results.ToList();
            }

            // We still have more to get let's start looping
            while (recordsFetching <= totalToFetch)
            {
                // Wait for a free thread
                await taskThrottle.WaitAsync();

                taskList.Add(
                    // This will run in a new thread parallel on threadpool) 
                    Task.Run(async () =>
                    {
                        // Safely add our page limit to the offset so all Tasks are calling their correct offset.
                        Interlocked.Add(ref offset, settings.ApiPageLimit);

                        // Request our next page based on the updated offset
                        CnApiResponse<T>? apiResponse = await CallApiAsync<T>(endPoint, filter, offset);

                        // If we got a response add it to our results ConcurrencyBag
                        if (apiResponse is not null)
                            Array.ForEach<T>(apiResponse.data, r => results.Add(r));

                        // Release this task because we're done.
                        taskThrottle.Release();
                    }));

                recordsFetching += firstCall.paging.limit;
            }

            Task.WaitAll(taskList.ToArray());

            return results.ToList<T>();
        }

        #region API Calls
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

        /// <summary>
        /// Return a list of alarms from a device between 2 dates, it's returned as an array of days and hours.
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public Task<IList<CnAlarm>> GetDeviceAlarmsHistoryAsync(string macAddress, DateTime startTime, DateTime endTime, CnSeverity severity = CnSeverity.none)
        {
            var sev = severity != CnSeverity.none ? $"&severity={severity.ToString()}" : "";
            return GetFullApiResultsAsync<CnAlarm>($"devices/{macAddress}/alarms/history",
                $"start_time={startTime:yyyy-MM-ddTHH:mm:ss.fffffffK}&stop_time={endTime:yyyy-MM-ddTHH:mm:ss.fffffffK}");
        }

        public async Task<string> DeleteDeviceAsync(string mac)
        {
            // Super lazy mac check
            if (mac.Length == 17 && mac.Contains(":")) { 
                return await apiURL.AppendPathSegment($"/devices/{mac}").WithClient(flurlClient).WithOAuthBearerToken(apiBearer).DeleteAsync().ReceiveString();
            }
            throw new ArgumentException("Deleting requires a valid  mac address");
        }
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
    }
}