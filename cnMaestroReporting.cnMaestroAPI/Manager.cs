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

namespace cnMaestroReporting.cnMaestroAPI
{
    public class Manager
    {
        private Dictionary<string, string> _credentials { get; set;  }
        private FormUrlEncodedContent _tokenCredentials { get; set; }
        private string _tokenEndpoint { get => _apiURL + "/access/token"; }
        private string _apiURL { get; set; }
        private int _apiFetchLimit { get; set;  }
        public Settings settings = new Settings();
        private SemaphoreSlim _taskThrottle { get; set; }
        private TextWriter _outputLog { get; set; }
        private HttpClient _client { get; set; }

        /// <summary>
        /// Constructor that creates a manager with all the basic values provided.
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="clientSecret"></param>
        /// <param name="apiDomain"></param>
        /// <param name="apiFetchLimit"></param>
        /// <param name="threads"></param>
        /// <param name="logger"></param>
        public void SetupManager(string clientID, string clientSecret, string apiDomain, int apiFetchLimit = 100, int threads = 4, TextWriter logger = null)
        {
            if (_credentials == null)
                _credentials = new Dictionary<string, string>();

            _credentials["grant_type"] = "client_credentials";
            _credentials["client_id"] = clientID;
            _credentials["client_secret"] = clientSecret;
            _tokenCredentials = new FormUrlEncodedContent(_credentials);

            if (_taskThrottle == null)
                 _taskThrottle = new SemaphoreSlim(initialCount: threads);

            _outputLog = logger ?? Console.Out;

            _apiURL = $"https://{apiDomain}/api/v1";
            _apiFetchLimit = apiFetchLimit;

            if (_client == null)
            {
                _client = new HttpClient();
                _client.BaseAddress = new Uri(_apiURL);
                _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            }
        }

        /// <summary>
        /// Creating a manager from a valid Settings Object, optional textlogger
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="logger"></param>
        public Manager(Settings _settings, TextWriter logger = null) {
            settings = _settings;
            SetupManager(settings.ApiClientID, settings.ApiClientSecret, settings.ApiDomain, settings.ApiPageLimit, settings.ApiThreads, logger);
        }

        /// <summary>
        /// Passed a clean config section from our creator
        /// </summary>
        /// <param name="configSection"></param>
        public Manager(IConfigurationSection configSection, TextWriter logger = null)
        {
            configSection.Bind(settings);
            SetupManager(settings.ApiClientID, settings.ApiClientSecret, settings.ApiDomain, settings.ApiPageLimit, settings.ApiThreads, logger);
        }

        /// <summary>
        /// Connect to the API and grab our valid bearer token we will be using
        /// </summary>
        public async Task ConnectAsync()
        {
            HttpRequestMessage hRequest = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint);
            hRequest.Content = _tokenCredentials;

            HttpResponseMessage response = await _client.SendAsync(hRequest);

            string responseText = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                Dictionary<string, string> tokenEndpointDecoded = JsonSerializer.Deserialize<Dictionary<string, string>>(responseText);
                _client.DefaultRequestHeaders.Add("Authorization", $"Bearer { tokenEndpointDecoded["access_token"] }");
                return;
            } 
            _outputLog.WriteLine($"Login Error Response: { responseText }");
            response.EnsureSuccessStatusCode();
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
        private async Task<CnApiResponse<T>> CallApiAsync<T>(string endPoint, string filter = null, int limit = 100, int offset = 0) where T : ICnMaestroDataType
        {
            HttpResponseMessage response;
            if (String.IsNullOrEmpty(filter))
            {
                response = await _client.GetAsync(_apiURL + endPoint + $"?limit={limit}&offset={offset}");
                _outputLog.WriteLine($"Fetching: {_apiURL}{endPoint}?limit={limit}&offset={offset}");
            } else {
                response = await _client.GetAsync(_apiURL + endPoint + $"?{filter}&limit={limit}&offset={offset}");
                _outputLog.WriteLine($"Fetching: {_apiURL}{endPoint}?{filter}&limit={limit}&offset={offset}");
            }

            string responseText = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode) 
                return JsonSerializer.Deserialize<CnApiResponse<T>>(responseText);
            
