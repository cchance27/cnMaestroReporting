using cnMaestroAPI.cnDataTypes;
using System;
using System.Collections.Generic;
namespace cnMaestroReporting.Domain
{
    public class AccessPointRadioInfo
    {
        [KMLConfig(Hidden = true)]
        public string Name { get; set; }
        public string Hardware { get; set; }
        public double DlUsageAnalysis { get; set; }

        [KMLConfig(ConvertToUrl = true)]
        public string IP { get; set; }

        public string Esn { get; set; }

        [KMLConfig(Name = "SMs")]
        public string ConnectedSMs { get; set; }

        public string Lan { get; set; }

        public double Channel { get; set; }

        [KMLConfig(Name = "Color Code")]
        public byte ColorCode { get; set; }

        [KMLConfig(Name = "Sync", TrimAfter = "(")]
        public string SyncState { get; set; }

        [KMLConfig(Name = "Power")]
        public int TxPower { get; set; }

        public string Tower { get; set; }

        public int Azimuth { get; set; }

        public int Downtilt { get; set; }

        public TimeSpan Uptime { get; set; }

        [KMLConfig(Hidden = true)]
        public IEnumerable<AccessPointStatistic> Statistics { get; set; }
        [KMLConfig(Hidden = true)]
        public IEnumerable<CnAlarm> Alarms { get; set; }

    }
}
