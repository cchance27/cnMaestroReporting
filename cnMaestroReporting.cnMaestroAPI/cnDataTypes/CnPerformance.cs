using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace cnMaestroReporting.cnMaestroAPI.cnDataType
{
    public record CnPerformance(string mac, string managed_account, string mode, string name, string network, int? online_duration, string site, Dictionary<string, double> sm_count, Dictionary<string, double> sm_drops, string timestamp, string tower, string type, string uptime, CnFixedRadioPerformance radio) : ICnMaestroDataType;
      
    public record CnFixedRadioPerformance(ReadOnlyDictionary<byte, double> dl_frame_utilization, ReadOnlyDictionary<byte, Int64?> dl_kbits, ReadOnlyDictionary<byte, int?> dl_mcs, ReadOnlyDictionary<byte, string> dl_modulation, ReadOnlyDictionary<byte, Int64?> dl_pkts, ReadOnlyDictionary<byte, Int64?> dl_pkts_loss, ReadOnlyDictionary<byte, int?> dl_retransmits_pct, ReadOnlyDictionary<byte, double> dl_rssi, ReadOnlyDictionary<byte, double> dl_rssi_imbalance, ReadOnlyDictionary<byte, int?> dl_snr, ReadOnlyDictionary<byte, int?> dl_snr_v, ReadOnlyDictionary<byte, int?> dl_snr_h, ReadOnlyDictionary<byte, Int64?> dl_throughput, ReadOnlyDictionary<byte, double?> ul_frame_utilization, ReadOnlyDictionary<byte, Int64?> ul_kbits, ReadOnlyDictionary<byte, int?> ul_mcs, ReadOnlyDictionary<byte, string> ul_modulation, ReadOnlyDictionary<byte, Int64?> ul_pkts, ReadOnlyDictionary<byte, Int64?> ul_pkts_loss, ReadOnlyDictionary<byte, int?> ul_retransmits_pct, ReadOnlyDictionary<byte, double?> ul_rssi, ReadOnlyDictionary<byte, double?> ul_rssi_imbalance, ReadOnlyDictionary<byte, int?> ul_snr, ReadOnlyDictionary<byte, int?> ul_snr_v, ReadOnlyDictionary<byte, int?> ul_snr_h, ReadOnlyDictionary<byte, Int64?> ul_throughput) : ICnMaestroDataType;
}