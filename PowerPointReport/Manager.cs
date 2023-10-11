using System;
using Spire.Presentation;
using System.Drawing;
using Spire.Presentation.Charts;
using System.Data;
using System.Collections.Generic;
using cnMaestroReporting.Domain;
using System.Linq;
using cnMaestroReporting.Reporting.PPTX.Entities;
using Spire.Presentation.Drawing;
using cnMaestroReporting.Prometheus.Entities;
using System.Globalization;
using static cnMaestroReporting.Prometheus.PrometheusExtension;
using static cnMaestroReporting.Prometheus.Utils.StringExtensions;
using static cnMaestroReporting.Prometheus.Utils.Bytes;

using System.Collections.Immutable;
using CnMaestroWebAPI = cnmWebApi;

namespace cnMaestroReporting.Output.PPTX
{
    public class Manager
    {
        private int days = 7;
        private const int GAP = 10;
        private const int HEADER_HEIGHT = 50;
        private const int SUBHEADER_HEIGHT = 40;
        private const int TOTAL_DOUBLE_HEADER_HEIGHT = GAP + HEADER_HEIGHT + GAP + SUBHEADER_HEIGHT + GAP;
        private const int TOTAL_SINGLE_HEADER_HEIGHT = GAP + HEADER_HEIGHT + GAP;
         
        private const int SLIDE_WIDTH = 1280;
        private const int SLIDE_HEIGHT = 720;
         
