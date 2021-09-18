using System;
using System.Threading.Tasks;
using Flurl.Http;
using Flurl;

namespace cnMaestroReporting.Prometheus
{
    public static class API {
        private static string promUrl = "https://prometheus.caribserve.net/api/v1/query";
        public static async Task<PromApiResponse> QueryAllDlTotal(string timerange)
        {
            var x = promUrl.SetQueryParam("query", $"increase(ifHCInOctets{{ifIndex=\"1\", job=\"canopy450\"}}[{timerange}])", false);
            return await x.GetAsync().ReceiveJson<PromApiResponse>();
        }

        public static async Task<PromApiResponse> QueryAllUlTotal(string timerange)
        {
            var x = promUrl.SetQueryParam("query", $"increase(ifHCOutOctets{{ifIndex=\"1\", job=\"canopy450\"}}[{timerange}])", false);
            return await x.GetAsync().ReceiveJson<PromApiResponse>();
        }

        public static async Task<PromApiResponse> QueryAllDlMaxThroughput(string timerange)
        {
            var x = promUrl.SetQueryParam("query", $"max_over_time(rate(ifHCInOctets{{job=\"canopy450\", ifIndex=\"1\"}}[2m])[{timerange}:30m])*8", false);
            return await x.GetAsync().ReceiveJson<PromApiResponse>();
        }

        public static async Task<PromApiResponse> QueryAllUlMaxThroughput(string timerange)
        {
            var x = promUrl.SetQueryParam("query", $"max_over_time(rate(ifHCOutOctets{{job=\"canopy450\", ifIndex=\"1\"}}[2m])[{timerange}:30m])*8", false);
            return await x.GetAsync().ReceiveJson<PromApiResponse>();
        }

        public static async Task<PromApiResponse> QueryAllUptime(string timerange)
        {
            var x = promUrl.SetQueryParam("query", $"avg_over_time((sum without()(up{{job =\"canopy450\"}}) or (0 * sum_over_time(up{{job = \"canopy450\"}}[{timerange}])))[{timerange}:30m])", false);
            return await x.GetAsync().ReceiveJson<PromApiResponse>();
        }
        
    }
}
