using Flurl.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace cnMaestroReporting.cnMaestroAPI
{
    public static class FlurlExceptionChecks
    {
        /// <summary>
        /// Flurl Authentication Check
        /// </summary>
        /// <returns></returns>
        public static bool HandleAuthFailure(FlurlHttpException ex)
        {
            int[] authErrors =
            {
                (int)HttpStatusCode.Forbidden,
                (int)HttpStatusCode.Unauthorized
            };

            return ex.StatusCode.HasValue && authErrors.Contains(ex.StatusCode!.Value);
        }

        /// <summary>
        /// Transient Error Check
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public static bool HandleTransient(FlurlHttpException ex)
        {
            int[] transientErrors =
            {
                    (int)HttpStatusCode.RequestTimeout, // 408
                    (int)HttpStatusCode.BadGateway, // 502
                    (int)HttpStatusCode.ServiceUnavailable, // 503
                    (int)HttpStatusCode.GatewayTimeout // 504
                };

            return ex.StatusCode.HasValue && transientErrors.Contains(ex.StatusCode.Value);
        }
    }
}