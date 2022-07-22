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

namespace cnMaestroReporting.Output.PPTX
{
    public class Manager
    {
        private const int GAP = 10;
        private const int HEADER_HEIGHT = 50;
        private const int SUBHEADER_HEIGHT = 40;
        private const int TOTAL_DOUBLE_HEADER_HEIGHT = GAP + HEADER_HEIGHT + GAP + SUBHEADER_HEIGHT + GAP;
        private const int TOTAL_SINGLE_HEADER_HEIGHT = GAP + HEADER_HEIGHT + GAP;
         
        private const int SLIDE_WIDTH = 1280;
        private const int SLIDE_HEIGHT = 720;
         
        private static Color COLOR_BRAND = Color.FromArgb(255, 65, 0);
        private static Color COLOR_WHITE = Color.White;
        private static Color COLOR_GREY = Color.FromArgb(87, 88, 90);
        private static Color[] THEMECOLORS = new[] { COLOR_BRAND, Color.FromArgb(247, 163, 121), Color.FromArgb(250, 195, 168) };

        public Manager(List<SubscriberRadioInfo> smInfo, IDictionary<ESN, AccessPointRadioInfo> apInfo, PromNetworkData promNetworkData)
        {
            Console.WriteLine("\nStarting PPTX Generation");

            // PPTX Only Works with Online SMs
            smInfo = smInfo.Where(sm => sm.Online == true).ToList();

            var settings = (FileName: " ", Test: 0);
            double[] width7030 = { 0.70, 0.30 };
            double[] width502525 = { 0.50, 0.25, 0.25 };

            // create a PowerPoint document
            Presentation presentation = new Presentation();
            presentation.SlideSize.Type = SlideSizeType.Custom;
            presentation.SlideSize.Size = new SizeF(SLIDE_WIDTH, SLIDE_HEIGHT);
            presentation.SlideSize.Orientation = SlideOrienation.Landscape;

            // SLIDE: SM Modulation Composition
            IOrderedEnumerable<dlModInfo> dlMod = smInfo.GroupBy(sm => sm.DlMod).Select(x => new dlModInfo(x.Key, (float)x.Count())).OrderBy(x => modToOrder(x.series));
            IOrderedEnumerable<ulModInfo> ulMod = smInfo.GroupBy(sm => sm.UlMod).Select(x => new ulModInfo(x.Key, (float)x.Count())).OrderBy(x => modToOrder(x.series));
            IEnumerable<SubscriberRadioInfo> poorModulationDL = smInfo.Where(d => IsLowMod(d.DlMod));
            IEnumerable<SubscriberRadioInfo> poorModulationUL = smInfo.Where(d => IsLowMod(d.UlMod));
            IOrderedEnumerable<fullModInfo> modulationBreakdown = dlMod.Join(ulMod, o => o.series, i => i.series, (a, b) => new fullModInfo(a.series, a.Downlink, b.Uplink)).OrderBy(x => modToOrder(x.series));
            AddOneChartSlide(presentation, ChartType.Bar3DClustered, modulationBreakdown,
                "Current SM Modulation Overview",
                "Uplink/Downlink Modulations",
                $"<html><body>" +
                $"<center><h1>Total SMs</h1>{smInfo.Count().ToString("N0")}<br/><br/>" +
                $"<h2>Poor DL Modulation SMs</h2>{poorModulationDL.Count().ToString("N0")}<br/>" +
                $"({((double)poorModulationDL.Count() / (double)smInfo.Count()).ToString("P1")})<br/><br/>" +
                $"<h2>Poor UL Modulation SMs</h2>{poorModulationUL.Count().ToString("N0")}<br/>" +
                $"({((double)poorModulationUL.Count() / (double)smInfo.Count()).ToString("P1")})</font></center>" +
                $"<br/></br><p><i>Note: Poor Modulation set to less than 64-QAM</i></p></body></html>");

            // SLIDE: Sectors with Poor SM Modulations
            IEnumerable<IGrouping<string, SubscriberRadioInfo>> smsByLowDlMod = smInfo.Where(y => IsLowMod(y.DlMod)).GroupBy(x => x.APName).OrderBy(x => x.Count()).Reverse();
            IEnumerable<IGrouping<string, SubscriberRadioInfo>> smsByLowUlMod = smInfo.Where(y => IsLowMod(y.UlMod)).GroupBy(x => x.APName).OrderBy(x => x.Count()).Reverse();
            AddSlideTwoTables(presentation, "Sectors with Highest # of Poor Modulation SMs (Current)",
                new TableInfo($"Downlink - Total SMs: {smsByLowDlMod.Sum(y => y.Count())}", new string[] { "Sectors", "< 64-QAM", "Total" }, width502525, smsByLowDlMod.Select(x => new string[] { x.Key, x.Count().ToString(), apInfo.Values.Where(aps => aps.Name == x.Key).First().ConnectedSMs.ToString() })),
                new TableInfo($"Uplink - Total SMs {smsByLowUlMod.Sum(y => y.Count())}", new string[] { "Sectors", "< 64-QAM", "Total" }, width502525, smsByLowUlMod.Select(x => new string[] { x.Key, x.Count().ToString(), apInfo.Values.Where(aps => aps.Name == x.Key).First().ConnectedSMs.ToString() })));

            // SLIDE: Canopy Hardware Overview
            AddTwoChartSlide(presentation, "Network Canopy Hardware Overview",
                $"Total APs: {apInfo.Count()}", ChartType.Column3DStacked, apInfo.GroupBy(x => x.Value.Hardware).Select(y => new freqCountInfo(y.Key, y.Where(x => x.Value.Channel > 3000 && x.Value.Channel < 5000).Count(), y.Where(x => x.Value.Channel > 5000).Count())),
                $"Total SMs: {smInfo.Count()}", ChartType.Column3DStacked, smInfo.GroupBy(x => x.Model).Select(y => new freqCountInfo(y.Key, y.Where(x =>
                {
                    var channel = apInfo.Where(ap => ap.Key == x.APEsn).FirstOrDefault().Value.Channel;
                    return channel > 3000 && channel < 5000;
                }).Count(), y.Where(x => apInfo.Where(ap => ap.Key == x.APEsn).FirstOrDefault().Value.Channel > 5000).Count())));

            // SLIDE: Sector Highest Data Usage
            decimal totalDL = promNetworkData.ApDl.data.result.Sum((x) => decimal.Parse(x.value[1]));
            decimal totalUL = promNetworkData.ApUl.data.result.Sum((x) => decimal.Parse(x.value[1]));
            decimal DL = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, totalDL, Utils.Bytes.Unit.Terabyte, 2);
            decimal UL = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, totalUL, Utils.Bytes.Unit.Terabyte, 2);
            Prometheus.PromResult[] SectorDataUsageDl = promNetworkData.ApDl.data.result.OrderBy(x => double.Parse(x.value[1])).Reverse().ToArray();
            Prometheus.PromResult[] SectorDataUsageUl = promNetworkData.ApUl.data.result.OrderBy(x => double.Parse(x.value[1])).Reverse().ToArray();
            AddSlideTwoTables(presentation, "Sectors with Highest Data Usage (30 days)",
                new TableInfo($"Downlink - Network Total: {DL}TB", new string[] { "Sectors", "Terabytes" }, width7030, SectorDataUsageDl.Select(x => new string[] { lookupApNameByIp(x.metric.instance, apInfo), Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(x.value[1]), Utils.Bytes.Unit.Terabyte, 2).ToString() })),
                new TableInfo($"Uplink - Network Total: {UL}TB", new string[] { "Sectors", "Terabytes" }, width7030, SectorDataUsageUl.Select(x => new string[] { lookupApNameByIp(x.metric.instance, apInfo), Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(x.value[1]), Utils.Bytes.Unit.Terabyte, 2).ToString() })));

            // SLIDE: Sectors with Highest Peak Throughput
            Prometheus.PromResult[] SectorDataTputDl = promNetworkData.ApDlTp.data.result.OrderBy(x => double.Parse(x.value[1])).Reverse().ToArray();
            Prometheus.PromResult[] SectorDataTputUl = promNetworkData.ApUlTp.data.result.OrderBy(x => double.Parse(x.value[1])).Reverse().ToArray();
            AddSlideTwoTables(presentation, "Sectors with Highest Peak Throughput (30 days)",
                new TableInfo("Downlink", new string[] { "Sectors", "SMs", "Mbps" }, width502525, SectorDataTputDl.Select(x => new string[] { lookupApNameByIp(x.metric.instance, apInfo), apInfo.Where(ap => ap.Value.IP == x.metric.instance).FirstOrDefault().Value?.ConnectedSMs, Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(x.value[1]), Utils.Bytes.Unit.Megabyte, 2).ToString() + " Mbps" })),
                new TableInfo("Uplink", new string[] { "Sectors", "SMs", "Mbps" }, width502525, SectorDataTputUl.Select(x => new string[] { lookupApNameByIp(x.metric.instance, apInfo), apInfo.Where(ap => ap.Value.IP == x.metric.instance).FirstOrDefault().Value?.ConnectedSMs, Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(x.value[1]), Utils.Bytes.Unit.Megabyte, 2).ToString() + " Mbps" })));

            // SLIDE: Outages on canopy network
            IEnumerable<(string Tower, int Downtime)> downtimes = apInfo.Values.Select(ap => (Tower: ap.Tower, Downtime: ap.Alarms.Sum(x => x.duration))).OrderBy(x => x.Downtime).DistinctBy(x => x.Tower).Where(x => x.Downtime > 0).Reverse();
            AddSlideTwoTables(presentation, "Canopy Site Outages (30 days)",
                new TableInfo("Total Canopy Downtime by Site", new string[] { "Site", "Duration" }, width7030, downtimes.Take(15).Select(x => new string[] { x.Tower, TimeSpan.FromSeconds(x.Downtime).ToString() })),
                new TableInfo("Total Canopy Downtime by Site", new string[] { "Site", "Duration" }, width7030, downtimes.Skip(15).Take(15).Select(x => new string[] { x.Tower, TimeSpan.FromSeconds(x.Downtime).ToString() })));

            // Compute filename
            string FileName = GenerateFileName(settings);

            presentation.Slides.RemoveAt(0); // Remove first slide since our subfunctions skip it.

            presentation.SaveToFile(FileName, FileFormat.Pptx2013);
            Console.WriteLine("\nPPTX Generation Completed");
        }

        private static string GenerateFileName((string FileName, int Test) settings)
        {
            string FileName;
            if (String.IsNullOrWhiteSpace(settings.FileName))
                FileName = $"{DateTime.Now:yyyy-MM-dd} - Monthly Report.pptx";
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
            paragraph.TextRanges[0].FontHeight = 28;
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
        static int modToOrder(string mod)
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
        private static bool IsLowMod(string d)
        {
            return !d.Contains("256-") && !d.Contains("64-") && !d.Contains("128-");
        }
        #endregion

        private static string lookupApNameByIp(string ipaddress, IDictionary<ESN, AccessPointRadioInfo> _apInfo)
        {
            var ApRi = _apInfo.Values.Where(v => v.IP == ipaddress).FirstOrDefault()?.Name;
            return ApRi ?? ipaddress;
        }

    }
}