        private static Color COLOR_BRAND = Color.FromArgb(15, 134, 202);
        private static Color COLOR_WHITE = Color.White;
        private static Color COLOR_GREY = Color.FromArgb(97, 99, 103);
        private static Color[] THEMECOLORS = new[] { COLOR_BRAND, Color.FromArgb(253, 183, 27), Color.FromArgb(255, 71, 19) };
        private const string LowModulationBreakPoint = "128-QAM";
        public Manager(List<SubscriberRadioInfo> smInfo, IDictionary<ESN, AccessPointRadioInfo> apInfo, PromNetworkData promNetworkData, PromNetworkData promNetworkPrevious, IImmutableDictionary<string, Dictionary<DateOnly, CnMaestroWebAPI.DataUsage>> smGbOfDataUsage, int historyDays = 7)
        {
            Console.WriteLine("\nStarting PPTX Generation");

            days = historyDays;

            smInfo = smInfo.Where(sm => sm.Online == true).ToList(); // PPTX Only Works with Online SMs
            //apInfo = apInfo.Where(ap => !ap.Value.Name.Contains("PTP") && !ap.Value.Name.Contains("SCAN")).ToDictionary(ap => ap.Key, ap => ap.Value); // PPTX We don't want to report on SCAN APs or PTP Links

            var settings = (FileName: " ", Test: 0);
            double[] width7030 = { 0.70, 0.30 };
            double[] width502525 = { 0.50, 0.25, 0.25 };
            double[] width2818181818 = { 0.28, 0.18, 0.18, 0.18, 0.18 };

            // create a PowerPoint document
            Presentation presentation = new Presentation();
            presentation.SlideSize.Type = SlideSizeType.Custom;
            presentation.SlideSize.Size = new SizeF(SLIDE_WIDTH, SLIDE_HEIGHT);
            presentation.SlideSize.Orientation = SlideOrienation.Landscape;

            // SLIDE: SM Modulation Composition
            var dlModLatest = promNetworkData.SMDlMod.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP")).GroupBy(a => Int32.Parse(a.metric.modulation)).Select(x => new dlModInfo(intToMod(x.Key - 1), (float)x.Select(x => Math.Round(decimal.Parse(x.value[1]))).Sum())).OrderBy(x => modToInt(x.series));
            var dlModPrev = promNetworkPrevious.SMDlMod.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP")).GroupBy(a => Int32.Parse(a.metric.modulation)).Select(x => new dlModInfo(intToMod(x.Key - 1), (float)x.Select(x => Math.Round(decimal.Parse(x.value[1]))).Sum())).OrderBy(x => modToInt(x.series));
            var dlModChartData = dlModLatest.Join(dlModPrev, o => o.series, i => i.series, (a, b) => new fullModInfoHistory(a.series, a.Downlink, b.Downlink)).OrderBy(x => modToInt(x.series));

            var poorModDLLatest = promNetworkData.SMDlMod.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP")).Where(d => IsLowMod(Int32.Parse(d.metric.modulation) - 1)).Select(x => Math.Round(decimal.Parse(x.value[1]))).Sum();
            var poorModDLPrev = promNetworkPrevious.SMDlMod.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP")).Where(d => IsLowMod(Int32.Parse(d.metric.modulation) - 1)).Select(x => Math.Round(decimal.Parse(x.value[1]))).Sum();
            var promSmCount = promNetworkData.SMMaxCount.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP")).Sum(x => decimal.Parse(x.value[1]));
            var promPrevSmCount = promNetworkPrevious.SMMaxCount.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP")).Sum(x => decimal.Parse(x.value[1]));
            var fullFormat = String.Format("+#.{0};-#.{0}", new string('#', 0));

            AddOneChartSlide(presentation, ChartType.ColumnClustered, dlModChartData,
                "Downlink Modulation Overview",
                $"Current and Previous ({days} Days)",
                $"<html><body>" +
                $"<center><h1>Maximum Total SMs</h1>" +
                $"This Week: {promSmCount.ToString("N0")} ({(promSmCount - promPrevSmCount).ToString(fullFormat)})<br/>" +
                $"Last Week: {promPrevSmCount.ToString("N0")}<br/><br/>" +
                $"<h1>SMs with Poor Modulations</h1>" +
                $"This Week: {poorModDLLatest.ToString("N0")} ({(poorModDLLatest - poorModDLPrev).ToString(fullFormat)}) [{(poorModDLLatest / promSmCount).ToString("P1")}]<br/>" +
                $"Last Week: {poorModDLPrev.ToString("N0")} [{(poorModDLPrev / promPrevSmCount).ToString("P1")}]" +
                $"<br/></br><p><i>Note: Poor Modulation set to less than {LowModulationBreakPoint}</i></p></body></html>");

            var avgWeightedModulations = promNetworkData.SMDlMod.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP"))
               .GroupBy(prom => prom.metric.instance)
               .Select(ap =>
                   ap.Select(m => (m.metric.instance, m.metric.modulation, SMs: Decimal.Parse(m.value[1]), ScoreValue: (int.Parse(m.metric.modulation) * Decimal.Parse(m.value[1])))))
               .Select(ap =>
                   (Instance: ap.First().instance, SMs: Math.Round(ap.Sum(i => i.SMs)), WeightedModulation: ap.Sum(i => i.SMs) > 0 ? (ap.Sum(i => i.ScoreValue) / ap.Sum(i => i.SMs)) : 1)).ToArray();

            var avgWeightedModulationsPrevious = promNetworkPrevious.SMDlMod.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP"))
               .GroupBy(prom => prom.metric.instance)
               .Select(ap =>
                   ap.Select(m => (m.metric.instance, m.metric.modulation, SMs: Decimal.Parse(m.value[1]), ScoreValue: (int.Parse(m.metric.modulation) * Decimal.Parse(m.value[1])))))
               .Select(ap =>
                    (Instance: ap.First().instance, SMs: Math.Round(ap.Sum(i => i.SMs)), WeightedModulation: ap.Sum(i => i.SMs) > 0 ? (ap.Sum(i => i.ScoreValue) / ap.Sum(i => i.SMs)) : 1)).ToArray();


            // SLIDE: Worst SM Valuation
            var valueTable = promNetworkData.SMDlMod.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP")).GroupBy(x => x.metric.instance)
                .Select(ap => {
                    var apName = apInfo.lookupApNameByIp(ap.Key);
                    var poor = ap.Where(sm => IsLowMod(int.Parse(sm.metric.modulation) - 1)).Sum(sm => sm.value[1].DecimalStringToInt());
                    var all = ap.Sum(sm => sm.value[1].DecimalStringToInt());
                    var pct = all > 0 ? ((float)poor / all) : 0;
                    var value = smInfo.Where(ap => ap.APName == apName).Sum(sm => sm.EIPValue);
                    return new string[] {
                        apName,
                        $"{poor.ToString()}/{all.ToString()}",
                        intToMod((int)(Math.Floor(avgWeightedModulations.Where(x => x.Instance == ap.Key).FirstOrDefault().WeightedModulation)) - 1).ToString(),
                        (value * pct).ToString("C0"),
                        value.ToString("C0")
                    };
                }).OrderByDescending(x => Decimal.Parse(x[4], NumberStyles.Currency));

            var totalRisk = valueTable.Sum(x => Decimal.Parse(x[3], NumberStyles.Currency));
            var totalRev = valueTable.Sum(x => Decimal.Parse(x[4], NumberStyles.Currency));

            try
            {
                AddSlideTwoTables(presentation, "Highest Value Sectors w/Poor Modulation Impact",
                    new TableInfo(
                        $"Poor SMs: {poorModDLLatest.ToString("N0")} / Total SMs: {promSmCount.ToString("N0")} ({(poorModDLLatest / promSmCount).ToString("P0")})",
                        new string[] { "Sectors", $"Current <{LowModulationBreakPoint.Replace("-", "")}", "Current Avg Mod", "At Risk Revenue", "Monthly Revenue" },
                        width2818181818,
                        valueTable.Take(15)),
                    new TableInfo(
                        // $"Previous {days} Days: {poorModDLPrev.ToString("N0")}/{promPrevSmCount.ToString("N0")} ({(poorModDLPrev / promPrevSmCount).ToString("P0")})",
                        $"Poor SMs Revenue: {totalRisk.ToString("C0")} / Total: {totalRev.ToString("C0")} ({(totalRisk / totalRev).ToString("P0")})",
                        new string[] { "Sectors", $"Current <{LowModulationBreakPoint.Replace("-", "")}", "Current Avg Mod", "Avg Low Mod", "At Risk Revenue", "Monthly Revenue" },
                        width2818181818,
                        valueTable.Skip(15).Take(15))
                 );
            }
            catch { 
            
            }

            // SLIDE: Canopy Hardware Overview
            AddTwoChartSlide(presentation,
                "Network Canopy Hardware Overview",
                $"Total APs: {apInfo.Where(x => !x.Value.Name.Contains("PTP")).Count()}",
                ChartType.Column3DStacked,
                apInfo
                    .Where(x => !x.Value.Name.Contains("PTP"))
                    .GroupBy(x => x.Value.Hardware)
                    .Select(y => new freqCountInfo(y.Key, y.Where(x =>
                        x.Value.Channel > 3000 && x.Value.Channel < 5000).Count(), y.Where(x => x.Value.Channel > 5000).Count())),
                $"Total SMs: {smInfo.Count()}",
                ChartType.Column3DStacked,
                smInfo
                    .GroupBy(x => x.Model)
                    .Select(y => new freqCountInfo(y.Key, y.Where(x =>
                    {
                        var channel = apInfo.Where(ap => ap.Key == x.APEsn).FirstOrDefault().Value.Channel;
                        return channel > 3000 && channel < 5000;
                    }).Count(), y.Where(x => apInfo.Where(ap => ap.Key == x.APEsn).FirstOrDefault().Value.Channel > 5000).Count())));

            // SLIDE: Sectors with Poor SM Modulations
            var byMostBadSMs = promNetworkData.SMDlMod.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP")).GroupBy(x => x.metric.instance)
                .Select(ap => new string[] {
                     apInfo.lookupApNameByIp(ap.Key),
                     $"{ap.Where(sm => IsLowMod(int.Parse(sm.metric.modulation) - 1)).Sum(sm => sm.value[1].DecimalStringToInt()).ToString()} / " +
                     $"{ap.Sum(sm => sm.value[1].DecimalStringToInt()).ToString()}",
                     $"{promNetworkPrevious.SMDlMod.data.result.Where(old => old.metric.instance == ap.Key && IsLowMod(int.Parse(old.metric.modulation) - 1)).Sum(old => old.value[1].DecimalStringToInt()).ToString()} / " +
                     $"{promNetworkPrevious.SMDlMod.data.result.Where(old => old.metric.instance == ap.Key).Sum(old => old.value[1].DecimalStringToInt()).ToString()}",
                     intToMod((int)(Math.Floor(avgWeightedModulations.Where(x => x.Instance == ap.Key).FirstOrDefault().WeightedModulation)) - 1).ToString(),
                     intToMod((int)(Math.Floor(avgWeightedModulationsPrevious.Where(x => x.Instance == ap.Key).FirstOrDefault().WeightedModulation)) - 1).ToString()
                 }).OrderByDescending(x => int.Parse(x[1].Split("/")[0].Trim())).ToArray();

            var byWorstModulations = byMostBadSMs.Where(x => int.Parse(x[1].Split("/")[0].Trim()) > 1).OrderBy(x => modToInt(x[3]));

            AddSlideTwoTables(presentation, "Worst Modulation SM concentration by AP",
                new TableInfo(
                    $"Sectors with Highest # of Poor SMs",
                    new string[] { "Sectors", $"Current <{LowModulationBreakPoint.Replace("-","")}", $"Previous <{LowModulationBreakPoint.Replace("-", "")}", "Current Mod", "Previous Mod" },
                    width2818181818,
                    byMostBadSMs.Take(15)),
                new TableInfo(
                    $"Sectors with Worst Average Modulation",
                    new string[] { "Sectors", $"Current <{LowModulationBreakPoint.Replace("-", "")}", $"Previous <{LowModulationBreakPoint.Replace("-", "")}", "Current Mod", "Previous Mod" },
                    width2818181818,
                    byWorstModulations.Take(15))
             );

            


            var SectorDataUsageDl = promNetworkData.ApDl.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP")).OrderByDescending(x => decimal.Parse(x.value[1])).ToArray();
            var SectorDataUsageUl = promNetworkData.ApUl.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP")).OrderByDescending(x => decimal.Parse(x.value[1])).ToArray();
            decimal totalDL = SectorDataUsageDl.Sum((x) => decimal.Parse(x.value[1]));
            decimal totalUL = SectorDataUsageUl.Sum((x) => decimal.Parse(x.value[1]));
            decimal DL = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, totalDL, Utils.Bytes.Unit.Terabyte, 2);
            decimal UL = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, totalUL, Utils.Bytes.Unit.Terabyte, 2);