            throw new WebException(responseText);
        }

        /// <summary>
        /// Call the API with auto expansion to get all results on all pages, not just the first page.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="endPoint"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public async Task<IList<T>> GetFullApiResultsAsync<T>(string endPoint, string filter = null) where T : ICnMaestroDataType
        {
            // T is the data type we're expecting (e.g. CnDevice)
            var taskList = new List<Task>();
            var offset = 0;

            ConcurrentBag<T> results = new ConcurrentBag<T>();

            // firstCall is made so we get the count we need to pull
            var firstCall = await CallApiAsync<T>(endPoint, filter, _apiFetchLimit, offset);
            Array.ForEach<T>(firstCall.data, r => results.Add(r));

            // We've got our first page so we now know how many records we have and how many we need total
            var totalToFetch = firstCall.paging.Total;
            var recordsFetching = firstCall.data.Count<T>();

            if (totalToFetch > recordsFetching)
            {
                // We still have more to get let's start looping
                while (recordsFetching <= totalToFetch)
                {
                    await _taskThrottle.WaitAsync();

                    taskList.Add(
                        // This will run in a new thread parallel on threadpool) 
                        Task.Run(async () =>
                        {
                            try
                            {
                                Interlocked.Add(ref offset, _apiFetchLimit);
                                CnApiResponse<T> result = await CallApiAsync<T>(endPoint, filter, _apiFetchLimit, offset);

                                Array.ForEach<T>(result.data, r => results.Add(r));
                            }
                            finally
                            {
                                _taskThrottle.Release();
                            }
                        }));

                    recordsFetching += firstCall.paging.Limit;
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
        public Task<IList<CnTower>> GetNetworksAsync(string filter = null) =>
            GetFullApiResultsAsync<CnTower>("/networks", filter);

        /// <summary>
        /// Returns a a list of tasks that are fetching all of the towers available on the network
        /// </summary>
        /// <param name="network"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public Task<IList<CnTower>> GetTowersAsync() =>
            GetFullApiResultsAsync<CnTower>($"/networks/{settings.Network}/towers");

        /// <summary>
        /// Returns a list of devices based on a filter
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public Task<IList<CnDevice>> GetMultipleDevicesAsync(string filter = null) =>
            GetFullApiResultsAsync<CnDevice>($"/devices", TowerAndFiltersToQueryString(settings.Tower, filter));

        /// <summary>
        /// Return a single device by macaddress
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public async Task<CnDevice> GetSingleDeviceAsync(string macAddress, string filter = null) => 
            (await GetFullApiResultsAsync<CnDevice>($"/devices/{macAddress}", filter))
                .FirstOrDefault<CnDevice>();

        /// <summary>
        /// Return device current last reported statistics
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public async Task<CnStatistics> GetDeviceStatsAsync(string macAddress, string filter = null) =>
            (await GetFullApiResultsAsync<CnStatistics>($"/devices/{macAddress}/statistics", filter))
            .FirstOrDefault<CnStatistics>();

        /// <summary>
        /// Returns a list of devices statistics based on a filter
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public Task<IList<CnStatistics>> GetMultipleDevStatsAsync(string filter = "") => 
            GetFullApiResultsAsync<CnStatistics>($"/devices/statistics", TowerAndFiltersToQueryString(settings.Tower, filter));

        /// <summary>
        /// Return a list of performance from a device between 2 dates, it's returned as an array of days and hours.
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public Task<IList<CnPerformance>> GetDevicePerfAsync(string macAddress, DateTime startTime, DateTime endTime) =>
            GetFullApiResultsAsync<CnPerformance>($"/devices/{macAddress}/performance", 
                $"start_time={startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffK")}&stop_time={endTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffK")}");
#endregion

        private static string TowerAndFiltersToQueryString(string towerFilter, string filter)
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