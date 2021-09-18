using System;

namespace cnMaestroReporting.Domain
{
    public class AccessPointStatistic
    {
        public string TimeStamp { get; init; }
        public double UplinkThroughput { get; init; }
        public double UplinkUtilization { get; init; }
        public double DownlinkThroughput { get; init; }
        public double DownlinkUtilization { get; init; }
    }
}
