using System;
using Spire.Presentation;
using System.Drawing;
using Spire.Presentation.Charts;
using System.Data;
using System.Collections.Generic;
using cnMaestroReporting.Domain;
using System.Linq;
using Memoize;
using MoreLinq;

namespace cnMaestroReporting.Output.PPTX
{
    public class Manager
    {
        const int GAP = 10;
        const int HEADER_HEIGHT = 25;
        const int SUBHEADER_HEIGHT = 25;
        const int TOTAL_DOUBLE_HEADER_HEIGHT = GAP + HEADER_HEIGHT + GAP + SUBHEADER_HEIGHT + GAP;
        const int TOTAL_SINGLE_HEADER_HEIGHT = GAP + HEADER_HEIGHT + GAP;

        const int SLIDE_WIDTH = 1280;
        const int SLIDE_HEIGHT = 720;

        public Manager(List<SubscriberRadioInfo> smInfo, Dictionary<ESN, AccessPointRadioInfo> apInfo, Redis cache)
        {
            Console.WriteLine("\nStarting PPTX Generation");

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
                new TableInfo($"Downlink - Total SMs: {smsByLowDlMod.Sum(y => y.Count())}", new string[] { "Sectors", "Total", "< 64-QAM" }, width502525, smsByLowDlMod.Select(x => new string[] { x.Key, apInfo.Values.Where(aps => aps.Name == x.Key).First().ConnectedSMs.ToString(), x.Count().ToString() })),
                new TableInfo($"Uplink - Total SMs {smsByLowUlMod.Sum(y => y.Count())}", new string[] { "Sectors", "Total", "< 64-QAM" }, width502525, smsByLowUlMod.Select(x => new string[] { x.Key, apInfo.Values.Where(aps => aps.Name == x.Key).First().ConnectedSMs.ToString(), x.Count().ToString() })));

            // SLIDE: Canopy Hardware Overview
            AddTwoChartSlide(presentation, "Network Canopy Hardware Overview",
                $"Total APs: {apInfo.Count()}", ChartType.Column3DStacked, apInfo.GroupBy(x => x.Value.Hardware).Select(y => new freqCountInfo(y.Key, y.Where(x => x.Value.Channel > 3000 && x.Value.Channel < 5000).Count(), y.Where(x => x.Value.Channel > 5000).Count())),
                $"Total SMs: {smInfo.Count()}", ChartType.Column3DStacked, smInfo.GroupBy(x => x.Model).Select(y => new freqCountInfo(y.Key, y.Where(x => {
                    var channel = apInfo.Where(ap => ap.Key == x.APEsn).FirstOrDefault().Value.Channel;
                    return channel > 3000 && channel < 5000;
                }).Count(), y.Where(x =>  apInfo.Where(ap => ap.Key == x.APEsn).FirstOrDefault().Value.Channel > 5000).Count())));

