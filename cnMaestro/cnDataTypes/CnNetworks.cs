using Newtonsoft.Json;

namespace cnMaestro.cnDataType
{
    public struct CnNetworks: ICnMaestroDataType
    {
        [JsonProperty("id")]
        readonly string Id;

        [JsonProperty("managed_account")]
        readonly string ManagedAccount;

        [JsonProperty("name")]
        readonly string Name;
    }
}
