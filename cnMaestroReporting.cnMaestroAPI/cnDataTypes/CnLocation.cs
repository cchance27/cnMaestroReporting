using System.Text.Json.Serialization;

namespace cnMaestroReporting.cnMaestroAPI.cnDataType
{
    public struct CnLocation
    {
        [JsonPropertyName("type")]
        public readonly string type;

        [JsonPropertyName("coordinates")]
        public readonly decimal[] coordinates;
    }
}
