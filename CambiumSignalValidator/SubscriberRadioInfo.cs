using System;

namespace CambiumSignalValidator
{
    public struct SubscriberRadioInfo
    {
        public string Name { get; set; }
        public string Esn { get; set; }
        public string Location { get; set; }
        public string APName { get; set; }
        public string ApModel { get; set; }
        public string Firmware { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string IP { get; set; }
        public int DistanceM { get; set; }
        public string Model { get; set; }
        public int SmGain { get; set; }
        public int SmTxPower { get; set; }
        public int SmMaxTxPower { get; set; }
        public double SmEPL { get; set; }
        public double SmAPL { get; set; }
        public double SmPowerDiff { get => Math.Round(Math.Abs(SmEPL - SmAPL), 2); }
        public double SmImbalance { get; set; }
        public int ApTxPower { get; set; }
        public double ApEPL { get; set; }
        public double ApAPL { get; set; }
        public double ApPowerDiff { get => Math.Round(Math.Abs(ApEPL - ApAPL), 2); }        
    }
}