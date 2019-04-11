using CommonCalculations;

namespace CambiumSignalValidator
{
    public static class RadioConfig
    {
        // 450m : 14dBi antenna / 24dBm combined
        // 450i : 16dBi antenna
        // 450 : 16 dBi antenna
        public static RFCalc.Radio CambiumAP(int TxPower, int Gain = 16) => new RFCalc.Radio { AntennaGain = Gain, RadioPower = TxPower, InternalLoss = 1 };
        public static RFCalc.Radio CambiumSM(int TxPower, int Gain = 9) => new RFCalc.Radio { AntennaGain = Gain, RadioPower = TxPower, InternalLoss = 1 };
    }
}