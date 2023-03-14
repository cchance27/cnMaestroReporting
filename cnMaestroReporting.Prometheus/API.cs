using System;
using System.Threading.Tasks;
using Flurl.Http;
using Flurl;
using static Utils.DateTimeExtensions;

namespace cnMaestroReporting.Prometheus
{
    public static class API
    {
        // VERY INTENSIVE SUBQUERIES BUT SINCE THIS IS REPORTING IT DOESN'T RUN OFTEN
        private static string promQueryUrl = "https://prometheus.caribserve.net/api/v1/query";
        public static async Task<PromApiResponse> QueryAllDlTotal(string timerange, DateTime? endTime = null)
        {
            var x = promQueryUrl.SetQueryParam("query", $"increase(ifHCInOctets{{ifIndex=\"1\", job=\"canopy450\"}}[{timerange}])", false);

            if (endTime.HasValue)
                x = x.SetQueryParam("time", endTime.Value.ToRFC3339());

            return await x.GetAsync().ReceiveJson<PromApiResponse>();
        }

        //public static async Task<PromApiResponse> QueryAllAvgDlSMModulation(string timerange, DateTime? endTime = null)
        //{
        //    //FlurlHttp.Configure(x => x.AfterCall = (obj) => Console.WriteLine(obj.Request.Url));
        //
        //    var x = promQueryUrl.SetQueryParam("query", $"avg_over_time(canopyap_dl_mod_count{{job =\"canopyprom\"}}[{timerange}])", false);
        //
        //    if (endTime.HasValue)
        //        x = x.SetQueryParam("time", endTime.Value.ToRFC3339());
        //
        //    return await x.GetAsync().ReceiveJson<PromApiResponse>();
        //}

        public static async Task<PromApiResponse> QueryAllDlSMModulation(string timerange, DateTime? endTime = null)
        {
            //FlurlHttp.Configure(x => x.AfterCall = (obj) => Console.WriteLine(obj.Request.Url));

            var x = promQueryUrl.SetQueryParam("query", $"avg_over_time(canopyap_dl_mod_count{{job =\"canopyprom\"}}[{timerange}])", false);

            if (endTime.HasValue)
                x = x.SetQueryParam("time", endTime.Value.ToRFC3339());

            return await x.GetAsync().ReceiveJson<PromApiResponse>();
        }

        public static async Task<PromApiResponse> QueryAllMaxSMCount(string timerange, DateTime? endTime = null)
        {
            var x = promQueryUrl.SetQueryParam("query", $"max_over_time(regCount{{job=\"canopy450\"}}[{timerange}])", false);
            
            if (endTime.HasValue)
                x = x.SetQueryParam("time", endTime.Value.ToRFC3339(), true);

            return await x.GetAsync().ReceiveJson<PromApiResponse>();
        }

        public static async Task<PromApiResponse> QueryAllUlTotal(string timerange, DateTime? endTime = null)
        {
            var x = promQueryUrl.SetQueryParam("query", $"increase(ifHCOutOctets{{ifIndex=\"1\", job=\"canopy450\"}}[{timerange}])", false);

            if (endTime.HasValue)
                x = x.SetQueryParam("time", endTime.Value.ToRFC3339());

            return await x.GetAsync().ReceiveJson<PromApiResponse>();
        }

        public static async Task<PromApiResponse> QueryAllDlMaxThroughput(string timerange, DateTime? endTime = null)
        {
            var x = promQueryUrl.SetQueryParam("query", $"max_over_time(rate(ifHCInOctets{{job=\"canopy450\", ifIndex=\"1\"}}[2m])[{timerange}:30m])*8", false);

            if (endTime.HasValue)
                x = x.SetQueryParam("time", endTime.Value.ToRFC3339());

            return await x.GetAsync().ReceiveJson<PromApiResponse>();
        }

        public static async Task<PromApiResponse> QueryAllUlMaxThroughput(string timerange, DateTime? endTime = null)
        {
            var x = promQueryUrl.SetQueryParam("query", $"max_over_time(rate(ifHCOutOctets{{job=\"canopy450\", ifIndex=\"1\"}}[2m])[{timerange}:30m])*8", false);

            if (endTime.HasValue)
                x = x.SetQueryParam("time", endTime.Value.ToRFC3339());

            return await x.GetAsync().ReceiveJson<PromApiResponse>();
        }

        public static async Task<PromApiResponse> QueryAllUptime(string timerange, DateTime? endTime = null)
        {
            var x = promQueryUrl.SetQueryParam("query", $"avg_over_time((sum without()(up{{job =\"canopy450\"}}) or (0 * sum_over_time(up{{job = \"canopy450\"}}[{timerange}])))[{timerange}:30m])", false);

            if (endTime.HasValue)
                x = x.SetQueryParam("time", endTime.Value.ToRFC3339());

            return await x.GetAsync().ReceiveJson<PromApiResponse>();
        }

        public static async Task<PromApiResponse> QueryAllAvgGroupSize(string timerange, DateTime? endTime = null)
        {
            var x = promQueryUrl.SetQueryParam("query", $"avg_over_time(frUtlLowMumimoDownlinkAvgGroupSize[{timerange}])/100", false);

            if (endTime.HasValue)
                x = x.SetQueryParam("time", endTime.Value.ToRFC3339());

            return await x.GetAsync().ReceiveJson<PromApiResponse>();
        }

        public static async Task<PromApiResponse> QueryAllAvgMultiplexingGain(string timerange, DateTime? endTime = null)
        {
            var x = promQueryUrl.SetQueryParam("query", $"avg_over_time(frUtlLowMumimoDownlinkMultiplexingGain[{timerange}])/100", false);

            if (endTime.HasValue)
                x = x.SetQueryParam("time", endTime.Value.ToRFC3339());

            return await x.GetAsync().ReceiveJson<PromApiResponse>();
        }


    }
}
