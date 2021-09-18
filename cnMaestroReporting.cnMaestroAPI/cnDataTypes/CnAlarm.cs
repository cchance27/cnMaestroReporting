using cnMaestroReporting.cnMaestroAPI.cnDataType;
using cnMaestroReporting.cnMaestroAPI.JsonType;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cnMaestroReporting.cnMaestroAPI.cnDataTypes
{
    public record CnAlarm : ICnMaestroDataType {
        public string ip { get; init; }
        public string network { get; init; }
        public string message { get; init; }
        public string name { get; init; }
        public CnSeverity severity { get; init; }
        public string source_type { get; init; }
        public string status { get; init; }
        public DateTime time_raised { get; init; }
        public DateTime? time_cleared  { get; init; }
        public string tower  { get; init; }
        public int duration { get; init; }
        public string id { get; init; }
        public string code { get; init; }
        public string mac { get; init; }
        public string acknowledged_by { get; init; }
        public string source { get; init; }
        public string managed_account { get; init; }

    }
}
