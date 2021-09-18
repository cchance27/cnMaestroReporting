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
        public string APEsn { get; set; }

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
        
        [KMLConfig(Name = "SM SNR Horizontal")]
        public int SmSNRH { get; set; }

        [KMLConfig(Name = "SM SNR Vertical")]
        public int SmSNRV { get; set; }

        [KMLConfig(Name = "AP SNR Horizontal")]
        public int ApSNRH { get; set; }

        [KMLConfig(Name = "AP SNR Vertical")]
        public int ApSNRV { get; set; }

        public string DownlinkModulation { get; set; }

        public string UplinkModulation { get; set; }

        [KMLConfig(Hidden = true)]
        public readonly string DlMod { get => ModToQAM(int.Parse(DownlinkModulation.Split("X")[0])); }

        [KMLConfig(Hidden = true)]
        public readonly string UlMod { get => ModToQAM(int.Parse(UplinkModulation.Split("X")[0])); }

        [KMLConfig(Hidden = true)]
        public readonly string DlMimo { get => DownlinkModulation.Split("-")[1]; }

        [KMLConfig(Hidden = true)]
        public readonly string UlMimo { get => UplinkModulation.Split("-")[1]; }
        private static string ModToQAM(int canopyModulation)
        {
            canopyModulation = canopyModulation - 1;
            string[] modulations = { "BPSK", "QPSK", "8-QAM", "16-QAM", "32-QAM", "64-QAM", "128-QAM", "256-QAM" };
            return modulations[canopyModulation];
        }
    }
}