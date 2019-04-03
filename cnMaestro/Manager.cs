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
        private Dictionary<string, string> credentials { get; }
        private FormUrlEncodedContent tokenCredentials { get; }
        private string tokenEndpoint { get => apiURL + "/access/token"; }
        private string apiURL { get; }
        private int apiFetchLimit { get; } = 100;

        private TextWriter outputLog { get; }
        private HttpClient client { get; }

        public Manager(string clientID, string clientSecret, string apiDomain, int apiFetchLimit = 100, TextWriter logger = null)
        {
            if (credentials == null)
                credentials = new Dictionary<string, string>();

            credentials["grant_type"] = "client_credentials";
            credentials["client_id"] = clientID;
            credentials["client_secret"] = clientSecret;

            tokenCredentials = new FormUrlEncodedContent(credentials);

            outputLog = logger ?? Console.Out;

            apiURL = $"https://{apiDomain}/api/v1";
            this.apiFetchLimit = apiFetchLimit;

            if (client == null)
            {
                client = new HttpClient();
                client.BaseAddress = new Uri(apiURL);
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            }
        }

        public async Task ConnectAsync()
        {
            HttpRequestMessage hRequest = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            hRequest.Content = tokenCredentials;

            HttpResponseMessage response = await client.SendAsync(hRequest);

            string responseText = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                Dictionary<string, string> tokenEndpointDecoded = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseText);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer { tokenEndpointDecoded["access_token"] }");
            } else
            {
                outputLog.WriteLine($"Login Error Response: { responseText }");
                response.EnsureSuccessStatusCode();
            }
        }

        // Standard non filtered API Call
        private async Task<CnApiResponse<T>> CallApiAsync<T>(string endPoint, int limit = 100, int offset = 0) where T : ICnMaestroDataType
        {
            HttpResponseMessage response = await client.GetAsync(apiURL + endPoint + $"?limit={limit}&offset={offset}");
            outputLog.WriteLine($"Fetching: {apiURL}{endPoint}?limit={limit}&offset={offset}");
            
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
            HttpResponseMessage response = await client.GetAsync(apiURL + endPoint + $"?{filter}&limit={limit}&offset={offset}");
            outputLog.WriteLine($"Fetching: {apiURL}{endPoint}?{filter},limit={limit}&offset={offset}");

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

        public async Task<IEnumerable<T>> GetFullApiResultsAsync<T>(string endPoint, int simultaneousThreads = 5) where T : ICnMaestroDataType
        {
            //T is the data type we're expecting (e.g. CnDevice)
            var taskList = new List<Task>();
            var taskThrottle = new SemaphoreSlim(initialCount: simultaneousThreads);
            var offset = 0;

            ConcurrentBag<T> results = new ConcurrentBag<T>();

            // firstCall is made so we get the count we need to pull
            var firstCall = await CallApiAsync<T>(endPoint, apiFetchLimit, offset);
            Array.ForEach<T>(firstCall.data, r => results.Add(r));

            var totalToFetch = firstCall.paging.Total;
            
            //We've got our first page so we should only be calling again if we need more.
            var recordsFetching = firstCall.data.Count<T>();

            while (recordsFetching <= totalToFetch)
            {
                await taskThrottle.WaitAsync();

                taskList.Add(
                    // This will run in a new thread parallel on threadpool)
                    Task.Run(async () =>
                    {
                        try
                        {
                            Interlocked.Add(ref offset, apiFetchLimit);
                            var result = await CallApiAsync<T>(endPoint, apiFetchLimit, offset);

                            Array.ForEach<T>(result.data, r => results.Add(r));
                        }
                        finally
                        {
                            taskThrottle.Release();
                        }
                    }));

                recordsFetching += firstCall.paging.Limit;
            }

            Task.WaitAll(taskList.ToArray());

            return results.ToList<T>();
        }
    }
}