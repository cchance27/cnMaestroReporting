using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace cnMaestro.cnDataType
{
    public struct CnPerformance : ICnMaestroDataType
    {
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

        [JsonProperty("online_duration")]
        readonly int online_duration;

        [JsonProperty("site")]
        readonly string site;

        [JsonProperty("sm_count")]
        readonly Dictionary<string, float> sm_count;

        [JsonProperty("sm_drops")]
        readonly Dictionary<string, float> sm_drops;

        [JsonProperty("timestamp")]
        readonly string timestamp;

        [JsonProperty("tower")]
        readonly string tower;

        [JsonProperty("type")]
        readonly string type;

        [JsonProperty("uptime")]
        readonly string uptime;

        [JsonProperty("radio")]
        readonly CnFixedRadioPerformance radio;
    }

    public class CnFixedRadioPerformance
    {
        [JsonProperty("dl_frame_utilization")]
        readonly ReadOnlyDictionary<byte, float> dl_frame_utilization;

        [JsonProperty("dl_kbits")]
        readonly ReadOnlyDictionary<byte, Int64?> dl_kbits;

        [JsonProperty("dl_mcs")]
        readonly ReadOnlyDictionary<byte, int> dl_mcs;

        [JsonProperty("dl_modulation")]
        readonly ReadOnlyDictionary<byte, string> dl_modulation;

        [JsonProperty("dl_pkts")]
        readonly ReadOnlyDictionary<byte, Int64?> dl_pkts;

        [JsonProperty("dl_pkts_loss")]
        readonly ReadOnlyDictionary<byte, Int64?> dl_pkts_loss;

        [JsonProperty("dl_retransmits_pct")]
        readonly ReadOnlyDictionary<byte, int> dl_retransmits_pct;

        [JsonProperty("dl_rssi")]
        readonly ReadOnlyDictionary<byte, float> dl_rssi;

        [JsonProperty("dl_rssi_imbalance")]
        readonly ReadOnlyDictionary<byte, float> dl_rssi_imbalance;

        [JsonProperty("dl_snr")]
        readonly ReadOnlyDictionary<byte, int> dl_snr;

        [JsonProperty("dl_snr_v")]
        readonly ReadOnlyDictionary<byte, int> dl_snr_v;

        [JsonProperty("dl_snr_h")]
        readonly ReadOnlyDictionary<byte, int> dl_snr_h;

        [JsonProperty("dl_throughput")]
        readonly ReadOnlyDictionary<byte, Int64?> dl_throughput;

        [JsonProperty("ul_frame_utilization")]
        readonly ReadOnlyDictionary<byte, float> ul_frame_utilization;

        [JsonProperty("ul_kbits")]
        readonly ReadOnlyDictionary<byte, Int64?> ul_kbits;

        [JsonProperty("ul_mcs")]
        readonly ReadOnlyDictionary<byte, int> ul_mcs;

        [JsonProperty("ul_modulation")]
        readonly ReadOnlyDictionary<byte, string> ul_modulation;

        [JsonProperty("ul_pkts")]
        readonly ReadOnlyDictionary<byte, Int64?> ul_pkts;

        [JsonProperty("ul_pkts_loss")]
        readonly ReadOnlyDictionary<byte, Int64?> ul_pkts_loss;

        [JsonProperty("ul_retransmits_pct")]
        readonly ReadOnlyDictionary<byte, int> ul_retransmits_pct;

        [JsonProperty("ul_rssi")]
        readonly ReadOnlyDictionary<byte, float> ul_rssi;

        [JsonProperty("ul_rssi_imbalance")]
        readonly ReadOnlyDictionary<byte, float> ul_rssi_imbalance;

        [JsonProperty("ul_snr")]
        readonly ReadOnlyDictionary<byte, int> ul_snr;

        [JsonProperty("ul_snr_v")]
        readonly ReadOnlyDictionary<byte, int> ul_snr_v;

        [JsonProperty("ul_snr_h")]
        readonly ReadOnlyDictionary<byte, int> ul_snr_h;

        [JsonProperty("ul_throughput")]
        readonly ReadOnlyDictionary<byte, Int64?> ul_throughput;
    }
}