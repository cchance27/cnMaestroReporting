using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace cnMaestroReporting.cnMaestroAPI.cnDataType
{
    public struct CnPerformance : ICnMaestroDataType
    {
        [JsonProperty("mac")]
        public readonly string mac;

        [JsonProperty("managed_account")]
        public readonly string managed_account;

        [JsonProperty("mode")]
        public readonly string mode;

        [JsonProperty("name")]
        public readonly string name;

        [JsonProperty("network")]
        public readonly string network;

        [JsonProperty("online_duration")]
        public readonly int? online_duration;

        [JsonProperty("site")]
        public readonly string site;

        [JsonProperty("sm_count")]
        public readonly Dictionary<string, double> sm_count;

        [JsonProperty("sm_drops")]
        public readonly Dictionary<string, double> sm_drops;

        [JsonProperty("timestamp")]
        public readonly string timestamp;

        [JsonProperty("tower")]
        public readonly string tower;

        [JsonProperty("type")]
        public readonly string type;

        [JsonProperty("uptime")]
        public readonly string uptime;

        [JsonProperty("radio")]
        public readonly CnFixedRadioPerformance radio;
    }

    public class CnFixedRadioPerformance
    {
        [JsonProperty("dl_frame_utilization")]
        public readonly ReadOnlyDictionary<byte, double> dl_frame_utilization;

        [JsonProperty("dl_kbits")]
        public readonly ReadOnlyDictionary<byte, Int64?> dl_kbits;

        [JsonProperty("dl_mcs")]
        public readonly ReadOnlyDictionary<byte, int?> dl_mcs;

        [JsonProperty("dl_modulation")]
        public readonly ReadOnlyDictionary<byte, string> dl_modulation;

        [JsonProperty("dl_pkts")]
        public readonly ReadOnlyDictionary<byte, Int64?> dl_pkts;

        [JsonProperty("dl_pkts_loss")]
        public readonly ReadOnlyDictionary<byte, Int64?> dl_pkts_loss;

        [JsonProperty("dl_retransmits_pct")]
        public readonly ReadOnlyDictionary<byte, int?> dl_retransmits_pct;

        [JsonProperty("dl_rssi")]
        public readonly ReadOnlyDictionary<byte, double> dl_rssi;

        [JsonProperty("dl_rssi_imbalance")]
        public readonly ReadOnlyDictionary<byte, double> dl_rssi_imbalance;

        [JsonProperty("dl_snr")]
        public readonly ReadOnlyDictionary<byte, int?> dl_snr;

        [JsonProperty("dl_snr_v")]
        public readonly ReadOnlyDictionary<byte, int?> dl_snr_v;

        [JsonProperty("dl_snr_h")]
        public readonly ReadOnlyDictionary<byte, int?> dl_snr_h;

        [JsonProperty("dl_throughput")]
        public readonly ReadOnlyDictionary<byte, Int64?> dl_throughput;

        [JsonProperty("ul_frame_utilization")]
        public readonly ReadOnlyDictionary<byte, double?> ul_frame_utilization;

        [JsonProperty("ul_kbits")]
        public readonly ReadOnlyDictionary<byte, Int64?> ul_kbits;

        [JsonProperty("ul_mcs")]
        public readonly ReadOnlyDictionary<byte, int?> ul_mcs;

        [JsonProperty("ul_modulation")]
        public readonly ReadOnlyDictionary<byte, string> ul_modulation;

        [JsonProperty("ul_pkts")]
        public readonly ReadOnlyDictionary<byte, Int64?> ul_pkts;

        [JsonProperty("ul_pkts_loss")]
        public readonly ReadOnlyDictionary<byte, Int64?> ul_pkts_loss;

        [JsonProperty("ul_retransmits_pct")]
        public readonly ReadOnlyDictionary<byte, int?> ul_retransmits_pct;

        [JsonProperty("ul_rssi")]
        public readonly ReadOnlyDictionary<byte, double?> ul_rssi;

        [JsonProperty("ul_rssi_imbalance")]
        public readonly ReadOnlyDictionary<byte, double?> ul_rssi_imbalance;

        [JsonProperty("ul_snr")]
        public readonly ReadOnlyDictionary<byte, int?> ul_snr;

        [JsonProperty("ul_snr_v")]
        public readonly ReadOnlyDictionary<byte, int?> ul_snr_v;

        [JsonProperty("ul_snr_h")]
        public readonly ReadOnlyDictionary<byte, int?> ul_snr_h;

        [JsonProperty("ul_throughput")]
        public readonly ReadOnlyDictionary<byte, Int64?> ul_throughput;
    }
}