using Newtonsoft.Json;

namespace cnMaestro.cnDataType
{
    public struct CnLocation
    {
        [JsonProperty("type")]
        readonly string type;

        [JsonProperty("coordinates")]
        readonly decimal[] coordinates;
    }
}
