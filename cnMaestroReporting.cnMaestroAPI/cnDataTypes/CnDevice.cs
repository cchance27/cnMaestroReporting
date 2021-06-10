
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace cnMaestroReporting.cnMaestroAPI.cnDataType
{
    public struct CnDevice : ICnMaestroDataType
    {
        [JsonPropertyName("ap_group")]
        public readonly string ap_group;

        [JsonPropertyName("country")]
        public readonly string country;

        [JsonPropertyName("description")]
        public readonly string description;

        [JsonPropertyName("config")]
        public readonly CnDeviceConfig config;

        [JsonPropertyName("hardware_version")]
        public readonly string hardware_version;

        [JsonPropertyName("inactive_software_version")]
        public readonly string inactive_software_version;

        [JsonPropertyName("ip")]
        public readonly string ip;

        [JsonPropertyName("location")]
        public readonly CnLocation location;

        [JsonPropertyName("mac")]
        public readonly string mac;

        [JsonPropertyName("managed_account")]
        public readonly string managed_account;

        [JsonPropertyName("msn")]
        public readonly string msn;

        [JsonPropertyName("name")]
        public readonly string name;

        [JsonPropertyName("network")]
        public readonly string network;

        [JsonPropertyName("product")]
        public readonly string product;

        [JsonPropertyName("last_reboot_reason")]
        public readonly string last_reboot_reason;

        [JsonPropertyName("maximum_range")]
        public readonly string maximum_range;

        [JsonPropertyName("registration_date")]
        public readonly string registration_date;

        [JsonPropertyName("site")]
        public readonly string site;

        [JsonPropertyName("site_id")]
        public readonly string site_id;

        [JsonPropertyName("software_version")]
        public readonly string software_version;

        [JsonPropertyName("status")]
        public readonly string status;

        [JsonPropertyName("status_time")]
        public readonly string status_time;

        [JsonPropertyName("tower")]
        public readonly string tower;

        [JsonPropertyName("type")]
        public readonly string type;
    }

    public struct CnDeviceConfig
    {
        [JsonPropertyName("sync_reason")]
        public readonly string sync_reason;

        [JsonPropertyName("sync_status")]
        public readonly bool sync_status;

        [JsonPropertyName("variables")]
        public readonly ReadOnlyDictionary<string, string> variables;

        [JsonPropertyName("version")]
        public readonly int version;
    }
}
