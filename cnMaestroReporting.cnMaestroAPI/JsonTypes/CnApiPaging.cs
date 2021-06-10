using System.Text.Json.Serialization;

namespace cnMaestroReporting.cnMaestroAPI.JsonType
{
    public struct CnApiPaging
    {
        [JsonPropertyName("total")]
        public readonly int Total;

        [JsonPropertyName("limit")]
        public readonly int Limit;

        [JsonPropertyName("offset")]
        public readonly int Offset;
    }
}
