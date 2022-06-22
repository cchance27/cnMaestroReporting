using cnMaestroAPI.cnDataType;
using cnMaestroReporting.Domain;
using cnMaestroReporting.Prometheus.Entities;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using OfficeOpenXml.ConditionalFormatting;
using OfficeOpenXml.Table;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace cnMaestroReporting.Output.XLSX
{
    public class Manager
    {
        private ExcelPackage ExcelDocSM { get; }

        private ExcelPackage ExcelDocAP { get; }
        private ExcelPackage ExcelDocAP2 { get; }
        public Settings settings = new Settings();

        public Manager()
        {

            settings = LoadConfiguration();

            ArgumentNullException.ThrowIfNull(settings);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            if (ExcelDocSM == null)
                ExcelDocSM = new ExcelPackage();

            if (ExcelDocAP == null)
                ExcelDocAP = new ExcelPackage();

            if (ExcelDocAP2 == null)
                ExcelDocAP2= new ExcelPackage();
        }

        private Settings LoadConfiguration()
        {
            var configuration = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
              .Build();

            // Setup config for the main eventloop
            return configuration.GetSection("outputs:xlsx").Get<Settings>();
        }

        /// <summary>
        /// This takes our Enumerable data and generates an Excel Worksheet to a filename.
        /// </summary>
        /// <param name="subRadioInfo"></param>
        public void Generate(IEnumerable<SubscriberRadioInfo> subRadioInfo, IDictionary<ESN, AccessPointRadioInfo> apRadioInfo, PromNetworkData promNetworkData, IEnumerable<KeyValuePair<string, CnLocation>> allTowers)
        {
            GenerateSubscriberWorkSheet(subRadioInfo.Where(dev =>
               (dev.SmAPL < settings.LowSignal || dev.ApAPL < settings.LowSignal)), 
               "SMs with Low Power Levels");

            GenerateSubscriberWorkSheet(subRadioInfo.Where(dev =>
               (dev.ApSNRH != 0 && dev.ApSNRV != 0) && 
               (dev.SmSNRH < settings.LowSNR || dev.SmSNRV < settings.LowSNR || dev.ApSNRH < settings.LowSNR || dev.ApSNRV < settings.LowSNR)), 
               "SMs with Low SNR");

            GenerateSubscriberWorkSheet(subRadioInfo, "All Subscribers");

            // Generate the AP Data and Worksheets
            var apAverageInfo = AccessPointAverageInfo.GenerateFromSMandAPData(subRadioInfo, apRadioInfo, promNetworkData, allTowers);

            GenerateAccessPointWorksheet(apAverageInfo.Where(dev => 
                (dev.AvgSmPl < settings.LowSignal || dev.AvgSmPl < settings.LowSignal)), 
                "APs with Low Avg SM Power Levels");

            GenerateAccessPointWorksheet(apAverageInfo.Where(dev => 
                (dev.AvgApSnrH < settings.LowSNR || dev.AvgApSnrV < settings.LowSignal || dev.AvgSmSnrH < settings.LowSNR || dev.AvgSmSnrV < settings.LowSignal)), 
                "APs with Low Avg SM SNRs");
         
            GenerateAccessPointAvailabilityWorksheet(apAverageInfo, "All AccessPoint");
        }

        /// <summary>
        /// Generate a Access Point Worksheet attached to the managers Excel dock with formatting.
        /// </summary>
        /// <param name="apData"></param>
        /// <param name="WorksheetName"></param>
        public void GenerateAccessPointWorksheet(IEnumerable<AccessPointAverageInfo> apData, string WorksheetName)
        {
            int apDataCount = apData.Count() == 0 ? 1 : apData.Count();

            ExcelWorksheet dataWS = ExcelDocAP.Workbook.Worksheets.Add(WorksheetName);
            dataWS.Cells["A1"].LoadFromCollection(apData, true);

            ExcelRange dataRange = dataWS.Cells[dataWS.Dimension.Address];
            ExcelTable dataTable = dataWS.Tables.Add(dataRange, WorksheetName.Replace(" ", ""));
            dataRange.Sort(dataTable.Columns["APName"].Position);

            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
               1000, Color.Green, Color.Red, new int[] { }, "AvgSMDistanceM", "Average distance from AP of SMs based on AirDelayNS");
            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
               1000, Color.Green, Color.Red, new int[] { }, "MaxSMDistanceM", "Maximum distance from AP of SMs based on AirDelayNS");

            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
               settings.LowSignal, Color.Red, Color.Green, new int[] { -50, -60, -70, -80, -100 }, "AvgApPL", "Average SM Power Level on the AP Side");
            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
               settings.LowSignal, Color.Red, Color.Green, new int[] { -50, -60, -70, -80, -100 }, "WorstApPL", "Worst SM Power Level on the AP Side");
            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
               settings.LowSignal, Color.Red, Color.Green, new int[] { -50, -60, -70, -80, -100 }, "AvgSmPL", "Average SM Power Level on the SM Side");
            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
               settings.LowSignal, Color.Red, Color.Green, new int[] { -50, -60, -70, -80, -100 }, "WorstSmPL", "Worst SM Power Level on the SM Side");

            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
                settings.LowSNR, Color.Red, Color.Green, new int[] { 32, 24, 17, 10, 0 }, "AvgApSNRH", "Average SNR on the AP-H (ap side of the connection in Horizontal Polarity");
            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
                settings.LowSNR, Color.Red, Color.Green, new int[] { 32, 24, 17, 10, 0 }, "AvgApSNRV", "Average SNR on the AP-V (ap side of the connection in Vertical Polarity");
            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
                settings.LowSNR, Color.Red, Color.Green, new int[] { 32, 24, 17, 10, 0 }, "WorstApSNR", "Worst SM Signal-To-Noise Ratio on the AP Side");
            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
                settings.LowSNR, Color.Red, Color.Green, new int[] { 32, 24, 17, 10, 0 }, "AvgSmSNRH", "Average SNR on the SM-H (sm side of the connection in Horizontal Polarity");
            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
                settings.LowSNR, Color.Red, Color.Green, new int[] { 32, 24, 17, 10, 0 }, "AvgSmSNRV", "Average SNR on the SM-V (sm side of the connection in Vertical Polarity");
            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
                settings.LowSNR, Color.Red, Color.Green, new int[] { 32, 24, 17, 10, 0 }, "WorstSmSNR", "Worst SM Signal-To-Noise Ratio on the SM Side");

            dataTable.ShowFilter = false;

            dataWS.Cells[dataWS.Dimension.Address].AutoFitColumns();
        }

        public void GenerateAccessPointAvailabilityWorksheet(IEnumerable<AccessPointAverageInfo> apAvgData, string WorksheetName)
        {
            int apDataCount = apAvgData.Count() == 0 ? 1 : apAvgData.Count();

            ExcelWorksheet dataWS = ExcelDocAP2.Workbook.Worksheets.Add(WorksheetName);

            dataWS.Cells["A1"].LoadFromCollection(apAvgData, true);
            ExcelRange dataRange = dataWS.Cells[dataWS.Dimension.Address];
            ExcelTable dataTable = dataWS.Tables.Add(dataRange, WorksheetName.Replace(" ", ""));
            dataRange.Sort(dataTable.Columns["APName"].Position);

            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
               1000, Color.Green, Color.Red, new int[] { }, "AvgSMDistanceM", "Average distance from AP of SMs based on AirDelayNS");
            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
               1000, Color.Green, Color.Red, new int[] { }, "MaxSMDistanceM", "Maximum distance from AP of SMs based on AirDelayNS");

            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
               settings.LowSignal, Color.Red, Color.Green, new int[] { -50, -60, -70, -80, -100 }, "AvgApPL", "Average SM Power Level on the AP Side");
            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
               settings.LowSignal, Color.Red, Color.Green, new int[] { -50, -60, -70, -80, -100 }, "AvgSmPL", "Average SM Power Level on the SM Side");
            
            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
                settings.LowSNR, Color.Red, Color.Green, new int[] { 32, 24, 17, 10, 0 }, "AvgApSNRH", "Average SNR on the AP-H (ap side of the connection in Horizontal Polarity");
            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
                settings.LowSNR, Color.Red, Color.Green, new int[] { 32, 24, 17, 10, 0 }, "AvgApSNRV", "Average SNR on the AP-V (ap side of the connection in Vertical Polarity");
            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
                settings.LowSNR, Color.Red, Color.Green, new int[] { 32, 24, 17, 10, 0 }, "AvgSmSNRH", "Average SNR on the SM-H (sm side of the connection in Horizontal Polarity");
            conditionalColumn(ref dataTable, ref dataWS, apDataCount,
                settings.LowSNR, Color.Red, Color.Green, new int[] { 32, 24, 17, 10, 0 }, "AvgSmSNRV", "Average SNR on the SM-V (sm side of the connection in Vertical Polarity");


            RenameColumn("DL30d", "DL tb/30d", ref dataTable);
            RenameColumn("DL7d", "DL tb/7d", ref dataTable);
            RenameColumn("DL1d", "DL tb/1d", ref dataTable);
            RenameColumn("UL30d", "UL tb/30d", ref dataTable);
            RenameColumn("UL7d", "UL tb/7d", ref dataTable);
            RenameColumn("UL1d", "UL tb/1d", ref dataTable);
            dataTable.ShowFilter = false;

            dataWS.Cells[dataWS.Dimension.Address].AutoFitColumns();
        }


        /// <summary>
        /// Generates a Subscriber Worksheet attached to this managers ExcelDoc with formatting.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="WorksheetName"></param>
        public void GenerateSubscriberWorkSheet(IEnumerable<SubscriberRadioInfo> data, string WorksheetName)
        {
            int dataCount = data.Count() == 0 ? 1 : data.Count();

            ExcelWorksheet dataWS = ExcelDocSM.Workbook.Worksheets.Add(WorksheetName);
            dataWS.Cells["A1"].LoadFromCollection<SubscriberRadioInfo>(data, true);

            ExcelRange dataRange = dataWS.Cells[dataWS.Dimension.Address];
            ExcelTable dataTable = dataWS.Tables.Add(dataRange, WorksheetName.Replace(" ", ""));
            dataRange.Sort(dataTable.Columns["APName"].Position);

            RenameColumn("ApTxPower", "AP TX", ref dataTable);
            RenameColumn("SmTxPower", "SM TX", ref dataTable);
            RenameColumn("SmMaxTxPower", "SMTxMax", ref dataTable);

            CommentColumn("SmImbalance", "Difference between Vertical and Horizontal power levels on SM", ref dataTable, ref dataWS);
            RenameColumn("SmImbalance", "SMI", ref dataTable);

            conditionalColumn(ref dataTable, ref dataWS, dataCount,
                settings.LowSignal, Color.Red, Color.Green, new int[] { -60, -70, -80, -90, -100 }, "SmAPL", "Actual Power Level the SM is receiving from the AP");
            conditionalColumn(ref dataTable, ref dataWS, dataCount,
                settings.LowSignal, Color.Red, Color.Green, new int[] { -60, -70, -80, -90, -100 }, "SmEPL", "Expected Power Level the SM should receive from the AP");
            conditionalColumn(ref dataTable, ref dataWS, dataCount,
                settings.LowSignal, Color.Red, Color.Green, new int[] { -60, -70, -80, -90, -100 }, "ApAPL", "Actual Power Level the AP is receiving from the SM");
            conditionalColumn(ref dataTable, ref dataWS, dataCount,
                settings.LowSignal, Color.Red, Color.Green, new int[] { -60, -70, -80, -90, -100 }, "ApEPL", "Expected Power Level the AP should receive from the SM");

            conditionalColumn(ref dataTable, ref dataWS, dataCount, 
                5, Color.Green, Color.Red, new int[] { }, "ApPowerDiff", "AP side power difference between Expected and Actual", "APPD");
            conditionalColumn(ref dataTable, ref dataWS, dataCount, 
                5, Color.Green, Color.Red, new int[] { }, "SmPowerDiff", "SM side power difference between Expected and Actual", "SMPD");

            conditionalColumn(ref dataTable, ref dataWS, dataCount,
                settings.LowSNR, Color.Red, Color.Green, new int[] { 30, 20, 15, 10, 0 }, "SmSnrH", "SNR on the SM-H (sm side of the connection in Vertical Polarity");
            conditionalColumn(ref dataTable, ref dataWS, dataCount,
                settings.LowSNR, Color.Red, Color.Green, new int[] { 30, 20, 15, 10, 0 }, "SmSnrV", "SNR on the SM-V (sm side of the connection in Vertical Polarity");
            conditionalColumn(ref dataTable, ref dataWS, dataCount,
                settings.LowSNR, Color.Red, Color.Green, new int[] { 30, 20, 15, 10, 0 }, "ApSNRH", "SNR on the AP-H (ap side of the connection in Horizontal Polarity");
            conditionalColumn(ref dataTable, ref dataWS, dataCount,
                settings.LowSNR, Color.Red, Color.Green, new int[] { 30, 20, 15, 10, 0 }, "ApSNRV", "SNR on the AP-V (ap side of the connection in Vertical Polarity");

            dataTable.ShowFilter = false;

            dataWS.Cells[dataWS.Dimension.Address].AutoFitColumns();
        }

        /// <summary>
        /// This will save the generated XLSX to a file
        /// </summary>
        /// <param name="ColumnName"></param>
        /// <param name="Comment"></param>
        /// <param name="table"></param>
        /// <param name="ew"></param>
        public void Save()
        {
            string FileNameSM = $"{DateTime.Now.ToString("yyyy-MM-dd")} - Subscriber Report.xlsx"; ;
            string FileNameAP = $"{DateTime.Now.ToString("yyyy-MM-dd")} - AP Report.xlsx"; ;
            string FileNameAP2 = $"{DateTime.Now.ToString("yyyy-MM-dd")} - AP Availability Report.xlsx"; ;

            ExcelDocSM.SaveAs(new FileInfo(FileNameSM));
            ExcelDocAP.SaveAs(new FileInfo(FileNameAP));
            ExcelDocAP2.SaveAs(new FileInfo(FileNameAP2));
        }

        // These functions are for manipulating Excel Tables and Columns.
        #region ExcelHelpers
        /// <summary>
        /// Helper Function that creates conditionally formatted columns
        /// </summary>
        /// <param name="tbl1"></param>
        /// <param name="ws1"></param>
        /// <param name="tableCounts1"></param>
        /// <param name="low2"></param>
        /// <param name="high2"></param>
        /// <param name="colorLow"></param>
        /// <param name="colorHigh"></param>
        /// <param name="icon5levels"></param>
        /// <param name="column"></param>
        /// <param name="comment"></param>
        /// <param name="newName"></param>
        public void conditionalColumn(ref ExcelTable tbl1, ref ExcelWorksheet ws1, int tableCounts1, int breakPoint, Color colorLow, Color colorHigh, int[] icon5levels, string column, string comment, string newName = "")
        {
            //Cond2ColorFormat(column, tableCounts1, ref tbl1, ref ws1, low2, colorLow, high2, colorHigh);

            CondBreakPointColor(column, tableCounts1, ref tbl1, ref ws1, breakPoint, colorHigh, colorLow);
            CommentColumn(column, comment, ref tbl1, ref ws1);
            if (icon5levels.Length == 5) { Cond5IconFormat(column, tableCounts1, ref tbl1, ref ws1, icon5levels[0], icon5levels[1], icon5levels[2], icon5levels[3], icon5levels[4]); };
            if (newName != "") { RenameColumn(column, newName, ref tbl1); };
        }

        private void CommentColumn(string ColumnName, string Comment, ref ExcelTable table, ref ExcelWorksheet ew)
        {
            ew.Cells[1, table.Columns[ColumnName].Position + 1].AddComment(Comment, "CSV");
        }

        private void RenameColumn(string ColumnName, string NewName, ref ExcelTable table)
        {
            table.Columns[ColumnName].Name = NewName;
        }

        private void CondBreakPointColor(string ColumnName, int dataCount, ref ExcelTable tbl, ref ExcelWorksheet ew, int breakValue, Color highColor, Color lowColor)
        {
            ExcelAddress e = new ExcelAddress(2, tbl.Columns[ColumnName].Position + 1, dataCount + 1, tbl.Columns[ColumnName].Position + 1);
            var cf = ew.ConditionalFormatting.AddGreaterThanOrEqual(e);
            cf.Formula = breakValue.ToString();
            cf.Style.Fill.BackgroundColor.Color = highColor;
            var cf2 = ew.ConditionalFormatting.AddLessThan(e);
            cf2.Formula = breakValue.ToString();
            cf2.Style.Fill.BackgroundColor.Color = lowColor;


        }
        private void Cond2ColorFormat(string ColumnName, int dataCount, ref ExcelTable tbl, ref ExcelWorksheet ew, int LowValue, Color LowColor, int HighValue, Color HighColor)
        {
            ExcelAddress e = new ExcelAddress(2, tbl.Columns[ColumnName].Position + 1, dataCount + 1, tbl.Columns[ColumnName].Position + 1);
            var cf = ew.ConditionalFormatting.AddTwoColorScale(e);
            cf.LowValue.Type = eExcelConditionalFormattingValueObjectType.Num;
            cf.LowValue.Value = LowValue;
            cf.LowValue.Color = LowColor;
            cf.HighValue.Type = eExcelConditionalFormattingValueObjectType.Num;
            cf.HighValue.Value = HighValue;
            cf.HighValue.Color = HighColor;

            cf.Style.Font.Bold = true;
        }

        private void Cond5IconFormat(string ColumnName, int dataCount, ref ExcelTable tbl, ref ExcelWorksheet ew, int val1, int val2, int val3, int val4, int val5, bool showVal = true)
        {
            ExcelAddress e = new ExcelAddress(2, tbl.Columns[ColumnName].Position + 1, dataCount + 1, tbl.Columns[ColumnName].Position + 1);
            var cf2 = ew.ConditionalFormatting.AddFiveIconSet(e, OfficeOpenXml.ConditionalFormatting.eExcelconditionalFormatting5IconsSetType.Rating);
            cf2.Icon5.Type = eExcelConditionalFormattingValueObjectType.Num;
            cf2.Icon5.Value = val1;
            cf2.Icon4.Type = eExcelConditionalFormattingValueObjectType.Num;
            cf2.Icon4.Value = val2;
            cf2.Icon3.Type = eExcelConditionalFormattingValueObjectType.Num;
            cf2.Icon3.Value = val3;
            cf2.Icon2.Type = eExcelConditionalFormattingValueObjectType.Num;
            cf2.Icon2.Value = val4;
            cf2.Icon1.Type = eExcelConditionalFormattingValueObjectType.Num;
            cf2.Icon1.Value = val5;
            cf2.ShowValue = showVal;
        }

        #endregion
    }
}
