using Newtonsoft.Json;

namespace cnMaestro.JsonType
{
    public struct CnApiPaging
    {
        [JsonProperty("total")]
        public readonly int Total;

        [JsonProperty("limit")]
        public readonly int Limit;

        [JsonProperty("offset")]
        public readonly int Offset;
    }
}
