using System;
using Spire.Presentation;
using System.Drawing;
using Spire.Presentation.Charts;
using System.Data;
using System.Collections.Generic;
using cnMaestroReporting.Domain;
using System.Linq;
using MemoizeRedis;
using cnMaestroReporting.Reporting.PPTX.Entities;
using Spire.Presentation.Drawing;
using cnMaestroReporting.Prometheus.Entities;
using Utils;

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
        public Manager(List<SubscriberRadioInfo> smInfo, IDictionary<ESN, AccessPointRadioInfo> apInfo, PromNetworkData promNetworkData, PromNetworkData promNetworkPrevious, int historyDays = 7)
        {
            Console.WriteLine("\nStarting PPTX Generation");

            days = historyDays;

            smInfo = smInfo.Where(sm => sm.Online == true).ToList(); // PPTX Only Works with Online SMs
            //apInfo = apInfo.Where(ap => !ap.Value.Name.Contains("PTP") && !ap.Value.Name.Contains("SCAN")).ToDictionary(ap => ap.Key, ap => ap.Value); // PPTX We don't want to report on SCAN APs or PTP Links

            var settings = (FileName: " ", Test: 0);
            double[] width7030 = { 0.70, 0.30 };
            double[] width502525 = { 0.50, 0.25, 0.25 };
            double[] width3217171717 = { 0.32, 0.17, 0.17, 0.17, 0.17 };

            // create a PowerPoint document
            Presentation presentation = new Presentation();
            presentation.SlideSize.Type = SlideSizeType.Custom;
            presentation.SlideSize.Size = new SizeF(SLIDE_WIDTH, SLIDE_HEIGHT);
            presentation.SlideSize.Orientation = SlideOrienation.Landscape;

            // SLIDE: SM Modulation Composition
            var dlModLatest = promNetworkData.SMDlMod.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).GroupBy(a => Int32.Parse(a.metric.modulation)).Select(x => new dlModInfo(intToMod(x.Key - 1), (float)x.Select(x => Math.Round(decimal.Parse(x.value[1]))).Sum())).OrderBy(x => modToInt(x.series));
            var dlModPrev = promNetworkPrevious.SMDlMod.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).GroupBy(a => Int32.Parse(a.metric.modulation)).Select(x => new dlModInfo(intToMod(x.Key - 1), (float)x.Select(x => Math.Round(decimal.Parse(x.value[1]))).Sum())).OrderBy(x => modToInt(x.series));
            var dlModChartData = dlModLatest.Join(dlModPrev, o => o.series, i => i.series, (a, b) => new fullModInfoHistory(a.series, a.Downlink, b.Downlink)).OrderBy(x => modToInt(x.series));

            var poorModDLLatest = promNetworkData.SMDlMod.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).Where(d => IsLowMod(Int32.Parse(d.metric.modulation) - 1)).Select(x => Math.Round(decimal.Parse(x.value[1]))).Sum();
            var poorModDLPrev = promNetworkPrevious.SMDlMod.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).Where(d => IsLowMod(Int32.Parse(d.metric.modulation) - 1)).Select(x => Math.Round(decimal.Parse(x.value[1]))).Sum();
            var promSmCount = promNetworkData.SMMaxCount.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).Sum(x => decimal.Parse(x.value[1]));
            var promPrevSmCount = promNetworkPrevious.SMMaxCount.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).Sum(x => decimal.Parse(x.value[1]));
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

            // SLIDE: Sectors with Poor SM Modulations

            var avgWeightedModulations = promNetworkData.SMDlMod.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP")))
                .GroupBy(prom => prom.metric.instance)
                .Select(ap =>
                    ap.Select(m => (m.metric.instance, m.metric.modulation, SMs: Decimal.Parse(m.value[1]), ScoreValue: (int.Parse(m.metric.modulation) * Decimal.Parse(m.value[1])))))
                .Select(ap =>
                    (Instance: ap.First().instance, SMs: Math.Round(ap.Sum(i => i.SMs)), WeightedModulation: ap.Sum(i => i.SMs) > 0 ? (ap.Sum(i => i.ScoreValue) / ap.Sum(i => i.SMs)) : 1)).ToArray();

            var avgWeightedModulationsPrevious = promNetworkPrevious.SMDlMod.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP")))
               .GroupBy(prom => prom.metric.instance)
               .Select(ap =>
                   ap.Select(m => (m.metric.instance, m.metric.modulation, SMs: Decimal.Parse(m.value[1]), ScoreValue: (int.Parse(m.metric.modulation) * Decimal.Parse(m.value[1])))))
               .Select(ap =>
                    (Instance: ap.First().instance, SMs: Math.Round(ap.Sum(i => i.SMs)), WeightedModulation: ap.Sum(i => i.SMs) > 0 ? (ap.Sum(i => i.ScoreValue) / ap.Sum(i => i.SMs)) : 1)).ToArray();

            var tableValues = promNetworkData.SMDlMod.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).GroupBy(x => x.metric.instance)
                .Select(ap => new string[] {
                     lookupApNameByIp(ap.Key, apInfo),
                     $"{ap.Where(sm => IsLowMod(int.Parse(sm.metric.modulation) - 1)).Sum(sm => sm.value[1].DecimalStringToInt()).ToString()} / " +
                     $"{ap.Sum(sm => sm.value[1].DecimalStringToInt()).ToString()}",
                     $"{promNetworkPrevious.SMDlMod.data.result.Where(old => old.metric.instance == ap.Key && IsLowMod(int.Parse(old.metric.modulation) - 1)).Sum(old => old.value[1].DecimalStringToInt()).ToString()} / " +
                     $"{promNetworkPrevious.SMDlMod.data.result.Where(old => old.metric.instance == ap.Key).Sum(old => old.value[1].DecimalStringToInt()).ToString()}",
                     intToMod((int)(Math.Floor(avgWeightedModulations.Where(x => x.Instance == ap.Key).FirstOrDefault().WeightedModulation)) - 1).ToString(),
                     intToMod((int)(Math.Floor(avgWeightedModulationsPrevious.Where(x => x.Instance == ap.Key).FirstOrDefault().WeightedModulation)) - 1).ToString()
                 }).OrderByDescending(x => int.Parse(x[1].Split("/")[0].Trim())).ToArray();

            AddSlideTwoTables(presentation, "Sectors with Highest # of Poor Modulation SMs",
                new TableInfo(
                    $"Poor Downlink Modulation Sectors (Poor / Total)",
                    new string[] { "Sectors", $"Current <{LowModulationBreakPoint.Replace("-","")}", $"Previous <{LowModulationBreakPoint.Replace("-", "")}", "Current Mod", "Previous Mod" },
                    width3217171717,
                    tableValues.Take(15)),
                new TableInfo(
                    $"This Week: {poorModDLLatest.ToString("N0")} / Last Week: {poorModDLPrev.ToString("N0")}",
                    new string[] { "Sectors", $"Current <{LowModulationBreakPoint.Replace("-", "")}", $"Previous <{LowModulationBreakPoint.Replace("-", "")}", "Current Mod", "Previous Mod" },
                    width3217171717,
                    tableValues.Skip(15).Take(15))
             );



            // SLIDE: Canopy Hardware Overview
            AddTwoChartSlide(presentation,
                "Network Canopy Hardware Overview",
                $"Total APs: { apInfo.Where(x => !x.Value.Name.Contains("PTP")).Count()}", 
                ChartType.Column3DStacked, 
                apInfo
                    .Where(x => !x.Value.Name.Contains("PTP"))
                    .GroupBy(x => x.Value.Hardware)
                    .Select(y => new freqCountInfo(y.Key, y.Where(x => 
                        x.Value.Channel > 3000 && x.Value.Channel < 5000).Count(), y.Where(x => x.Value.Channel > 5000).Count())),
                $"Total SMs: { smInfo.Count()}", 
                ChartType.Column3DStacked, 
                smInfo
                    .GroupBy(x => x.Model)
                    .Select(y => new freqCountInfo(y.Key, y.Where(x =>
                {
                    var channel = apInfo.Where(ap => ap.Key == x.APEsn).FirstOrDefault().Value.Channel;
                    return channel > 3000 && channel < 5000;
                }).Count(), y.Where(x => apInfo.Where(ap => ap.Key == x.APEsn).FirstOrDefault().Value.Channel > 5000).Count())));

            // SLIDE: Sector Highest Data Usage
            decimal totalDL = promNetworkData.ApDl.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).Sum((x) => decimal.Parse(x.value[1]));
            decimal totalUL = promNetworkData.ApUl.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).Sum((x) => decimal.Parse(x.value[1]));
            decimal DL = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, totalDL, Utils.Bytes.Unit.Terabyte, 2);
            decimal UL = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, totalUL, Utils.Bytes.Unit.Terabyte, 2);
            Prometheus.PromResult[] SectorDataUsageDl = promNetworkData.ApDl.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).OrderBy(x => double.Parse(x.value[1])).Reverse().ToArray();
            Prometheus.PromResult[] SectorDataUsageUl = promNetworkData.ApUl.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).OrderBy(x => double.Parse(x.value[1])).Reverse().ToArray();

            decimal totalDLPrev = promNetworkPrevious.ApDl.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).Sum((x) => decimal.Parse(x.value[1]));
            decimal totalULPrev = promNetworkPrevious.ApUl.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).Sum((x) => decimal.Parse(x.value[1]));
            decimal DLPrev = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, totalDLPrev, Utils.Bytes.Unit.Terabyte, 2);
            decimal ULPrev = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, totalULPrev, Utils.Bytes.Unit.Terabyte, 2);
            Prometheus.PromResult[] SectorDataUsageDlPrev = promNetworkPrevious.ApDl.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).OrderBy(x => double.Parse(x.value[1])).Reverse().ToArray();
            Prometheus.PromResult[] SectorDataUsageUlPrev = promNetworkPrevious.ApUl.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).OrderBy(x => double.Parse(x.value[1])).Reverse().ToArray();

            AddSlideTwoTables(presentation, $"Sectors with Highest Data Usage ({days} days)",
                new TableInfo(
                    $"Downlink Total Current: {DL}TB / Last: {DLPrev}TB",
                    new string[] { "Sectors", "Current Terabytes", "Previous Terabytes", "Last Avg Modulation", "Prev Avg Modulation" },
                    width3217171717, 
                    SectorDataUsageDl.Select(current => new string[] { 
                        lookupApNameByIp(current.metric.instance, apInfo), 
                        Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(current.value[1]), Utils.Bytes.Unit.Terabyte, 2).ToString(),
                        Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(SectorDataUsageDlPrev.Where(prev => prev.metric.instance == current.metric.instance).FirstOrDefault().value[1]), Utils.Bytes.Unit.Terabyte, 2).ToString(),
                        intToMod((int)(Math.Floor(avgWeightedModulations.Where(x => x.Instance == current.metric.instance).FirstOrDefault().WeightedModulation)) - 1).ToString(),
                        intToMod((int)(Math.Floor(avgWeightedModulationsPrevious.Where(x => x.Instance == current.metric.instance).FirstOrDefault().WeightedModulation)) - 1).ToString()
                    })),
                new TableInfo(
                    $"Uplink Total Current: {UL}TB / Last: {ULPrev}TB", 
                    new string[] { "Sectors", "Current Terabytes", "Previous Terabytes", "Last Avg Modulation", "Prev Avg Modulation" },
                    width3217171717, 
                    SectorDataUsageUl.Select(current => new string[] { 
                        lookupApNameByIp(current.metric.instance, apInfo), 
                        Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(current.value[1]), Utils.Bytes.Unit.Terabyte, 2).ToString(),
                        Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(SectorDataUsageUlPrev.Where(prev => prev.metric.instance == current.metric.instance).FirstOrDefault().value[1]), Utils.Bytes.Unit.Terabyte, 2).ToString(),
                        intToMod((int)(Math.Floor(avgWeightedModulations.Where(x => x.Instance == current.metric.instance).FirstOrDefault().WeightedModulation)) - 1).ToString(),
                        intToMod((int)(Math.Floor(avgWeightedModulationsPrevious.Where(x => x.Instance == current.metric.instance).FirstOrDefault().WeightedModulation)) - 1).ToString()
                    })));

            // SLIDE: Sectors with Highest Peak Throughput
            Prometheus.PromResult[] SectorDataTputDl = promNetworkData.ApDlTp.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).OrderByDescending(x => double.Parse(x.value[1])).ToArray();
            Prometheus.PromResult[] SectorDataTputUl = promNetworkData.ApUlTp.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).OrderByDescending(x => double.Parse(x.value[1])).ToArray();
            Prometheus.PromResult[] SectorDataTputDlPrev = promNetworkPrevious.ApDlTp.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).OrderByDescending(x => double.Parse(x.value[1])).ToArray();
            Prometheus.PromResult[] SectorDataTputUlPrev = promNetworkPrevious.ApUlTp.data.result.Where(x => !(lookupApNameByIp(x.metric.instance, apInfo).Contains("PTP"))).OrderByDescending(x => double.Parse(x.value[1])).ToArray();
            AddSlideTwoTables(presentation, $"Sectors with Highest Peak Throughput ({days} days)",
                new TableInfo(
                    "Downlink",
                    //new string[] { "Sectors", "Current SMs", "Previous SMs", "Current Mbps", "Previous Mbps" },
                    new string[] { "Sectors", "Current Mbps", "Previous Mbps", "Last Avg Modulation", "Prev Avg Modulation" },
                    width3217171717,
                    SectorDataTputDl.Select(d => new string[] {
                        lookupApNameByIp(d.metric.instance, apInfo),
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
                    new string[] { "Sectors", "Current Mbps", "Previous Mbps", "Last Avg Modulation", "Prev Avg Modulation" },
                    width3217171717,
                    SectorDataTputUl.Select(u => new string[] {
                        lookupApNameByIp(u.metric.instance, apInfo),
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

        private static string lookupApNameByIp(string ipaddress, IDictionary<ESN, AccessPointRadioInfo> _apInfo)
        {
            var ApRi = _apInfo.Values.Where(v => v.IP == ipaddress).FirstOrDefault()?.Name;
            return ApRi ?? ipaddress;
        }

    }
}


