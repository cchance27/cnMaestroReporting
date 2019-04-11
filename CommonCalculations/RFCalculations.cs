using System;

namespace CommonCalculations
{
    public static class RFCalc
    {
        public struct Radio
        {
            public int RadioPower;
            public int AntennaGain;
            public int InternalLoss;
        }
        
        public static double FreeSpacePathLoss(double DistanceM, double FrequencyHz) =>
               32.44 + 20 * Math.Log(FrequencyHz / 1000) / 2.302585093 + 20 * Math.Log(DistanceM / 1000) / 2.302585093;

        public static double EstimatedPowerLevel(double DistanceM, double FrequencyHz, int MiscLoss, Radio Tx, Radio Rx) =>
            Tx.RadioPower + Tx.AntennaGain - Tx.InternalLoss
            - FreeSpacePathLoss(DistanceM, FrequencyHz)
            - MiscLoss
            + Rx.AntennaGain - Rx.InternalLoss;

        public static Int32 SpeedOfLight = 299792458;
        public static Int32 NanoSecondsPerSecond = 1000000000;

        public static Decimal FreqHzToWaveLength(int FreqHz) => SpeedOfLight / FreqHz;
        public static Decimal FreqHzToMetersPerSec(int FreqHz) => FreqHzToWaveLength(FreqHz) * FreqHz;
        public static Decimal FreqHzToNanoMetersPerSec(int FreqHz) => (FreqHzToWaveLength(FreqHz) * FreqHz) / NanoSecondsPerSecond;

        public static Decimal MetersFromAirDelay(int AirDelayNS, int HzFrequency, bool OneWayAirDelayNS = false)
        {
            Decimal NanoMetersPerSec = RFCalc.FreqHzToNanoMetersPerSec(HzFrequency);

            // AirDelay is normally bi directional, but lets support handling it both ways based on flag
            Int32 AirDelayDistance = OneWayAirDelayNS ? AirDelayNS : AirDelayNS / 2;

            return NanoMetersPerSec * AirDelayDistance;

        }


    }
}
