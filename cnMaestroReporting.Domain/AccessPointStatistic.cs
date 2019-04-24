using System;

namespace cnMaestroReporting.Domain
{
    public class AccessPointStatistic
    {
        public DateTime DateTime { get; }
        public double UplinkThroughput { get; }
        public double UplinkUtilization { get; }
        public double DownlinkThroughput { get; }
        public double DownlinkUtilization { get; }
    }
}