            var SectorDataUsageDlPrev = promNetworkPrevious.ApDl.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP")).OrderByDescending(x => decimal.Parse(x.value[1])).ToArray();
            var SectorDataUsageUlPrev = promNetworkPrevious.ApUl.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP")).OrderByDescending(x => decimal.Parse(x.value[1])).ToArray();
            decimal totalDLPrev = SectorDataUsageDlPrev.Sum((x) => decimal.Parse(x.value[1]));
            decimal totalULPrev = SectorDataUsageUlPrev.Sum((x) => decimal.Parse(x.value[1]));
            decimal DLPrev = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, totalDLPrev, Utils.Bytes.Unit.Terabyte, 2);
            decimal ULPrev = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, totalULPrev, Utils.Bytes.Unit.Terabyte, 2);

            var (pctApCount, pctApDlAmount, _) = SectorDataUsageDl.TopPercentage(0.75m, (ip) => true);
            var (pctApCountM, pctApDlAmountM, _) = SectorDataUsageDl.TopPercentage(0.75m, (ip) => apInfo.lookupApModelByIp(ip).Contains("450m"));
            var (pctApCountO, pctApDlAmountO, _) = SectorDataUsageDl.TopPercentage(0.75m, (ip) => !apInfo.lookupApModelByIp(ip).Contains("450m"));

            var SectorSMs = promNetworkData.SMMaxCount.data.result.Where(x => !(apInfo.lookupApNameByIp(x.metric.instance)).Contains("PTP")).OrderByDescending(x => double.Parse(x.value[1])).ToArray();
            var SectorSMTotal = SectorSMs.Sum(x => decimal.Parse(x.value[1]));
            var (dlPctAPSMs, dlPctAPSMsAmt, _) = SectorSMs.TopPercentage(0.75m, (ip) => true);
            var (dlPctAPSMsM, dlPctAPSMsAmtM, _) = SectorSMs.TopPercentage(0.75m, (ip) => apInfo.lookupApModelByIp(ip).Contains("450m"));
            var (dlPctAPSMsO, dlPctAPSMsAmtO, _) = SectorSMs.TopPercentage(0.75m, (ip) => !apInfo.lookupApModelByIp(ip).Contains("450m"));

            Console.WriteLine("Top 75% of APs (by SM Count)");
            Console.WriteLine($"Full Network: {dlPctAPSMsAmt}/{SectorSMTotal} ({(dlPctAPSMsAmt / SectorSMTotal).ToString("P1")}) - {dlPctAPSMs}/{SectorSMs.Count()} ({((float)dlPctAPSMs / SectorSMs.Count()).ToString("P1")})");
            Console.WriteLine($"450M APSMs: {dlPctAPSMsAmtM}/{SectorSMTotal} ({(dlPctAPSMsAmtM / SectorSMTotal).ToString("P1")}) - {dlPctAPSMsM}/{SectorSMs.Count()} ({((float)dlPctAPSMsM / SectorSMs.Count()).ToString("P1")})");
            Console.WriteLine($"Non-450M APSMs: {dlPctAPSMsAmtO}/{SectorSMTotal} ({(dlPctAPSMsAmtO / SectorSMTotal).ToString("P1")}) - {dlPctAPSMsO}/{SectorSMs.Count()} ({((float)dlPctAPSMsO / SectorSMs.Count()).ToString("P1")})");

            Console.WriteLine("\nTop 75% of APs (by Traffic)");
            Console.WriteLine($"Full Network: {pctApDlAmount.BytesToTerabytes()}TB/{totalDL.BytesToTerabytes()}TB - {pctApCount}/{SectorDataUsageDl.Count()} ({((float)pctApCount / SectorDataUsageDl.Count()).ToString("P1")})");
            Console.WriteLine($"450M APs: {pctApDlAmountM.BytesToTerabytes()}TB/{totalDL.BytesToTerabytes()}TB - {pctApCountM}/{SectorDataUsageDl.Count()} ({((float)pctApCountM / SectorDataUsageDl.Count()).ToString("P1")})");
            Console.WriteLine($"Non-450M APs: {pctApDlAmountO.BytesToTerabytes()}TB/{totalDL.BytesToTerabytes()}TB - {pctApCountO}/{SectorDataUsageDl.Count()} ({((float)pctApCountO / SectorDataUsageDl.Count()).ToString("P1")})");



            var APsByWorstAvgMod = promNetworkData.SMDlMod.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP"))
                .GroupBy(x => x.metric.instance)
                .OrderBy(x => avgWeightedModulations.Where(y => y.Instance == x.Key).FirstOrDefault().WeightedModulation)
                .Select(ap => ap.Key)
                .Take(20);

            var APsByMostTraffic = SectorDataUsageDl
                .Take(20);

            var APInfoByIP = apInfo.Values.ToDictionary(x => x.IP, x => x);

            Console.WriteLine("\nPoor SMs on the Lowest Avg Modulation Sectors");
            var total = APsByWorstAvgMod.Sum(apIP => {
                var apESN = APInfoByIP[apIP].Esn;
                var smESNs = smInfo.Where(x => x.APEsn == apESN).ToDictionary(x => x.Esn, x => x);
                double totalDl = 0;

                foreach(var (esn, sm) in smESNs)
                {
                    if (IsLowMod(sm.DlMod))
                    {   
                        var smData = smGbOfDataUsage[esn]
                            .Where(day => day.Key >= DateOnly.FromDateTime(DateTime.Now.AddDays(-7)))
                            .Sum(x => { return x.Value.Downlink; });
                        totalDl += smData;
                        Console.WriteLine($"{apInfo.lookupApNameByEsn(apESN)},{sm.Name},{sm.DlMod},{smData.ToString("N2")}GB");
                    }
                }

                return totalDl; 
            });
            Console.WriteLine($"Total: {total.ToString("N2")}GB");

            Console.WriteLine("\nPoor SMs on Highest Traffic Sectors");
            var total2 = APsByMostTraffic.Sum(apIP => {
                var apESN = APInfoByIP[apIP.metric.instance].Esn;
                var smESNs = smInfo.Where(x => x.APEsn == apESN).ToDictionary(x => x.Esn, x => x);
                double totalDl = 0;

                foreach (var (esn, sm) in smESNs)
                {
                    if (IsLowMod(sm.DlMod))
                    {
                        var smData = smGbOfDataUsage[esn]
                            .Where(day => day.Key >= DateOnly.FromDateTime(DateTime.Now.AddDays(-7)))
                            .Sum(x => { return x.Value.Downlink; });
                        totalDl += smData;
                        Console.WriteLine($"{apInfo.lookupApNameByEsn(apESN)},{sm.Name},{sm.DlMod},{smData.ToString("N2")}GB");
                    }
                }

                return totalDl;
            });
            Console.WriteLine($"Total: {total2.ToString("N2")}GB");

            // SLIDE: Sector Highest Data Usage
            AddSlideTwoTables(presentation, $"Sectors with Highest Data Usage ({days} days)",
                new TableInfo(
                    $"Downlink Total Current: {DL}TB / Last: {DLPrev}TB",
                    new string[] { "Sectors", "Current Terabytes", "Previous Terabytes", "Current Avg Mod", "Previous Avg Mod" },
                    width2818181818, 
                    SectorDataUsageDl.Select(current => new string[] { 
                        apInfo.lookupApNameByIp(current.metric.instance), 
                        Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(current.value[1]), Utils.Bytes.Unit.Terabyte, 2).ToString(),
                        Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(SectorDataUsageDlPrev.Where(prev => prev.metric.instance == current.metric.instance).FirstOrDefault().value[1]), Utils.Bytes.Unit.Terabyte, 2).ToString(),
                        intToMod((int)(Math.Floor(avgWeightedModulations.Where(x => x.Instance == current.metric.instance).FirstOrDefault().WeightedModulation)) - 1).ToString(),
                        intToMod((int)(Math.Floor(avgWeightedModulationsPrevious.Where(x => x.Instance == current.metric.instance).FirstOrDefault().WeightedModulation)) - 1).ToString()
                    })),
                new TableInfo(
                    $"Uplink Total Current: {UL}TB / Last: {ULPrev}TB", 
                    new string[] { "Sectors", "Current Terabytes", "Previous Terabytes", "Current Avg Mod", "Previous Avg Mod" },
                    width2818181818, 
                    SectorDataUsageUl.Select(current => new string[] { 
                        apInfo.lookupApNameByIp(current.metric.instance), 
                        Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(current.value[1]), Utils.Bytes.Unit.Terabyte, 2).ToString(),
                        Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(SectorDataUsageUlPrev.Where(prev => prev.metric.instance == current.metric.instance).FirstOrDefault().value[1]), Utils.Bytes.Unit.Terabyte, 2).ToString(),
                        intToMod((int)(Math.Floor(avgWeightedModulations.Where(x => x.Instance == current.metric.instance).FirstOrDefault().WeightedModulation)) - 1).ToString(),
                        intToMod((int)(Math.Floor(avgWeightedModulationsPrevious.Where(x => x.Instance == current.metric.instance).FirstOrDefault().WeightedModulation)) - 1).ToString()
                    })));

            // SLIDE: Sectors with Highest Peak Throughput
            var SectorDataTputDl = promNetworkData.ApDlTp.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP")).OrderByDescending(x => double.Parse(x.value[1])).ToArray();
            var SectorDataTputUl = promNetworkData.ApUlTp.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP")).OrderByDescending(x => double.Parse(x.value[1])).ToArray();
            var SectorDataTputDlPrev = promNetworkPrevious.ApDlTp.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP")).OrderByDescending(x => double.Parse(x.value[1])).ToArray();
            var SectorDataTputUlPrev = promNetworkPrevious.ApUlTp.data.result.Where(x => !apInfo.lookupApNameByIp(x.metric.instance).Contains("PTP")).OrderByDescending(x => double.Parse(x.value[1])).ToArray();
            AddSlideTwoTables(presentation, $"Sectors with Highest Peak Throughput ({days} days)",
                new TableInfo(
                    "Downlink",
                    //new string[] { "Sectors", "Current SMs", "Previous SMs", "Current Mbps", "Previous Mbps" },
                    new string[] { "Sectors", "Current Mbps", "Previous Mbps", "Current Avg Mod", "Previous Avg Mod" },
                    width2818181818,
                    SectorDataTputDl.Select(d => new string[] {
                        apInfo.lookupApNameByIp(d.metric.instance),
                        //promNetworkData.SMMaxCount.data.result.Where(x => x.metric.instance == d.metric.instance).FirstOrDefault().value[1],
                        //promNetworkPrevious.SMMaxCount.data.result.Where(x => x.metric.instance == d.metric.instance).FirstOrDefault().value[1],
                        Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(d.value[1]), Utils.Bytes.Unit.Megabyte, 0).ToString() + " Mbps",
                        Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(SectorDataTputDlPrev.Where(x => x.metric.instance == d.metric.instance).FirstOrDefault().value[1]), Utils.Bytes.Unit.Megabyte, 0).ToString() + " Mbps",
                        intToMod((int)(Math.Floor(avgWeightedModulations.Where(x => x.Instance == d.metric.instance).FirstOrDefault().WeightedModulation)) - 1).ToString(),
                        intToMod((int)(Math.Floor(avgWeightedModulationsPrevious.Where(x => x.Instance == d.metric.instance).FirstOrDefault().WeightedModulation)) - 1).ToString()
                    })),
                new TableInfo(
                    "Uplink",
                    //new string[] { "Sectors", "Current SMs", "Previous SMs", "Current Mbps", "Previous Mbps" },
                    new string[] { "Sectors", "Current Mbps", "Previous Mbps", "Current Avg Mod", "Previous Avg Mod" },
                    width2818181818,
                    SectorDataTputUl.Select(u => new string[] {
                        apInfo.lookupApNameByIp(u.metric.instance),
                        //promNetworkData.SMMaxCount.data.result.Where(x => x.metric.instance == u.metric.instance).FirstOrDefault().value[1],
                        //promNetworkPrevious.SMMaxCount.data.result.Where(x => x.metric.instance == u.metric.instance).FirstOrDefault().value[1],
                        Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(u.value[1]), Utils.Bytes.Unit.Megabyte, 0).ToString() + " Mbps",
                        Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(SectorDataTputUlPrev.Where(x => x.metric.instance == u.metric.instance).FirstOrDefault().value[1]), Utils.Bytes.Unit.Megabyte, 0).ToString() + " Mbps",
                        intToMod((int)(Math.Floor(avgWeightedModulations.Where(x => x.Instance == u.metric.instance).FirstOrDefault().WeightedModulation)) - 1).ToString(),
                        intToMod((int)(Math.Floor(avgWeightedModulationsPrevious.Where(x => x.Instance == u.metric.instance).FirstOrDefault().WeightedModulation)) - 1).ToString()
                    }))
                );

            // SLIDE: Outages on canopy network
            IEnumerable <(string Tower, int Downtime)> downtimes = apInfo.Values.Select(ap => (Tower: ap.Tower, Downtime: ap.Alarms.Sum(x => x.duration))).OrderBy(x => x.Downtime).DistinctBy(x => x.Tower).Where(x => x.Downtime > 60).Reverse();
            AddSlideTwoTables(presentation, $"Canopy Site Outages ({days} days)",
                new TableInfo("Total Canopy Downtime by Site", new string[] { "Site", "Duration" }, width7030, downtimes.Take(15).Select(x => new string[] { x.Tower, TimeSpan.FromSeconds(x.Downtime).ToString() })),
                new TableInfo("Total Canopy Downtime by Site", new string[] { "Site", "Duration" }, width7030, downtimes.Skip(15).Take(15).Select(x => new string[] { x.Tower, TimeSpan.FromSeconds(x.Downtime).ToString() })));

            // Compute filename
            string FileName = GenerateFileName(settings, days);

            presentation.Slides.RemoveAt(0); // Remove first slide since our subfunctions skip it.

            presentation.SaveToFile(FileName, FileFormat.Pptx2013);
            Console.WriteLine("\nPPTX Generation Completed");
        }

        private static string GenerateFileName((string FileName, int Test) settings, int days)
        {
            string FileName;
            if (String.IsNullOrWhiteSpace(settings.FileName))
                FileName = $"{DateTime.Now:yyyy-MM-dd} - Past {days}-Day Report.pptx";
            else
                FileName = settings.FileName;
            return FileName;
        }

        private static void AddSlideTwoTables(Presentation presentation, string slideTitle, TableInfo table1Info, TableInfo table2Info)
        {
            int columns = 2;
            float tableWidth = (presentation.SlideSize.Size.Width - ((columns + 1) * GAP)) / 2;

            ISlide slide = presentation.Slides.Append();

            AddSlideHeader(slide, slideTitle);

            AddSubHeader(slide, table1Info.title, GAP, tableWidth);
            AddTable(slide, table1Info, GAP, tableWidth);

            AddSubHeader(slide, table2Info.title, GAP + tableWidth + GAP, tableWidth);
            AddTable(slide, table2Info, GAP + tableWidth + GAP, tableWidth);
        }
                 
        private static void AddOneChartSlide(Presentation presentation, ChartType chartType, IEnumerable<ISeriesInfo> data, string slideTitle, string chartTitle, string descText)
        {
            ISlide slide = presentation.Slides.Append();
            float chartWidth = (float)((presentation.SlideSize.Size.Width - (GAP * 3)) * 0.65);
            float textWidth = (float)((presentation.SlideSize.Size.Width - (GAP * 3)) * 0.35);
            float chartHeight = presentation.SlideSize.Size.Height - (TOTAL_SINGLE_HEADER_HEIGHT + GAP /* TOP HEADER AREA + GAP */);

            RectangleF rectChart = new RectangleF(GAP, TOTAL_SINGLE_HEADER_HEIGHT, chartWidth, chartHeight);

            AddSlideHeader(slide, slideTitle);

            AddChart(slide, rectChart, chartTitle, chartType, data);

            RectangleF rectText = new RectangleF(GAP + chartWidth + GAP, TOTAL_SINGLE_HEADER_HEIGHT, textWidth, chartHeight);
            IAutoShape shape = slide.Shapes.AppendShape(ShapeType.Rectangle, rectText);
            shape.TextFrame.Paragraphs.Clear();
            shape.ShapeStyle.FillColor.Color = COLOR_GREY;
            shape.ShapeStyle.LineColor.Color = COLOR_GREY;

            shape.TextFrame.Paragraphs.AddFromHtml(descText);
            for (int i = 0; i < shape.TextFrame.Paragraphs.Count; i++)
            {
                shape.TextFrame.Paragraphs[i].TextRanges[0].Fill.SolidColor.Color = Color.White;
                shape.TextFrame.Paragraphs[i].Alignment = TextAlignmentType.Center;
            }
        }
                 
        private static void AddTwoChartSlide(Presentation presentation, string slideTitle, string chartTitle1, ChartType chartType1, IEnumerable<ISeriesInfo> data1, string chartTitle2, ChartType chartType2, IEnumerable<ISeriesInfo> data2)
        {
            ISlide slide = presentation.Slides.Append();
            float chartWidth = (float)((presentation.SlideSize.Size.Width - (GAP * 3)) * 0.50);
            float chartHeight = presentation.SlideSize.Size.Height - (TOTAL_SINGLE_HEADER_HEIGHT + GAP /* TOP HEADER AREA + GAP */);

            RectangleF rectChart = new RectangleF(GAP, TOTAL_SINGLE_HEADER_HEIGHT, chartWidth, chartHeight);
            RectangleF rectChart2 = new RectangleF(GAP + chartWidth + GAP, TOTAL_SINGLE_HEADER_HEIGHT, chartWidth, chartHeight);

            AddSlideHeader(slide, slideTitle);
            AddChart(slide, rectChart, chartTitle1, chartType1, data1);
            AddChart(slide, rectChart2, chartTitle2, chartType2, data2);
        }

        private static void AddSlideHeader(ISlide slide, string slideTitle)
        {
            IAutoShape header = slide.Shapes.AppendShape(ShapeType.Rectangle, new RectangleF(GAP, GAP, slide.Presentation.SlideSize.Size.Width - (GAP * 2), HEADER_HEIGHT));
            header.ShapeStyle.FillColor.Color = COLOR_BRAND;
            header.ShapeStyle.LineColor.Color = COLOR_BRAND;
            
            var paragraph = header.TextFrame.Paragraphs[0];
            paragraph.Text = slideTitle;
            paragraph.Alignment = TextAlignmentType.Center;
            paragraph.TextRanges[0].Fill.SolidColor.Color = COLOR_WHITE;
            paragraph.TextRanges[0].FontHeight = 32;
            paragraph.TextRanges[0].IsBold = TriState.True;
        }

        private static void AddSubHeader(ISlide slide, string title, float offset, float tableWidth)
        {
            IAutoShape header = slide.Shapes.AppendShape(ShapeType.Rectangle, new RectangleF(offset, TOTAL_SINGLE_HEADER_HEIGHT, tableWidth, SUBHEADER_HEIGHT));
            header.ShapeStyle.FillColor.Color = COLOR_GREY;
            header.ShapeStyle.LineColor.Color = COLOR_GREY;

            var paragraph = header.TextFrame.Paragraphs[0];
            paragraph.Text = title;
            paragraph.Alignment = TextAlignmentType.Center;
            paragraph.TextRanges[0].Fill.SolidColor.Color = COLOR_WHITE;
            paragraph.TextRanges[0].FontHeight = 24;
        }

        private static void AddChart(ISlide slide, RectangleF rect, string chartTitle, ChartType chartType, IEnumerable<ISeriesInfo> data)
        {
            if (data is null)
                return;

            var columns = data.FirstOrDefault().GetType().GetProperties()?.Where(p => p.Name != "series").Select(n => n.Name).ToArray();

            IChart chart = slide.Shapes.AppendChart(chartType, rect);
            
            // Chart Title
            chart.ChartTitle.TextProperties.Text = chartTitle;
            chart.ChartTitle.TextProperties.IsCentered = true;
            chart.ChartTitle.Height = 30;
            chart.HasTitle = true;
            chart.HasDataTable = true;
            chart.HasLegend = false;
            chart.ChartDataTable.ShowLegendKey = true;

            // Header Row
            for (var i = 0; i < columns.Count(); i++)
            {
                chart.ChartData[0, i + 1].Value = columns[i];
            }
            chart.Series.SeriesLabel = chart.ChartData[0, 1, 0, columns.Count()];

            // Data Rows
            var row = 1;
            var cols = 0;
            foreach (var mod in data)
            {
                cols = 0;

                // Category Label
                chart.ChartData[row, cols].Value = mod.series;

                // Category Values 
                cols++;
                foreach (var col in columns)
                {
                    chart.ChartData[row, cols].Value = mod.GetType().GetProperty(col).GetValue(mod);
                    cols++;
                }
                row++;
            }
            chart.Categories.CategoryLabels = chart.ChartData[1, 0, data.Count(), 0];

            for (int i = 0; i < columns.Count(); i++)
            {
                chart.Series[i].Values = chart.ChartData[row: 1, column: i + 1, lastRow: data.Count(), lastColumn: i + 1];
                chart.Series[i].Fill.FillType = FillFormatType.Solid;
                chart.Series[i].Fill.SolidColor.Color = THEMECOLORS[i];

                if (chartType == ChartType.ColumnClustered)
                {
                    chart.Series[i].DataLabels.SeriesNameVisible = false;
                    chart.Series[i].DataLabels.LegendKeyVisible = false;
                    chart.Series[i].DataLabels.LabelValueVisible = true;
                    chart.Series[i].DataLabels.Position = ChartDataLabelPosition.OutsideEnd;
                    chart.Series[i].DataLabels.TextProperties.Paragraphs[0].DefaultCharacterProperties.FontHeight = 16;
                }
            }

            //apply built-in chart style  
            chart.ChartStyle = ChartStyle.Style11;

            //set overlap  
            chart.OverLap = 0;

            //set gap width  
            chart.GapWidth = 50;
        }

        private static void AddTable(ISlide slide, TableInfo tableInfo, float offset, float tableWidth)
        {
            ITable table = slide.Shapes.AppendTable(offset, TOTAL_DOUBLE_HEADER_HEIGHT, tableInfo.columnWidths(tableWidth), Enumerable.Repeat((double)10, 15 + 1).ToArray());

            string[][] dataArray = tableInfo.columnData.ToArray();

            table.StylePreset = TableStylePreset.LightStyle1;

            int length = table.TableRows.Count < dataArray.Count() ? table.TableRows.Count : dataArray.Count() + 1;
            for (int row = 0; row < length; row++)
            {
                if (dataArray.Length == 0)
                {
                    continue;
                }

                for (int col = 0; col < dataArray[0].Length; col++)
                {
                    if (row == 0)
                    {
                        table[col, 0].TextFrame.Text = tableInfo.headers[col];
                    }
                    else
                    {
                        table[col, row].TextFrame.Text = dataArray[row - 1][col] ?? "";
                    }

                    table[col, row].TextFrame.Paragraphs[0].TextRanges[0].LatinFont = new TextFont("Arial Narrow");

                    // Center First Row and Non-First Column
                    if (col > 0 || row == 0)
                    {
                        table[col, row].TextFrame.Paragraphs[0].Alignment = TextAlignmentType.Center;
                    }
                }
            }
        }

        #region Modulation Functions
        static int modToInt(string mod)
        {
            return mod switch
            {
                "BPSK" => 0,
                "QPSK" => 1,
                "8-QAM" => 2,
                "16-QAM" => 3,
                "32-QAM" => 4,
                "64-QAM" => 5,
                "128-QAM" => 6,
                "256-QAM" => 7,
                _ => throw new ArgumentOutOfRangeException($"Invalid Modulation {mod}")
            };
        }

        static string intToMod(int mod)
        {
            return mod switch
            {
                -1 => "WEIRD",
                0 => "BPSK",
                1 => "QPSK",
                2 => "8-QAM",
                3 => "16-QAM",
                4 => "32-QAM",
                5 => "64-QAM",
                6 => "128-QAM",
                7 => "256-QAM",
                _ => throw new ArgumentOutOfRangeException($"Invalid Modulation {mod}")
            };
        }
        private static bool IsLowMod(string d)
        {
            return IsLowMod(modToInt(d));
        }
        private static bool IsLowMod(int d)
        {
            return d < modToInt(LowModulationBreakPoint);
        }
        #endregion
    }
}


