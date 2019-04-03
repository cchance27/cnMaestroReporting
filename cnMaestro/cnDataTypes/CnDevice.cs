using cnMaestro.JsonType;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace cnMaestro.cnDataType
{
    public struct CnDevice : ICnMaestroDataType
    {
        [JsonProperty("ap_group")]
        readonly string ap_group;

        [JsonProperty("country")]
        readonly string country;

        [JsonProperty("description")]
        readonly string description;

        [JsonProperty("config")]
        readonly CnDeviceConfig config;

        [JsonProperty("hardware_version")]
        readonly string hardware_version;

        [JsonProperty("inactive_software_version")]
        readonly string inactive_software_version;

        [JsonProperty("ip")]
        readonly string ip;

        [JsonProperty("location")]
        readonly CnLocation location;

        [JsonProperty("mac")]
        readonly string mac;

        [JsonProperty("managed_account")]
        readonly string managed_account;

        [JsonProperty("msn")]
        readonly string msn;

        [JsonProperty("name")]
        readonly string name;

        [JsonProperty("network")]
        readonly string network;

        [JsonProperty("product")]
        readonly string product;

        [JsonProperty("last_reboot_reason")]
        readonly string last_reboot_reason;

        [JsonProperty("maximum_range")]
        readonly string maximum_range;

        [JsonProperty("registration_date")]
        readonly string registration_date;

        [JsonProperty("site")]
        readonly string site;

        [JsonProperty("site_id")]
        readonly string site_id;

        [JsonProperty("software_version")]
        readonly string software_version;

        [JsonProperty("status")]
        readonly string status;

        [JsonProperty("status_time")]
        readonly string status_time;

        [JsonProperty("tower")]
        readonly string tower;

        [JsonProperty("type")]
        readonly string type;
    }

    public struct CnDeviceConfig
    {
        [JsonProperty("sync_reason")]
        readonly string sync_reason;

        [JsonProperty("sync_status")]
        readonly bool sync_status;

        [JsonProperty("variables")]
        readonly ReadOnlyDictionary<string, string> variables;

        [JsonProperty("version")]
        readonly int version;
    }
}
