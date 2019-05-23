using System;

namespace cnMaestroReporting.Domain
{
    public struct SubscriberRadioInfo
    {
        [KMLConfig(Hidden = true)]
        public string Name { get; set; }

        [KMLConfig(ConvertToUrl = true)]
        public string IP { get; set; }

        public string Esn { get; set; }

        [KMLConfig(Hidden = true)]
        public string Tower { get; set; }

        [KMLConfig(Hidden = true)]
        public string APName { get; set; }

        [KMLConfig(Hidden = true)]
        public string ApModel { get; set; }

        public string Firmware { get; set; }

        [KMLConfig(Hidden = true)]
        public decimal Latitude { get; set; }

        [KMLConfig(Hidden = true)]
        public decimal Longitude { get; set; }

        /// <summary>
        /// This distance represents the real world distance based on AirDelay and frequency calculations.
        /// </summary>
        public int DistanceM { get; set; }

        /// <summary>
        /// This distance represents the distance from AP based on the SM's programmed Latitude and Longitude
        /// </summary>
        public int DistanceGeoM { get; set; }

        public string Model { get; set; }

        [KMLConfig(Hidden = true)]
        public int SmGain { get; set; }

        [KMLConfig(Hidden = true)]
        public int SmTxPower { get; set; }

        [KMLConfig(Hidden = true)]
        public int SmMaxTxPower { get; set; }

        [KMLConfig(Hidden = true)]
        public double SmPowerDiff { get => Math.Round(Math.Abs(SmEPL - SmAPL), 2); }

        [KMLConfig(Name = "SM Imbalance")]
        public double SmImbalance { get; set; }

        [KMLConfig(Hidden = true)]
        public int ApTxPower { get; set; }

        [KMLConfig(Name = "SM Expected Power")]
        public double SmEPL { get; set; }

        [KMLConfig(Name = "SM Actual Power")]
        public double SmAPL { get; set; }

        [KMLConfig(Name = "AP Expected Power")]
        public double ApEPL { get; set; }

        [KMLConfig(Name = "AP Actual Power")]
        public double ApAPL { get; set; }

        [KMLConfig(Hidden = true)]
        public double ApPowerDiff { get => Math.Round(Math.Abs(ApEPL - ApAPL), 2); }        
    }
}