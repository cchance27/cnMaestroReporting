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

        public static Double FreqHzToWaveLength(double FreqHz) => SpeedOfLight / FreqHz;
        public static Double FreqHzToMetersPerSec(int FreqHz) => FreqHzToWaveLength(FreqHz) * FreqHz;
        public static Double FreqHzToNanoMetersPerSec(double FreqHz) => (FreqHzToWaveLength(FreqHz) * FreqHz) / NanoSecondsPerSecond;

        /// <summary>
        /// Calculate the Distance based on the NanoSecond AirDelay and Frequency in use
        /// </summary>
        /// <param name="AirDelayNS"></param>
        /// <param name="HzFrequency"></param>
        /// <param name="OneWayAirDelayNS"></param>
        /// <returns></returns>
        public static Double MetersFromAirDelay(int AirDelayNS, double HzFrequency, bool OneWayAirDelayNS = false)
        {
            Double NanoMetersPerSec = RFCalc.FreqHzToNanoMetersPerSec(HzFrequency);

            // AirDelay is normally bi directional, but lets support handling it both ways based on flag
            Int32 AirDelayDistance = OneWayAirDelayNS ? AirDelayNS : AirDelayNS / 2;

            return NanoMetersPerSec * AirDelayDistance;

        }


    }
}