            // SLIDE: Sector Highest Data Usage
            Prometheus.PromApiResponse ApDl = cache.MemoizeAsync(nameof(Prometheus.API.QueryAllDlTotal) + "30d", () => Prometheus.API.QueryAllDlTotal("30d")).GetAwaiter().GetResult();
            Prometheus.PromApiResponse ApUl = cache.MemoizeAsync(nameof(Prometheus.API.QueryAllUlTotal) + "30d", () => Prometheus.API.QueryAllUlTotal("30d")).GetAwaiter().GetResult();
            decimal totalDL = ApDl.data.result.Sum((x) => decimal.Parse(x.value[1]));
            decimal totalUL = ApUl.data.result.Sum((x) => decimal.Parse(x.value[1]));
            decimal DL = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, totalDL, Utils.Bytes.Unit.Terabyte, 2);
            decimal UL = Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, totalUL, Utils.Bytes.Unit.Terabyte, 2);
            Prometheus.PromResult[] SectorDataUsageDl = ApDl.data.result.OrderBy(x => double.Parse(x.value[1])).Reverse().ToArray();
            Prometheus.PromResult[] SectorDataUsageUl = ApUl.data.result.OrderBy(x => double.Parse(x.value[1])).Reverse().ToArray();
            AddSlideTwoTables(presentation, "Sectors with Highest Data Usage (30 days)", 
                new TableInfo($"Downlink - Network Total: {DL}TB", new string[] { "Sectors", "Terabytes" }, width7030, SectorDataUsageDl.Select(x => new string[] { lookupApByIp(x.metric.instance, apInfo).Name, Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(x.value[1]), Utils.Bytes.Unit.Terabyte, 2).ToString() })),
                new TableInfo($"Uplink - Network Total: {UL}TB", new string[] { "Sectors", "Terabytes" }, width7030, SectorDataUsageUl.Select(x => new string[] { lookupApByIp(x.metric.instance, apInfo).Name, Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(x.value[1]), Utils.Bytes.Unit.Terabyte, 2).ToString() })));

            // SLIDE: Sectors with Highest Peak Throughput
            Prometheus.PromApiResponse ApDlTp = cache.MemoizeAsync(nameof(Prometheus.API.QueryAllDlMaxThroughput) + "30d", () => Prometheus.API.QueryAllDlMaxThroughput("30d")).GetAwaiter().GetResult();
            Prometheus.PromApiResponse ApUlTp = cache.MemoizeAsync(nameof(Prometheus.API.QueryAllUlMaxThroughput) + "30d", () => Prometheus.API.QueryAllUlMaxThroughput("30d")).GetAwaiter().GetResult();
            Prometheus.PromResult[] SectorDataTputDl = ApDlTp.data.result.OrderBy(x => double.Parse(x.value[1])).Reverse().ToArray();
            Prometheus.PromResult[] SectorDataTputUl = ApUlTp.data.result.OrderBy(x => double.Parse(x.value[1])).Reverse().ToArray();
            AddSlideTwoTables(presentation, "Sectors with Highest Peak Throughput (30 days)", 
                new TableInfo("Downlink", new string[] { "Sectors", "Mbps" }, width7030, SectorDataTputDl.Select(x => new string[] { lookupApByIp(x.metric.instance, apInfo).Name, Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(x.value[1]), Utils.Bytes.Unit.Megabyte, 2).ToString() + " Mbps" })),
                new TableInfo("Uplink", new string[] { "Sectors", "Mbps" }, width7030, SectorDataTputUl.Select(x => new string[] { lookupApByIp(x.metric.instance, apInfo).Name, Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, decimal.Parse(x.value[1]), Utils.Bytes.Unit.Megabyte, 2).ToString() + " Mbps" })));

            // SLIDE: Outages on canopy network
            IEnumerable<(string Tower, int Downtime)> downtimes = apInfo.Values.Select(ap => (Tower: ap.Tower, Downtime: ap.Alarms.Sum(x => x.duration))).OrderBy(x => x.Downtime).DistinctBy(x => x.Tower).Where(x => x.Downtime > 0).Reverse();
            AddSlideTwoTables(presentation, "Canopy Site Outages (30 days)", 
                new TableInfo("Total Canopy Downtime by Site", new string[] { "Site", "Duration" }, width7030, downtimes.Take(15).Select(x => new string[] { x.Tower, TimeSpan.FromSeconds(x.Downtime).ToString() })),
                new TableInfo("Total Canopy Downtime by Site", new string[] { "Site", "Duration" }, width7030, downtimes.Skip(15).Take(15).Select(x => new string[] { x.Tower, TimeSpan.FromSeconds(x.Downtime).ToString() })));

            // Compute filename and save output
            string FileName;
            if (String.IsNullOrWhiteSpace(settings.FileName))
                FileName = $"{DateTime.Now:yyyy-MM-dd} - Monthly Report.pptx";
            else
                FileName = settings.FileName;

            presentation.Slides.RemoveAt(0); // Remove first slide since our subfunctions skip it.

            presentation.SaveToFile(FileName, FileFormat.Pptx2013);
            Console.WriteLine("\nPPTX Generation Completed");
        }

        static AccessPointRadioInfo lookupApByIp(string ipaddress, Dictionary<ESN, AccessPointRadioInfo> _apInfo)
        {
            return _apInfo.Values.Where(v => v.IP == ipaddress).FirstOrDefault();
        }

        private void AddSlideTwoTables(Presentation presentation, string slideTitle, TableInfo table1Info, TableInfo table2Info)
        {
            int columns = 2;
            float tableWidth = (presentation.SlideSize.Size.Width - ((columns + 1) * GAP)) / 2;

            ISlide slide = presentation.Slides.Append();

            IAutoShape header0 = slide.Shapes.AppendShape(ShapeType.Rectangle, new RectangleF(GAP, GAP, presentation.SlideSize.Size.Width - (GAP * 2), HEADER_HEIGHT));
            header0.TextFrame.Paragraphs[0].Text = slideTitle;

            IAutoShape header1 = slide.Shapes.AppendShape(ShapeType.Rectangle, new RectangleF(GAP, TOTAL_SINGLE_HEADER_HEIGHT, tableWidth, SUBHEADER_HEIGHT));
            header1.TextFrame.Paragraphs[0].Text = table1Info.title;
            header1.TextFrame.Paragraphs[0].Alignment = TextAlignmentType.Center;
            AddTableToSlide(slide, table1Info, GAP, tableWidth);

            IAutoShape header2 = slide.Shapes.AppendShape(ShapeType.Rectangle, new RectangleF(GAP + tableWidth + GAP, TOTAL_SINGLE_HEADER_HEIGHT, tableWidth, SUBHEADER_HEIGHT));
            header2.TextFrame.Paragraphs[0].Text = table2Info.title;
            header2.TextFrame.Paragraphs[0].Alignment = TextAlignmentType.Center;
            AddTableToSlide(slide, table2Info, GAP + tableWidth + GAP, tableWidth);
        }

        private void AddOneChartSlide(Presentation presentation, ChartType chartType, IEnumerable<ISeriesInfo> data, string slideTitle, string chartTitle, string descText)
        {
            ISlide slide = presentation.Slides.Append();
            float chartWidth = (float)((presentation.SlideSize.Size.Width - (GAP * 3)) * 0.65);
            float textWidth = (float)((presentation.SlideSize.Size.Width - (GAP * 3)) * 0.35);
            float chartHeight = presentation.SlideSize.Size.Height - TOTAL_SINGLE_HEADER_HEIGHT;

            RectangleF rectChart = new RectangleF(GAP, TOTAL_SINGLE_HEADER_HEIGHT, chartWidth, chartHeight);
            RectangleF rectText = new RectangleF(GAP + chartWidth + GAP, TOTAL_SINGLE_HEADER_HEIGHT, textWidth, chartHeight);

            IAutoShape header0 = slide.Shapes.AppendShape(ShapeType.Rectangle, new RectangleF(GAP, GAP, presentation.SlideSize.Size.Width - (GAP * 2), HEADER_HEIGHT));
            header0.TextFrame.Paragraphs[0].Text = slideTitle;
            AddChart(slide, rectChart, chartTitle, chartType, data);

            IAutoShape shape = slide.Shapes.AppendShape(ShapeType.Rectangle, rectText);
            shape.TextFrame.Paragraphs.Clear();

            shape.TextFrame.Paragraphs.AddFromHtml(descText);
            for (int i = 0; i < shape.TextFrame.Paragraphs.Count; i++)
            {
                shape.TextFrame.Paragraphs[i].TextRanges[0].Fill.SolidColor.Color = Color.White;
                shape.TextFrame.Paragraphs[i].Alignment = TextAlignmentType.Center;
            }
        }

        private void AddTwoChartSlide(Presentation presentation, string slideTitle, string chartTitle1, ChartType chartType1, IEnumerable<ISeriesInfo> data1, string chartTitle2, ChartType chartType2, IEnumerable<ISeriesInfo> data2)
        {
            ISlide slide = presentation.Slides.Append();
            float chartWidth = (float)((presentation.SlideSize.Size.Width - (GAP * 3)) * 0.50);
            float chartHeight = presentation.SlideSize.Size.Height - TOTAL_SINGLE_HEADER_HEIGHT;

            RectangleF rectChart = new RectangleF(GAP, TOTAL_SINGLE_HEADER_HEIGHT, chartWidth, chartHeight);
            RectangleF rectChart2 = new RectangleF(GAP + chartWidth + GAP, TOTAL_SINGLE_HEADER_HEIGHT, chartWidth, chartHeight);

            IAutoShape header0 = slide.Shapes.AppendShape(ShapeType.Rectangle, new RectangleF(GAP, GAP, presentation.SlideSize.Size.Width - (GAP * 2), HEADER_HEIGHT));
            header0.TextFrame.Paragraphs[0].Text = slideTitle;
            AddChart(slide, rectChart, chartTitle1, chartType1, data1);
            AddChart(slide, rectChart2, chartTitle2, chartType2, data2);
        }

        private void AddChart(ISlide slide, RectangleF rect, string chartTitle, ChartType chartType, IEnumerable<ISeriesInfo> data)
        {
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
            }

            //apply built-in chart style  
            chart.ChartStyle = ChartStyle.Style11;


            //set overlap  
            chart.OverLap = 0;

            //set gap width  
            chart.GapWidth = 50;
        }

        #region DataTable Functions
        private static void AddTableToSlide(ISlide slide5, TableInfo tableInfo, float offset, float tableWidth)
        {
            ITable table7 = AddEmptyTableToSlide(slide5, 15 + 1, GAP, offset, tableInfo.columnWidths(tableWidth));
            AddDataToTable(table7, tableInfo.headers, tableInfo.columnData);
        }

        private static ITable AddEmptyTableToSlide(ISlide slide, int rows, int rowSize, float offset, double[] widths)
        {
            List<double> rowList = new();
            for (int i = 0; i < rows; i++)
            {
                rowList.Add(rowSize);
            }

            return slide.Shapes.AppendTable(offset, TOTAL_DOUBLE_HEADER_HEIGHT, widths, rowList.ToArray());
        }

        private static void AddDataToTable(ITable table, string[] Headers, IEnumerable<string[]> data)
        {
            var dataArray = data.ToArray();

            int length = table.TableRows.Count < data.Count() ? table.TableRows.Count : data.Count() + 1;
            for (int row = 0; row < length; row++)
            {
                for (int col = 0; col < dataArray[0].Length; col++) {
                    if (row == 0)
                    {
                        table[col, 0].TextFrame.Text = Headers[col];
                    }
                    else
                    {
                        table[col, row].TextFrame.Text = dataArray[row - 1][col];
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
        #endregion

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

        #region Type Info
        record TableInfo(string title, string[] headers, double[] columnPercents, IEnumerable<string[]> columnData)
        {
            double[] columnPercents { get; set; } = columnPercents;
            public double[] columnWidths(float tableWidth) => columnPercents.Select(x => x * tableWidth).ToArray();
        }

        record fullModInfo(string series, float Downlink, float Uplink) : ISeriesInfo
        {
            public string series { get; set; } = series;
        }
        record dlModInfo(string series, float Downlink) : ISeriesInfo
        {
            public string series { get; set; } = series;
        }
        record ulModInfo(string series, float Uplink) : ISeriesInfo
        {
            public string series { get; set; } = series;
        }
        record freqCountInfo(string series, int fq3Ghz, int fq5Ghz) : ISeriesInfo
        {
            public string series { get; set; } = series;
        }

        interface ISeriesInfo
        {
            string series { get; set; }
        }
        #endregion
    }
}

