using System;
using System.Collections.Generic;
using System.Linq;

namespace cnMaestroReporting.Prometheus
{
    public static class PrometheusExtension
    {
        public static (int, decimal, decimal) TopPercentage(this IEnumerable<PromResult> orderedArr, decimal pct, Func<string, bool> allowedIpFunc = null)
        {
            decimal topPctAmount = orderedArr.Sum(x => decimal.Parse(x.value[1])) * pct;
            int running = 0;
            decimal runningAmt = 0m;
            decimal includedAmt = 0m;

            for (int i = 0; i < orderedArr.Count(); i++)
            {
                if (runningAmt > topPctAmount)
                    break;

                runningAmt += decimal.Parse(orderedArr.ElementAt(i).value[1]);


                //check if we have a allowedIpFilter, if we do add only if we are allowed to add
                if (allowedIpFunc is null || allowedIpFunc(orderedArr.ElementAt(i).metric.instance)) { 
                    running++;
                    includedAmt += decimal.Parse(orderedArr.ElementAt(i).value[1]);
                }
            }

            return (running, includedAmt, runningAmt);
        }
    }
}
