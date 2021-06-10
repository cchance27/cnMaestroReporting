using System.Text.Json.Serialization;

namespace cnMaestroReporting.cnMaestroAPI.cnDataType
{
    public struct CnNetwork: ICnMaestroDataType
    {
        [JsonPropertyName("id")]
        public readonly string Id;

        [JsonPropertyName("managed_account")]
        public readonly string ManagedAccount;

        [JsonPropertyName("name")]
        public readonly string Name;
    }
}
