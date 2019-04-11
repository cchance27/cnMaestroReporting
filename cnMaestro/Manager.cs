using cnMaestro.JsonType;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace cnMaestro
{
    public class Manager
    {
        private Dictionary<string, string> _credentials { get; }
        private FormUrlEncodedContent _tokenCredentials { get; }
        private string _tokenEndpoint { get => _apiURL + "/access/token"; }
        private string _apiURL { get; }
        private int _apiFetchLimit { get; }

        private SemaphoreSlim _taskThrottle { get; }
        private TextWriter _outputLog { get; }
        private HttpClient _client { get; }

        public Manager(string clientID, string clientSecret, string apiDomain, int apiFetchLimit = 100, int threads = 4, TextWriter logger = null)
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

        public async Task ConnectAsync()
        {
            HttpRequestMessage hRequest = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint);
            hRequest.Content = _tokenCredentials;

            HttpResponseMessage response = await _client.SendAsync(hRequest);

            string responseText = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                Dictionary<string, string> tokenEndpointDecoded = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseText);
                _client.DefaultRequestHeaders.Add("Authorization", $"Bearer { tokenEndpointDecoded["access_token"] }");
            } else
            {
                _outputLog.WriteLine($"Login Error Response: { responseText }");
                response.EnsureSuccessStatusCode();
            }
        }

        // Standard non filtered API Call
        private async Task<CnApiResponse<T>> CallApiAsync<T>(string endPoint, int limit = 100, int offset = 0) where T : ICnMaestroDataType
        {
            HttpResponseMessage response = await _client.GetAsync(_apiURL + endPoint + $"?limit={limit}&offset={offset}");
            _outputLog.WriteLine($"Fetching: {_apiURL}{endPoint}?limit={limit}&offset={offset}");
            
            string responseText = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<CnApiResponse<T>>(responseText);
            }
            else
            {
                throw new WebException(responseText);
            }
        }

        // Allow us to pass a filter "fields=id,name" and limit/offset
        private async Task<CnApiResponse<T>> CallApiAsync<T>(string endPoint, string filter, int limit = 100, int offset = 0) where T : ICnMaestroDataType
        {
            HttpResponseMessage response = await _client.GetAsync(_apiURL + endPoint + $"?filter={filter}&limit={limit}&offset={offset}");
            _outputLog.WriteLine($"Fetching: {_apiURL}{endPoint}?{filter}&limit={limit}&offset={offset}");

            string responseText = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<CnApiResponse<T>>(responseText);
            }
            else
            {
                throw new WebException(responseText);
            }
        }

        public async Task<IEnumerable<T>> GetFullApiResultsAsync<T>(string endPoint, string filter = null) where T : ICnMaestroDataType
        {
            //T is the data type we're expecting (e.g. CnDevice)
            var taskList = new List<Task>();
            var offset = 0;

            ConcurrentBag<T> results = new ConcurrentBag<T>();

            // firstCall is made so we get the count we need to pull
            var firstCall = await CallApiAsync<T>(endPoint, _apiFetchLimit, offset);
            Array.ForEach<T>(firstCall.data, r => results.Add(r));

            var totalToFetch = firstCall.paging.Total;
            
            //We've got our first page so we should only be calling again if we need more.
            var recordsFetching = firstCall.data.Count<T>();

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
                            CnApiResponse<T> result;
                            if (String.IsNullOrWhiteSpace(filter))
                            {
                                result = await CallApiAsync<T>(endPoint, _apiFetchLimit, offset);
                            }
                            else
                            {
                                result = await CallApiAsync<T>(endPoint, filter, _apiFetchLimit, offset);
                            }

                            Array.ForEach<T>(result.data, r => results.Add(r));
                        }
                        finally
                        {
                            _taskThrottle.Release();
                        }
                    }));

                recordsFetching += firstCall.paging.Limit;
            }

            Task.WaitAll(taskList.ToArray());

            return results.ToList<T>();
        }
    }
}