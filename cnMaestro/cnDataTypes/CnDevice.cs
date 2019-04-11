using cnMaestro.JsonType;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace cnMaestro.cnDataType
{
    public struct CnDevice : ICnMaestroDataType
    {
        [JsonProperty("ap_group")]
        public readonly string ap_group;

        [JsonProperty("country")]
        public readonly string country;

        [JsonProperty("description")]
        public readonly string description;

        [JsonProperty("config")]
        public readonly CnDeviceConfig config;

        [JsonProperty("hardware_version")]
        public readonly string hardware_version;

        [JsonProperty("inactive_software_version")]
        public readonly string inactive_software_version;

        [JsonProperty("ip")]
        public readonly string ip;

        [JsonProperty("location")]
        public readonly CnLocation location;

        [JsonProperty("mac")]
        public readonly string mac;

        [JsonProperty("managed_account")]
        public readonly string managed_account;

        [JsonProperty("msn")]
        public readonly string msn;

        [JsonProperty("name")]
        public readonly string name;

        [JsonProperty("network")]
        public readonly string network;

        [JsonProperty("product")]
        public readonly string product;

        [JsonProperty("last_reboot_reason")]
        public readonly string last_reboot_reason;

        [JsonProperty("maximum_range")]
        public readonly string maximum_range;

        [JsonProperty("registration_date")]
        public readonly string registration_date;

        [JsonProperty("site")]
        public readonly string site;

        [JsonProperty("site_id")]
        public readonly string site_id;

        [JsonProperty("software_version")]
        public readonly string software_version;

        [JsonProperty("status")]
        public readonly string status;

        [JsonProperty("status_time")]
        public readonly string status_time;

        [JsonProperty("tower")]
        public readonly string tower;

        [JsonProperty("type")]
        public readonly string type;
    }

    public struct CnDeviceConfig
    {
        [JsonProperty("sync_reason")]
        public readonly string sync_reason;

        [JsonProperty("sync_status")]
        public readonly bool sync_status;

        [JsonProperty("variables")]
        public readonly ReadOnlyDictionary<string, string> variables;

        [JsonProperty("version")]
        public readonly int version;
    }
}
