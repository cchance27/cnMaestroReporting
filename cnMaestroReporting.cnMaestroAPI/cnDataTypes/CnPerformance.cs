using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace cnMaestroReporting.cnMaestroAPI.cnDataType
{
    public struct CnPerformance : ICnMaestroDataType
    {
        [JsonPropertyName("mac")]
        public readonly string mac;

        [JsonPropertyName("managed_account")]
        public readonly string managed_account;

        [JsonPropertyName("mode")]
        public readonly string mode;

        [JsonPropertyName("name")]
        public readonly string name;

        [JsonPropertyName("network")]
        public readonly string network;

        [JsonPropertyName("online_duration")]
        public readonly int? online_duration;

        [JsonPropertyName("site")]
        public readonly string site;

        [JsonPropertyName("sm_count")]
        public readonly Dictionary<string, double> sm_count;

        [JsonPropertyName("sm_drops")]
        public readonly Dictionary<string, double> sm_drops;

        [JsonPropertyName("timestamp")]
        public readonly string timestamp;

        [JsonPropertyName("tower")]
        public readonly string tower;

        [JsonPropertyName("type")]
        public readonly string type;

        [JsonPropertyName("uptime")]
        public readonly string uptime;

        [JsonPropertyName("radio")]
        public readonly CnFixedRadioPerformance radio;
    }

    public class CnFixedRadioPerformance
    {
        [JsonPropertyName("dl_frame_utilization")]
        public readonly ReadOnlyDictionary<byte, double> dl_frame_utilization;

        [JsonPropertyName("dl_kbits")]
        public readonly ReadOnlyDictionary<byte, Int64?> dl_kbits;

        [JsonPropertyName("dl_mcs")]
        public readonly ReadOnlyDictionary<byte, int?> dl_mcs;

        [JsonPropertyName("dl_modulation")]
        public readonly ReadOnlyDictionary<byte, string> dl_modulation;

        [JsonPropertyName("dl_pkts")]
        public readonly ReadOnlyDictionary<byte, Int64?> dl_pkts;

        [JsonPropertyName("dl_pkts_loss")]
        public readonly ReadOnlyDictionary<byte, Int64?> dl_pkts_loss;

        [JsonPropertyName("dl_retransmits_pct")]
        public readonly ReadOnlyDictionary<byte, int?> dl_retransmits_pct;

        [JsonPropertyName("dl_rssi")]
        public readonly ReadOnlyDictionary<byte, double> dl_rssi;

        [JsonPropertyName("dl_rssi_imbalance")]
        public readonly ReadOnlyDictionary<byte, double> dl_rssi_imbalance;

        [JsonPropertyName("dl_snr")]
        public readonly ReadOnlyDictionary<byte, int?> dl_snr;

        [JsonPropertyName("dl_snr_v")]
        public readonly ReadOnlyDictionary<byte, int?> dl_snr_v;

        [JsonPropertyName("dl_snr_h")]
        public readonly ReadOnlyDictionary<byte, int?> dl_snr_h;

        [JsonPropertyName("dl_throughput")]
        public readonly ReadOnlyDictionary<byte, Int64?> dl_throughput;

        [JsonPropertyName("ul_frame_utilization")]
        public readonly ReadOnlyDictionary<byte, double?> ul_frame_utilization;

        [JsonPropertyName("ul_kbits")]
        public readonly ReadOnlyDictionary<byte, Int64?> ul_kbits;

        [JsonPropertyName("ul_mcs")]
        public readonly ReadOnlyDictionary<byte, int?> ul_mcs;

        [JsonPropertyName("ul_modulation")]
        public readonly ReadOnlyDictionary<byte, string> ul_modulation;

        [JsonPropertyName("ul_pkts")]
        public readonly ReadOnlyDictionary<byte, Int64?> ul_pkts;

        [JsonPropertyName("ul_pkts_loss")]
        public readonly ReadOnlyDictionary<byte, Int64?> ul_pkts_loss;

        [JsonPropertyName("ul_retransmits_pct")]
        public readonly ReadOnlyDictionary<byte, int?> ul_retransmits_pct;

        [JsonPropertyName("ul_rssi")]
        public readonly ReadOnlyDictionary<byte, double?> ul_rssi;

        [JsonPropertyName("ul_rssi_imbalance")]
        public readonly ReadOnlyDictionary<byte, double?> ul_rssi_imbalance;

        [JsonPropertyName("ul_snr")]
        public readonly ReadOnlyDictionary<byte, int?> ul_snr;

        [JsonPropertyName("ul_snr_v")]
        public readonly ReadOnlyDictionary<byte, int?> ul_snr_v;

        [JsonPropertyName("ul_snr_h")]
        public readonly ReadOnlyDictionary<byte, int?> ul_snr_h;

        [JsonPropertyName("ul_throughput")]
        public readonly ReadOnlyDictionary<byte, Int64?> ul_throughput;
    }
}