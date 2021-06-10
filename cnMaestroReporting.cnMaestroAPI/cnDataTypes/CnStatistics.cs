using System;
using System.Text.Json.Serialization;

namespace cnMaestroReporting.cnMaestroAPI.cnDataType
{
    public struct CnStatistics: ICnMaestroDataType
    {
        [JsonPropertyName("ap_mac")]
        public readonly  string ap_mac;

        [JsonPropertyName("config_version")]
        public readonly  string config_version;

        [JsonPropertyName("connected_sms")]
        public readonly  string connected_sms;

        [JsonPropertyName("distance")]
        public readonly  string distance;

        [JsonPropertyName("gain")]
        public readonly  string gain;

        [JsonPropertyName("gps_sync_state")]
        public readonly  string gps_sync_state;

        [JsonPropertyName("last_sync")]
        public readonly  string last_sync;

        [JsonPropertyName("mac")]
        public readonly  string mac;

        [JsonPropertyName("managed_account")]
        public readonly  string managed_account;

        [JsonPropertyName("mode")]
        public readonly  string mode;

        [JsonPropertyName("name")]
        public readonly  string name;

        [JsonPropertyName("network")]
        public readonly  string network;

        [JsonPropertyName("parent_mac")]
        public readonly  string parent_mac;

        [JsonPropertyName("reboots")]
        public readonly  string reboots;

        [JsonPropertyName("site")]
        public readonly  string site;

        [JsonPropertyName("site_id")]
        public readonly  string site_id;

        [JsonPropertyName("status")]
        public readonly  string status;

        [JsonPropertyName("status_time")]
        public readonly  string status_time;

        [JsonPropertyName("temperature")]
        public readonly  string temperature;

        [JsonPropertyName("tower")]
        public readonly  string tower;

        [JsonPropertyName("type")]
        public readonly  string type;

        [JsonPropertyName("vlan")]
        public readonly  string vlan;

        [JsonPropertyName("default_gateway")]
        public readonly  string default_gateway;

        [JsonPropertyName("ip_dns")]
        public readonly  string ip_dns;

        [JsonPropertyName("ip_dns_secondary")]
        public readonly  string ip_dns_secondary;

        [JsonPropertyName("ip_wan")]
        public readonly  string ip_wan;

        [JsonPropertyName("lan_mode_status")]
        public readonly  string lan_mode_status;

        [JsonPropertyName("lan_mtu")]
        public readonly  string lan_mtu;

        [JsonPropertyName("lan_speed_status")]
        public readonly  string lan_speed_status;

        [JsonPropertyName("lan_status")]
        public readonly  string lan_status;

        [JsonPropertyName("netmask")]
        public readonly  string netmask;

        [JsonPropertyName("radio")]
        public readonly  CnFixedRadioStatistics radio;
    }

    public class CnFixedRadioStatistics
    {
        [JsonPropertyName("auth_mode")]
        public readonly  string auth_mode;

        [JsonPropertyName("auth_type")]
        public readonly  string auth_type;

        [JsonPropertyName("channel_width")]
        public readonly  string channel_width;

        [JsonPropertyName("color_code")]
        public readonly  string color_code;

        [JsonPropertyName("dfs_status")]
        public readonly  string dfs_status;

        [JsonPropertyName("wlan_status")]
        public readonly  string wlan_status;

        [JsonPropertyName("dl_frame_utilization")]
        public readonly  double? dl_frame_utilization;

        [JsonPropertyName("dl_mcs")]
        public readonly  int? dl_mcs;

        [JsonPropertyName("dl_modulation")]
        public readonly  string dl_modulation;

        [JsonPropertyName("dl_pkts")]
        public readonly  Int64? dl_pkts;

        [JsonPropertyName("dl_pkts_loss")]
        public readonly  Int64? dl_pkts_loss;

        [JsonPropertyName("dl_retransmits")]
        public readonly  int? dl_retransmits;

        [JsonPropertyName("dl_retransmits_pct")]
        public readonly  int? dl_retransmits_pct;

        [JsonPropertyName("dl_rssi")]
        public readonly  double? dl_rssi;

        [JsonPropertyName("dl_rssi_imbalance")]
        public readonly  double? dl_rssi_imbalance;

        [JsonPropertyName("dl_snr")]
        public readonly  int? dl_snr;

        [JsonPropertyName("dl_snr_v")]
        public readonly  int? dl_snr_v;

        [JsonPropertyName("dl_snr_h")]
        public readonly  int? dl_snr_h;

        [JsonPropertyName("dl_throughput")]
        public readonly  Int64? dl_throughput;

        [JsonPropertyName("frame_period")]
        public readonly  string frame_period;

        [JsonPropertyName("frequency")]
        public readonly  string frequency;

        [JsonPropertyName("mac")]
        public readonly  string mac;

        [JsonPropertyName("mode")]
        public readonly  string mode;

        [JsonPropertyName("sessions_dropped")]
        public readonly  string sessions_dropped;

        [JsonPropertyName("ssid")]
        public readonly  string ssid;

        [JsonPropertyName("sync_source")]
        public readonly  string sync_source;

        [JsonPropertyName("sync_state")]
        public readonly  string sync_state;

        [JsonPropertyName("tdd_ratio")]
        public readonly  string tdd_ratio;

        [JsonPropertyName("tx_capacity")]
        public readonly  string tx_capacity;

        [JsonPropertyName("tx_power")]
        public readonly  int? tx_power;

        [JsonPropertyName("tx_quality")]
        public readonly  string tx_quality;

        [JsonPropertyName("ul_frame_utilization")]
        public readonly  double? ul_frame_utilization;

        [JsonPropertyName("ul_mcs")]
        public readonly  int? ul_mcs;

        [JsonPropertyName("ul_modulation")]
        public readonly  string ul_modulation;

        [JsonPropertyName("ul_pkts")]
        public readonly  Int64? ul_pkts;

        [JsonPropertyName("ul_pkts_loss")]
        public readonly  Int64? ul_pkts_loss;

        [JsonPropertyName("ul_retransmits")]
        public readonly  int? ul_retransmits;

        [JsonPropertyName("ul_retransmits_pct")]
        public readonly  int? ul_retransmits_pct;

        [JsonPropertyName("ul_rssi")]
        public readonly  double? ul_rssi;

        [JsonPropertyName("ul_rssi_imbalance")]
        public readonly  double? ul_rssi_imbalance;

        [JsonPropertyName("ul_snr")]
        public readonly  int? ul_snr;

        [JsonPropertyName("ul_snr_v")]
        public readonly  int? ul_snr_v;

        [JsonPropertyName("ul_snr_h")]
        public readonly  int? ul_snr_h;

        [JsonPropertyName("ul_throughput")]
        public readonly  Int64? ul_throughput;
    }
}
