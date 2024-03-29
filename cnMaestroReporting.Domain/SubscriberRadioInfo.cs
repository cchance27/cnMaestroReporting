﻿using System;

namespace cnMaestroReporting.Domain
{
    public struct SubscriberRadioInfo
    {
        [KMLConfig(Hidden = true)]
        public string Name { get; set; }

        [KMLConfig(ConvertToUrl = true)]
        public string IP { get; set; }

        public string Esn { get; set; }
        public bool Online { get; set; }

        [KMLConfig(Hidden = true)]
        public string Tower { get; set; }

        [KMLConfig(Hidden = true)]
        public string APBand { get; set; }
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
        public string DlMod { get => ModToQAM(int.Parse(DownlinkModulation?.Split("X")[0] ?? "1")); }

        [KMLConfig(Hidden = true)]
        public string UlMod { get => ModToQAM(int.Parse(UplinkModulation?.Split("X")[0] ?? "1")); }

        [KMLConfig(Hidden = true)]
        public string DlMimo { get => DownlinkModulation?.Split("-")[1] ?? "A"; }

        [KMLConfig(Hidden = true)]
        public string UlMimo { get => UplinkModulation?.Split("-")[1] ?? "A"; }

        public string EIPAccount { get; set; }
 
        public string EIPService { get; set; }

        public double EIPValue { get; set; }

        public double DLGBDataUsage { get; set; }
        public double ULGBDataUsage { get; set; }

        public double RFWeight { 
            get {
                double RFMultiplierDL = RFToMultiplier(DownlinkModulation);
                double RFMultiplierUL = RFToMultiplier(UplinkModulation);

                return (DLGBDataUsage * RFMultiplierDL) + (ULGBDataUsage + RFMultiplierUL);
            } 
        }

        private static string ModToQAM(int canopyModulation)
        {
            canopyModulation = canopyModulation - 1;
            string[] modulations = { "BPSK", "QPSK", "8-QAM", "16-QAM", "32-QAM", "64-QAM", "128-QAM", "256-QAM" };
            return modulations[canopyModulation];
        }

        private static double RFToMultiplier(string Modulation)
        {
            double RFMultiplier = 9 - int.Parse(Modulation?.Split("X")[0] ?? "8");
            RFMultiplier = (Modulation?.Split("-")[1] ?? "B") == "A" ? RFMultiplier * 2 : RFMultiplier;
            return Math.Pow(RFMultiplier, RFMultiplier / 4);
        }
    }
}