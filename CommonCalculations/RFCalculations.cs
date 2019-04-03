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

        public static double FreeSpacePathLoss(int DistanceM, double FrequencyMhz) => 20 * Math.Log(DistanceM) / Math.Log(10) + 20 * Math.Log(FrequencyMhz) / Math.Log(10) - 147.55;

        public static double EstimatedPowerLevel(int DistanceM, int FrequencyMhz, int MiscLoss, Radio Tx, Radio Rx) =>
            Tx.RadioPower + Tx.AntennaGain - Tx.InternalLoss
            - FreeSpacePathLoss(DistanceM, (FrequencyMhz * Math.Pow(10, 6)))
            - MiscLoss
            + Rx.AntennaGain - Rx.InternalLoss;

    }
}
