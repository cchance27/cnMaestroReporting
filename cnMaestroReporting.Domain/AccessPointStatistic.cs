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
        public double BitzPerHzDownlink(int channelSizeMHz, int downlinkPercent) => KilobitToBit(this.DownlinkThroughput) / MegaHertzToHertz(channelSizeMHz) * IntPctToFloatPct(downlinkPercent) * IntPctToFloatPct(this.DownlinkUtilization);
        public double BitzPerHzUplink(int channelSizeMHz, int uplinkPercent) => KilobitToBit(this.UplinkThroughput) / MegaHertzToHertz(channelSizeMHz) * IntPctToFloatPct(uplinkPercent) * IntPctToFloatPct(this.UplinkUtilization);
        private double KilobitToBit(double x) => x * 1000f;
        private double MegaHertzToHertz(double x) => KiloHertzToHertz(MegahertzToKiloHertz(x));
        private double MegahertzToKiloHertz(double x) => x * 1000f;
        private double KiloHertzToHertz(double x) => x * 1000f;
        private double IntPctToFloatPct(double x) => x / 100f;



    }
}
