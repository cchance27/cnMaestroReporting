using Newtonsoft.Json;
using System;

namespace cnMaestro.cnDataType
{
    public struct CnStatistics: ICnMaestroDataType
    {
        [JsonProperty("ap_mac")]
        readonly string ap_mac;

        [JsonProperty("config_version")]
        readonly string config_version;

        [JsonProperty("connected_sms")]
        readonly string connected_sms;

        [JsonProperty("distance")]
        readonly string distance;

        [JsonProperty("gain")]
        readonly string gain;

        [JsonProperty("gps_sync_state")]
        readonly string gps_sync_state;

        [JsonProperty("last_sync")]
        readonly string last_sync;

        [JsonProperty("mac")]
        readonly string mac;

        [JsonProperty("managed_account")]
        readonly string managed_account;

        [JsonProperty("mode")]
        readonly string mode;

        [JsonProperty("name")]
        readonly string name;

        [JsonProperty("network")]
        readonly string network;

        [JsonProperty("parent_mac")]
        readonly string parent_mac;

        [JsonProperty("reboots")]
        readonly string reboots;

        [JsonProperty("site")]
        readonly string site;

        [JsonProperty("site_id")]
        readonly string site_id;

        [JsonProperty("status")]
        readonly string status;

        [JsonProperty("status_time")]
        readonly string status_time;

        [JsonProperty("temperature")]
        readonly string temperature;

        [JsonProperty("tower")]
        readonly string tower;

        [JsonProperty("type")]
        readonly string type;

        [JsonProperty("vlan")]
        readonly string vlan;

        [JsonProperty("default_gateway")]
        readonly string default_gateway;

        [JsonProperty("ip_dns")]
        readonly string ip_dns;

        [JsonProperty("ip_dns_secondary")]
        readonly string ip_dns_secondary;

        [JsonProperty("ip_wan")]
        readonly string ip_wan;

        [JsonProperty("lan_mode_status")]
        readonly string lan_mode_status;

        [JsonProperty("lan_mtu")]
        readonly string lan_mtu;

        [JsonProperty("lan_speed_status")]
        readonly string lan_speed_status;

        [JsonProperty("lan_status")]
        readonly string lan_status;

        [JsonProperty("netmask")]
        readonly string netmask;

        [JsonProperty("radio")]
        readonly CnFixedRadioStatistics radio;
    }

    public class CnFixedRadioStatistics
    {
        [JsonProperty("auth_mode")]
        readonly string auth_mode;

        [JsonProperty("auth_type")]
        readonly string auth_type;

        [JsonProperty("channel_width")]
        readonly string channel_width;

        [JsonProperty("color_code")]
        readonly string color_code;

        [JsonProperty("dfs_status")]
        readonly string dfs_status;

        [JsonProperty("wlan_status")]
        readonly string wlan_status;

        [JsonProperty("dl_frame_utilization")]
        readonly float dl_frame_utilization;

        [JsonProperty("dl_mcs")]
        readonly int dl_mcs;

        [JsonProperty("dl_modulation")]
        readonly string dl_modulation;

        [JsonProperty("dl_pkts")]
        readonly Int64? dl_pkts;

        [JsonProperty("dl_pkts_loss")]
        readonly Int64? dl_pkts_loss;

        [JsonProperty("dl_retransmits")]
        readonly int dl_retransmits;

        [JsonProperty("dl_retransmits_pct")]
        readonly int dl_retransmits_pct;

        [JsonProperty("dl_rssi")]
        readonly float dl_rssi;

        [JsonProperty("dl_rssi_imbalance")]
        readonly float dl_rssi_imbalance;

        [JsonProperty("dl_snr")]
        readonly int dl_snr;

        [JsonProperty("dl_snr_v")]
        readonly int dl_snr_v;

        [JsonProperty("dl_snr_h")]
        readonly int dl_snr_h;

        [JsonProperty("dl_throughput")]
        readonly Int64? dl_throughput;

        [JsonProperty("frame_period")]
        readonly string frame_period;

        [JsonProperty("frequency")]
        readonly string frequency;

        [JsonProperty("mac")]
        readonly string mac;

        [JsonProperty("mode")]
        readonly string mode;

        [JsonProperty("sessions_dropped")]
        readonly string sessions_dropped;

        [JsonProperty("ssid")]
        readonly string ssid;

        [JsonProperty("sync_source")]
        readonly string sync_source;

        [JsonProperty("sync_state")]
        readonly string sync_state;

        [JsonProperty("tdd_ratio")]
        readonly string tdd_ratio;

        [JsonProperty("tx_capacity")]
        readonly string tx_capacity;

        [JsonProperty("tx_power")]
        readonly string tx_power;

        [JsonProperty("tx_quality")]
        readonly string tx_quality;

        [JsonProperty("ul_frame_utilization")]
        readonly float ul_frame_utilization;

        [JsonProperty("ul_mcs")]
        readonly int ul_mcs;

        [JsonProperty("ul_modulation")]
        readonly string ul_modulation;

        [JsonProperty("ul_pkts")]
        readonly Int64? ul_pkts;

        [JsonProperty("ul_pkts_loss")]
        readonly Int64? ul_pkts_loss;

        [JsonProperty("ul_retransmits")]
        readonly int ul_retransmits;

        [JsonProperty("ul_retransmits_pct")]
        readonly int ul_retransmits_pct;

        [JsonProperty("ul_rssi")]
        readonly float ul_rssi;

        [JsonProperty("ul_rssi_imbalance")]
        readonly float ul_rssi_imbalance;

        [JsonProperty("ul_snr")]
        readonly int ul_snr;

        [JsonProperty("ul_snr_v")]
        readonly int ul_snr_v;

        [JsonProperty("ul_snr_h")]
        readonly int ul_snr_h;

        [JsonProperty("ul_throughput")]
        readonly Int64? ul_throughput;
    }
}
