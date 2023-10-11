using cnMaestroAPI.cnDataType;
using cnMaestroReporting.Prometheus;
using cnMaestroReporting.Prometheus.Entities;
using CommonCalculations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace cnMaestroReporting.Domain
{
    public struct AccessPointAverageInfo
    {
        public string ApName { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string Hardware { get; set; }
        public int Azimuth { get; set; }
        public int Downtilt { get; set; }
        public double Channel { get; set; }
        public int Band { get; set; }
        public string Tower { get; set; }
        public int SMs { get; set; }
        public int ColorCode { get; set; }
        public int AvgSmDistanceM { get; set; }
        public int MaxSmDistanceM { get; set; }
        public decimal DL { get; set; }
        public decimal UL { get; set; }
        public int AvgApPl { get; set; }
        public int WorstApPl { get; set; }
        public decimal AvgGroupSize { get; set; }
        public decimal AvgMplxGain { get; set; }
        public int AvgSmPl { get; set; }
        public int WorstSmPl { get; set; }
        public int AvgSmSnrH { get; set; }
        public int AvgSmSnrV { get; set; }
        public int WorstSmSnr { get; set; }
        public int AvgApSnrH { get; set; }
        public int AvgApSnrV { get; set; }
        public int WorstApSnr { get; set; }
        public double DlEfficiencyMax { get; set; }
        public double DlEfficiencyMean { get; set; }
        public double DlTputMax { get; set; }
        public double DlUtilMean { get; set; }
        public double DlBusyHoursPct { get; set; }
        public double DlFramePctl { get; set; }
        public double DlTputPctl { get; set; }
        public double DlUsageAnalysis { get; set; }

        /// <summary>
        /// Takes in an IEnumeable of SubscriberRadioInfo and Returns an IEnumerable of AccessPointAverageInfo
        /// </summary>
        /// <param name="subData"></param>
        /// <returns></returns>
        static public IEnumerable<AccessPointAverageInfo> GenerateFromSMandAPData(IEnumerable<SubscriberRadioInfo> subData, IDictionary<ESN, AccessPointRadioInfo> apData, PromNetworkData promNetworkData, IEnumerable<KeyValuePair<string, CnLocation>> allTowers)
        {
            var grouped = subData.GroupBy(sm => sm.APName);

            List<AccessPointAverageInfo> apAIs = new();

            //Prometheus.PromResult[] SectorDataUsageDl = promNetworkData.ApDl.data.result.OrderBy(x => double.Parse(x.value[1])).Reverse().ToArray();
            //Prometheus.PromResult[] SectorDataUsageUl = promNetworkData.ApUl.data.result.OrderBy(x => double.Parse(x.value[1])).Reverse().ToArray();
            //lookupApNameByIp(x.metric.instance, apInfo)
            //Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(x.value[1]), Utils.Bytes.Unit.Terabyte, 2).ToString() }

            //foreach (var sub in grouped)
            //{
            //    var apESN = sub.First().APEsn;
            //    var apIP = apData.Where(x => x.Value.Esn == apESN).Select(x => x.Value.IP).FirstOrDefault();
            //
            //    if (apIP is not null || apIP != "")
            //    {
            //        // Our Sub's AP ESN has an IP from our AP Data
            //        var count = promNetworkData.ApDl.data.result.Count(x => x.metric.instance == apIP);
            //        if (count > 0)
            //        {
            //            // Prometheus has data for this AP
            //            continue;
            //        }
            //        Console.WriteLine($"Prometheus Missing: {apIP}");
            //    }
            //}


            foreach (var sub in grouped)
            {
                var apESN = sub.First().APEsn;
                var apIP = apData.Where(x => x.Value.Esn == apESN).Select(x => x.Value.IP).FirstOrDefault();

                if (apIP is not null && apIP != "")
                {
                    var apPromCount = promNetworkData.ApDl.data.result.Where(x => x.metric.instance == apIP).Count();
                    if (apPromCount == 0)
                    {
                        Console.WriteLine($"Prometheus Missing: {apIP}");
                        //continue;
                    }

                    var promDataDL = promNetworkData.ApDl.data.result.Where(x => x.metric.instance == apIP).FirstOrDefault();
                    var promDataUL = promNetworkData.ApUl.data.result.Where(x => x.metric.instance == apIP).FirstOrDefault();

                    var promGrp = promNetworkData.ApAvgGrp.data.result.Where(x => x.metric.instance == apIP).FirstOrDefault();
                    var promMplxGain = promNetworkData.ApMPGain.data.result.Where(x => x.metric.instance == apIP).FirstOrDefault();

                    var Location = allTowers.Where(x => x.Key == apData[apESN].Tower).FirstOrDefault().Value.coordinates.ToArray();

                    if (Location.Count() < 2) Location = new decimal[] { 0m, 0m };
                    AccessPointAverageInfo apAI = new AccessPointAverageInfo
                    {
                        ApName = sub.Key,
                        Latitude = Location[1].ToString(),
                        Longitude = Location[0].ToString(),
                        Hardware = apData[apESN].Hardware,
                        Azimuth = apData[apESN].Azimuth,
                        Downtilt = apData[apESN].Downtilt,
                        ColorCode = apData[apESN].ColorCode,
                        Tower = sub.First().Tower,
                        SMs = sub.Count(),
                        Channel = apData[apESN].Channel,
                        Band = apData[apESN].Channel > 3000 && apData[apESN].Channel < 4000 ? 3 : apData[apESN].Channel > 4000 ? 5 : 0,
                        DL = Prometheus.Utils.Bytes.FromTo(Prometheus.Utils.Bytes.Unit.Byte, apPromCount == 0 ? 0 : decimal.Parse(promDataDL.value[1]), Prometheus.Utils.Bytes.Unit.Terabyte, 2),
                        UL = Prometheus.Utils.Bytes.FromTo(Prometheus.Utils.Bytes.Unit.Byte, apPromCount == 0 ? 0 : decimal.Parse(promDataUL.value[1]), Prometheus.Utils.Bytes.Unit.Terabyte, 2),
                        AvgGroupSize = promGrp is not null && promGrp.value.Count() > 0 ? decimal.Parse(promGrp.value[1]) : 0,
                        AvgMplxGain = promMplxGain is not null && promMplxGain.value.Count() > 0 ? decimal.Parse(promMplxGain.value[1]) : 0,
                        AvgSmDistanceM = (int)sub.Where(sel => sel.DistanceM != 0).DefaultIfEmpty().Average(sel => sel.DistanceM),
                        MaxSmDistanceM = (int)sub.Where(sel => sel.DistanceM != 0).DefaultIfEmpty().Max(sel => sel.DistanceM),
                        AvgApPl = (int)sub.Where(sel => sel.ApAPL != 0).DefaultIfEmpty().Average(sel => sel.ApAPL),
                        WorstApPl = (int)sub.Where(sel => sel.ApAPL != 0).DefaultIfEmpty().Min(sel => sel.ApAPL),
                        AvgSmPl = (int)sub.Where(sel => sel.SmAPL != 0).DefaultIfEmpty().Average(sel => sel.SmAPL),
                        WorstSmPl = (int)sub.Where(sel => sel.SmAPL != 0).DefaultIfEmpty().Min(sel => sel.SmAPL),
                        AvgSmSnrH = (int)sub.Where(sel => sel.SmSNRH != 0).DefaultIfEmpty().Average(sel => sel.SmSNRH),
                        AvgSmSnrV = (int)sub.Where(sel => sel.SmSNRV != 0).DefaultIfEmpty().Average(sel => sel.SmSNRV),
                        WorstSmSnr = (int)sub.Where(sel => sel.SmSNRH != 0 && sel.SmSNRV != 0).Select(sel => sel.SmSNRV < sel.SmSNRH ? sel.SmSNRV : sel.SmSNRH).DefaultIfEmpty().Min(),
                        AvgApSnrH = (int)sub.Where(sel => sel.ApSNRH != 0).DefaultIfEmpty().Average(sel => sel.ApSNRH),
                        AvgApSnrV = (int)sub.Where(sel => sel.ApSNRV != 0).DefaultIfEmpty().Average(sel => sel.ApSNRV),
                        WorstApSnr = (int)sub.Where(sel => sel.ApSNRH != 0 && sel.ApSNRV != 0).Select(sel => sel.ApSNRV < sel.ApSNRH ? sel.ApSNRV : sel.ApSNRH).DefaultIfEmpty().Min(),
                        DlEfficiencyMax = apData[apESN]?.Statistics?.Count() > 0 ? apData[apESN].Statistics.MaxBy(x => x.BitzPerHzDownlink(20, 80))?.BitzPerHzDownlink(20, 80) ?? 0 : 0,
                        DlEfficiencyMean = apData[apESN]?.Statistics?.Count() > 0 ? apData[apESN].Statistics.Average(x => x.BitzPerHzDownlink(20, 80)) : 0,
                        DlTputMax = apData[apESN]?.Statistics?.Count() > 0 ? apData[apESN].Statistics.MaxBy(x => x.DownlinkThroughput)?.DownlinkThroughput / 1000  ?? 0 : 0,
                        DlUtilMean = apData[apESN]?.Statistics?.Count() > 0 ? apData[apESN].Statistics.Average(x => x.DownlinkUtilization) : 0,
                        DlBusyHoursPct = apData[apESN]?.Statistics?.Count() > 0 ? (double)apData[apESN].Statistics.Where(x => x.DownlinkUtilization > 85).Count() / (double)apData[apESN].Statistics.Count() : 0
                    };

                    if (apData[apESN]?.Statistics?.Count() > 0) { 
                        var dlUtilization = apData[apESN].Statistics?.Select(s => s.DownlinkUtilization).ToArray();
                        apAI.DlFramePctl = MathCalc.Percentile(dlUtilization, 0.80);
                        apAI.DlTputPctl = MathCalc.Percentile(apData[apESN].Statistics?.Select(s => s.DownlinkThroughput).ToArray(), 0.80) / 1024;

                        var usageByHours = MathCalc.BucketizeDouble(dlUtilization, new[] { 20.0, 40.0, 60.0, 80.0, 100.0 });

                        apAI.DlUsageAnalysis = usageByHours?.OrderByDescending(a => a.Count)?.FirstOrDefault()?.Bucket ?? 0;
                    } 
                    else
                    {
                        apAI.DlFramePctl = 0;
                        apAI.DlTputPctl = 0;
                        apAI.DlUsageAnalysis = 0;
                    }
                    apAIs.Add(apAI);
                }
            }

            return apAIs;
        }
    }
}