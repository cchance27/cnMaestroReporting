using Newtonsoft.Json;
using System;

namespace cnMaestro.cnDataType
{
    public struct CnStatistics: ICnMaestroDataType
    {
        [JsonProperty("ap_mac")]
        public readonly  string ap_mac;

        [JsonProperty("config_version")]
        public readonly  string config_version;

        [JsonProperty("connected_sms")]
        public readonly  string connected_sms;

        [JsonProperty("distance")]
        public readonly  string distance;

        [JsonProperty("gain")]
        public readonly  string gain;

        [JsonProperty("gps_sync_state")]
        public readonly  string gps_sync_state;

        [JsonProperty("last_sync")]
        public readonly  string last_sync;

        [JsonProperty("mac")]
        public readonly  string mac;

        [JsonProperty("managed_account")]
        public readonly  string managed_account;

        [JsonProperty("mode")]
        public readonly  string mode;

        [JsonProperty("name")]
        public readonly  string name;

        [JsonProperty("network")]
        public readonly  string network;

        [JsonProperty("parent_mac")]
        public readonly  string parent_mac;

        [JsonProperty("reboots")]
        public readonly  string reboots;

        [JsonProperty("site")]
        public readonly  string site;

        [JsonProperty("site_id")]
        public readonly  string site_id;

        [JsonProperty("status")]
        public readonly  string status;

        [JsonProperty("status_time")]
        public readonly  string status_time;

        [JsonProperty("temperature")]
        public readonly  string temperature;

        [JsonProperty("tower")]
        public readonly  string tower;

        [JsonProperty("type")]
        public readonly  string type;

        [JsonProperty("vlan")]
        public readonly  string vlan;

        [JsonProperty("default_gateway")]
        public readonly  string default_gateway;

        [JsonProperty("ip_dns")]
        public readonly  string ip_dns;

        [JsonProperty("ip_dns_secondary")]
        public readonly  string ip_dns_secondary;

        [JsonProperty("ip_wan")]
        public readonly  string ip_wan;

        [JsonProperty("lan_mode_status")]
        public readonly  string lan_mode_status;

        [JsonProperty("lan_mtu")]
        public readonly  string lan_mtu;

        [JsonProperty("lan_speed_status")]
        public readonly  string lan_speed_status;

        [JsonProperty("lan_status")]
        public readonly  string lan_status;

        [JsonProperty("netmask")]
        public readonly  string netmask;

        [JsonProperty("radio")]
        public readonly  CnFixedRadioStatistics radio;
    }

    public class CnFixedRadioStatistics
    {
        [JsonProperty("auth_mode")]
        public readonly  string auth_mode;

        [JsonProperty("auth_type")]
        public readonly  string auth_type;

        [JsonProperty("channel_width")]
        public readonly  string channel_width;

        [JsonProperty("color_code")]
        public readonly  string color_code;

        [JsonProperty("dfs_status")]
        public readonly  string dfs_status;

        [JsonProperty("wlan_status")]
        public readonly  string wlan_status;

        [JsonProperty("dl_frame_utilization")]
        public readonly  double? dl_frame_utilization;

        [JsonProperty("dl_mcs")]
        public readonly  int? dl_mcs;

        [JsonProperty("dl_modulation")]
        public readonly  string dl_modulation;

        [JsonProperty("dl_pkts")]
        public readonly  Int64? dl_pkts;

        [JsonProperty("dl_pkts_loss")]
        public readonly  Int64? dl_pkts_loss;

        [JsonProperty("dl_retransmits")]
        public readonly  int? dl_retransmits;

        [JsonProperty("dl_retransmits_pct")]
        public readonly  int? dl_retransmits_pct;

        [JsonProperty("dl_rssi")]
        public readonly  double? dl_rssi;

        [JsonProperty("dl_rssi_imbalance")]
        public readonly  double? dl_rssi_imbalance;

        [JsonProperty("dl_snr")]
        public readonly  int? dl_snr;

        [JsonProperty("dl_snr_v")]
        public readonly  int? dl_snr_v;

        [JsonProperty("dl_snr_h")]
        public readonly  int? dl_snr_h;

        [JsonProperty("dl_throughput")]
        public readonly  Int64? dl_throughput;

        [JsonProperty("frame_period")]
        public readonly  string frame_period;

        [JsonProperty("frequency")]
        public readonly  string frequency;

        [JsonProperty("mac")]
        public readonly  string mac;

        [JsonProperty("mode")]
        public readonly  string mode;

        [JsonProperty("sessions_dropped")]
        public readonly  string sessions_dropped;

        [JsonProperty("ssid")]
        public readonly  string ssid;

        [JsonProperty("sync_source")]
        public readonly  string sync_source;

        [JsonProperty("sync_state")]
        public readonly  string sync_state;

        [JsonProperty("tdd_ratio")]
        public readonly  string tdd_ratio;

        [JsonProperty("tx_capacity")]
        public readonly  string tx_capacity;

        [JsonProperty("tx_power")]
        public readonly  int? tx_power;

        [JsonProperty("tx_quality")]
        public readonly  string tx_quality;

        [JsonProperty("ul_frame_utilization")]
        public readonly  double? ul_frame_utilization;

        [JsonProperty("ul_mcs")]
        public readonly  int? ul_mcs;

        [JsonProperty("ul_modulation")]
        public readonly  string ul_modulation;

        [JsonProperty("ul_pkts")]
        public readonly  Int64? ul_pkts;

        [JsonProperty("ul_pkts_loss")]
        public readonly  Int64? ul_pkts_loss;

        [JsonProperty("ul_retransmits")]
        public readonly  int? ul_retransmits;

        [JsonProperty("ul_retransmits_pct")]
        public readonly  int? ul_retransmits_pct;

        [JsonProperty("ul_rssi")]
        public readonly  double? ul_rssi;

        [JsonProperty("ul_rssi_imbalance")]
        public readonly  double? ul_rssi_imbalance;

        [JsonProperty("ul_snr")]
        public readonly  int? ul_snr;

        [JsonProperty("ul_snr_v")]
        public readonly  int? ul_snr_v;

        [JsonProperty("ul_snr_h")]
        public readonly  int? ul_snr_h;

        [JsonProperty("ul_throughput")]
        public readonly  Int64? ul_throughput;
    }
}
