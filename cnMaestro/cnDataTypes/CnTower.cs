using cnMaestro.JsonType;
using Newtonsoft.Json;

namespace cnMaestro.cnDataType
{
    public struct CnTower: ICnMaestroDataType
    {
        [JsonProperty("id")]
        public readonly string Id;

        [JsonProperty("managed_account")]
        public readonly string ManagedAccount;

        [JsonProperty("name")]
        public readonly string Name;

        [JsonProperty("location")]
        public readonly CnLocation Location;

        [JsonProperty("network")]
        public readonly string Network;
    }
}
