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
        public string Hardware { get; set; }
        public int Azimuth { get; set; }
        public int Downtilt { get; set; }
        public int Band { get; set; }
        public string Tower { get; set; }
        public int SMs { get; set; }
        public int AvgSmDistanceM { get; set; }
        public int MaxSmDistanceM { get; set; }
        public decimal DL30d { get; set; }
        public decimal UL30d { get; set; }
        public decimal DL7d { get; set; }
        public decimal UL7d { get; set; }
        public decimal DL1d { get; set; }
        public decimal UL1d { get; set; }
        public int AvgApPl { get; set; }
        public int WorstApPl { get; set; }
        public int AvgSmPl { get; set; }
        public int WorstSmPl { get; set; }
        public int AvgSmSnrH { get; set; }
        public int AvgSmSnrV { get; set; }
        public int WorstSmSnr { get; set; }
        public int AvgApSnrH { get; set; }
        public int AvgApSnrV { get; set; }
        public int WorstApSnr { get; set; }
        public double DlFramePctl { get; set; }
        public double DlTputPctl { get; set; }
        public double DlUsageAnalysis { get; set; }

        /// <summary>
        /// Takes in an IEnumeable of SubscriberRadioInfo and Returns an IEnumerable of AccessPointAverageInfo
        /// </summary>
        /// <param name="subData"></param>
        /// <returns></returns>
        static public IEnumerable<AccessPointAverageInfo> GenerateFromSMandAPData(IEnumerable<SubscriberRadioInfo> subData, IDictionary<ESN, AccessPointRadioInfo> apData, PromNetworkData promNetworkData)
        {
            var grouped = subData.GroupBy(sm => sm.APName);

            List<AccessPointAverageInfo> apAIs = new();

            //Prometheus.PromResult[] SectorDataUsageDl = promNetworkData.ApDl.data.result.OrderBy(x => double.Parse(x.value[1])).Reverse().ToArray();
            //Prometheus.PromResult[] SectorDataUsageUl = promNetworkData.ApUl.data.result.OrderBy(x => double.Parse(x.value[1])).Reverse().ToArray();
            //lookupApNameByIp(x.metric.instance, apInfo)
            //Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(x.value[1]), Utils.Bytes.Unit.Terabyte, 2).ToString() }

            foreach (var sub in grouped)
            {
                var apESN = sub.First().APEsn;
                var apIP = apData.Where(x => x.Value.Esn == apESN).Select(x => x.Value.IP).First();

                var count = promNetworkData.ApDl.data.result.Count(x => x.metric.instance == apIP);
                if (count > 0)
                    continue;

                Console.WriteLine($"Prometheus Missing: {apIP}");
            }


            foreach (var sub in grouped)
            {
                var apESN = sub.First().APEsn;
                var apIP = apData.Where(x => x.Value.Esn == apESN).Select(x => x.Value.IP).First();

                var promDataDL30 = promNetworkData.ApDl.data.result.Where(x => x.metric.instance == apIP).First();
                var promDataDL7 = promNetworkData.ApDl7Days.data.result.Where(x => x.metric.instance == apIP).First();
                var promDataDL1 = promNetworkData.ApDl24Hours.data.result.Where(x => x.metric.instance == apIP).First();
                var promDataUL30 = promNetworkData.ApUl.data.result.Where(x => x.metric.instance == apIP).First();
                var promDataUL7 = promNetworkData.ApUl7Days.data.result.Where(x => x.metric.instance == apIP).First();
                var promDataUL1 = promNetworkData.ApUl24Hours.data.result.Where(x => x.metric.instance == apIP).First();

                AccessPointAverageInfo apAI = new AccessPointAverageInfo
                {
                    ApName = sub.Key,
                    Hardware = apData[apESN].Hardware,
                    Azimuth = apData[apESN].Azimuth,
                    Downtilt = apData[apESN].Downtilt,
                    Tower = sub.First().Tower,
                    SMs = sub.Count(),
                    Band = apData[apESN].Channel > 3000 && apData[apESN].Channel < 4000 ? 3 : apData[apESN].Channel > 4000 ? 5 : 0,
                    DL30d = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(promDataDL30.value[1]), Utils.Bytes.Unit.Terabyte, 2),
                    DL7d = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(promDataDL7.value[1]), Utils.Bytes.Unit.Terabyte, 2),
                    DL1d = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(promDataDL1.value[1]), Utils.Bytes.Unit.Terabyte, 2),
                    UL30d = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(promDataUL30.value[1]), Utils.Bytes.Unit.Terabyte, 2),
                    UL7d = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(promDataUL7.value[1]), Utils.Bytes.Unit.Terabyte, 2),
                    UL1d = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(promDataUL1.value[1]), Utils.Bytes.Unit.Terabyte, 2),
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
                };

                var dlUtilization = apData[apESN].Statistics?.Select(s => s.DownlinkUtilization).ToArray();
                apAI.DlFramePctl = MathCalc.Percentile(dlUtilization, 0.80);
                apAI.DlTputPctl = MathCalc.Percentile(apData[apESN].Statistics?.Select(s => s.DownlinkThroughput).ToArray(), 0.80) / 1024;

                var usageByHours = MathCalc.BucketizeDouble(dlUtilization, new[] { 20.0, 40.0, 60.0, 80.0, 100.0 });

                apAI.DlUsageAnalysis = usageByHours.OrderByDescending(a => a.Count).First().Bucket;
                apAIs.Add(apAI);
            }

            return apAIs;
        }
    }


}