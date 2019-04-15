using System;

namespace CambiumSignalValidator
{
    public struct SubscriberRadioInfo
    {
        public string Name { get; set; }
        public string Esn { get; set; }
        public string APName { get; set; }
        public double DistanceM { get; set; }
        public string Model { get; set; }
        public int SMTxPower { get; set; }
        public int SMMaxTxPower { get; set; }
        public double SmEPL { get; set; }
        public double SmAPL { get; set; }
        public double SmPowerDiff { get => Math.Round(Math.Abs(SmEPL - SmAPL), 2); }
        public string ApModel { get; set; }
        public int APTxPower { get; set; }
        public double ApEPL { get; set; }
        public double ApAPL { get; set; }
        public double ApPowerDiff { get => Math.Round(Math.Abs(ApEPL - ApAPL), 2); }        
    }
}