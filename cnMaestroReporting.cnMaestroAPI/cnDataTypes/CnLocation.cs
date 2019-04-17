using Newtonsoft.Json;

namespace cnMaestroReporting.cnMaestroAPI.cnDataType
{
    public struct CnLocation
    {
        [JsonProperty("type")]
        public readonly string type;

        [JsonProperty("coordinates")]
        public readonly decimal[] coordinates;
    }
}
