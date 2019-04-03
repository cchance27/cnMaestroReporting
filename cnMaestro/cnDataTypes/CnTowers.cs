using cnMaestro.JsonType;
using Newtonsoft.Json;

namespace cnMaestro.cnDataType
{
    public struct CnTowers: ICnMaestroDataType
    {
        [JsonProperty("id")]
        readonly string Id;

        [JsonProperty("managed_account")]
        readonly string ManagedAccount;

        [JsonProperty("name")]
        readonly string Name;

        [JsonProperty("location")]
        readonly CnLocation Location;

        [JsonProperty("network")]
        readonly string Network;
    }
}
