using cnMaestroReporting.Domain;
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
        private string OutputFile { get; } 
        private ExcelPackage ExcelDoc { get; }
        public Settings settings = new Settings();

        public Manager(IConfigurationSection configSection)
        {
            configSection.Bind(settings);

            if (ExcelDoc == null)
                ExcelDoc = new ExcelPackage();

            if (String.IsNullOrWhiteSpace(settings.FileName))
                OutputFile = $"Subscriber Report {DateTime.Now.ToString("yyyy-MM-dd")}.xlsx";
            else
                OutputFile = settings.FileName;
        }

        /// <summary>
        /// This takes our Enumerable data and generates an Excel Worksheet to a filename.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="OutputFileName"></param>
        public void Generate(IEnumerable<SubscriberRadioInfo> data)
        {
            var filtered = data.Where(dev =>
               (dev.SmPowerDiff > settings.BadPowerDiff || dev.ApPowerDiff > settings.BadPowerDiff) &&
               (dev.SmAPL < settings.LowSignal || dev.ApAPL < settings.LowSignal)).ToList();

            // Fix for us not having any filtered or data crashing the export.
            var dataTableCount = data.Count();
            if (dataTableCount == 0)
                dataTableCount = 1;

            var filteredTableCount = filtered.Count();
            if (filteredTableCount == 0)
                filteredTableCount = 1;

            var FilteredWS = ExcelDoc.Workbook.Worksheets.Add($"SM Signal Issue");
            FilteredWS.Cells["A1"].LoadFromCollection<SubscriberRadioInfo>(filtered, true);
            
            ExcelRange filteredRange = FilteredWS.Cells[FilteredWS.Dimension.Address];
            ExcelTable filteredTable = FilteredWS.Tables.Add(filteredRange, "SMSignalIssues");
            filteredRange.Sort(filteredTable.Columns["APName"].Position);

            var FullWS = ExcelDoc.Workbook.Worksheets.Add("All Subscribers");
            FullWS.Cells["A1"].LoadFromCollection<SubscriberRadioInfo>(data, true);
            ExcelRange fullRange = FullWS.Cells[FullWS.Dimension.Address];
            ExcelTable fullTable = FullWS.Tables.Add(fullRange, "Subscribers");
            fullRange.Sort(fullTable.Columns["APName"].Position);

            RenameColumn("ApTxPower", "AP TX", ref fullTable);
            RenameColumn("ApTxPower", "AP TX", ref filteredTable);
            RenameColumn("SmTxPower", "SM TX", ref fullTable);
            RenameColumn("SmTxPower", "SM TX", ref filteredTable);
            RenameColumn("SmMaxTxPower", "SMTxMax", ref fullTable);
            RenameColumn("SmMaxTxPower", "SMTxMax", ref filteredTable);

            CommentColumn("SmImbalance", "Difference between Vertical and Horizontal power levels on SM", ref fullTable, ref FullWS);
            CommentColumn("SmImbalance", "Difference between Vertical and Horizontal power levels on SM", ref filteredTable, ref FilteredWS);
            RenameColumn("SmImbalance", "SMI", ref fullTable);
            RenameColumn("SmImbalance", "SMI", ref filteredTable);

            Cond2ColorFormat("SmAPL", dataTableCount, ref fullTable, ref FullWS, -85, Color.Red, -53, Color.Green);
            Cond2ColorFormat("SmAPL", filteredTableCount, ref filteredTable, ref FilteredWS, -85, Color.Red, -53, Color.Green);
            Cond5IconFormat("SmAPL", dataTableCount, ref fullTable, ref FullWS, -60, -70, -80, -90, -100);
            Cond5IconFormat("SmAPL", filteredTableCount, ref filteredTable, ref FilteredWS, -60, -70, -80, -90, -100);
            CommentColumn("SmAPL", "Actual Power Level the SM is receiving from the AP", ref fullTable, ref FullWS);
            CommentColumn("SmAPL", "Actual Power Level the SM is receiving from the AP", ref filteredTable, ref FilteredWS);

            Cond2ColorFormat("SmEPL", dataTableCount, ref fullTable, ref FullWS, -85, Color.Red, -53, Color.Green);
            Cond2ColorFormat("SmEPL", filteredTableCount, ref filteredTable, ref FilteredWS, -85, Color.Red, -53, Color.Green);
            Cond5IconFormat("SmEPL", dataTableCount, ref fullTable, ref FullWS, -60, -70, -80, -90, -100);
            Cond5IconFormat("SmEPL", filteredTableCount, ref filteredTable, ref FilteredWS, -60, -70, -80, -90, -100);
            CommentColumn("SmEPL", "Expected Power Level the SM should receive from the AP", ref fullTable, ref FullWS);
            CommentColumn("SmEPL", "Expected Power Level the SM should receive from the AP", ref filteredTable, ref FilteredWS);

            Cond2ColorFormat("ApAPL", dataTableCount, ref fullTable, ref FullWS, -85, Color.Red, -53, Color.Green);
            Cond2ColorFormat("ApAPL", filteredTableCount, ref filteredTable, ref FilteredWS, -85, Color.Red, -53, Color.Green);
            CommentColumn("ApAPL", "Actual Power Level the AP is receiving from the SM", ref fullTable, ref FullWS);
            CommentColumn("ApAPL", "Actual Power Level the AP is receiving from the SM", ref filteredTable, ref FilteredWS);
            Cond5IconFormat("ApAPL", dataTableCount, ref fullTable, ref FullWS, -60, -70, -80, -90, -100);
            Cond5IconFormat("ApAPL", filteredTableCount, ref filteredTable, ref FilteredWS, -60, -70, -80, -90, -100);

            Cond2ColorFormat("ApEPL", dataTableCount, ref fullTable, ref FullWS, -85, Color.Red, -53, Color.Green);
            Cond2ColorFormat("ApEPL", filteredTableCount, ref filteredTable, ref FilteredWS, -85, Color.Red, -53, Color.Green);
            CommentColumn("ApEPL", "Expected Power Level the AP should receive from the SM", ref fullTable, ref FullWS);
            CommentColumn("ApEPL", "Expected Power Level the AP should receive from the SM", ref filteredTable, ref FilteredWS);
            Cond5IconFormat("ApEPL", dataTableCount, ref fullTable, ref FullWS, -60, -70, -80, -90, -100);
            Cond5IconFormat("ApEPL", filteredTableCount, ref filteredTable, ref FilteredWS, -60, -70, -80, -90, -100);

            Cond2ColorFormat("ApPowerDiff", dataTableCount, ref fullTable, ref FullWS, 0, Color.Green, 15, Color.Red);
            Cond2ColorFormat("ApPowerDiff", filteredTableCount, ref filteredTable, ref FilteredWS, 0, Color.Green, 15, Color.Red);
            CommentColumn("ApPowerDiff", "AP side power difference between Expected and Actual", ref fullTable, ref FullWS);
            CommentColumn("ApPowerDiff", "AP side power difference between Expected and Actual", ref filteredTable, ref FilteredWS);
            RenameColumn("ApPowerDiff", "APPD", ref fullTable);
            RenameColumn("ApPowerDiff", "APPD", ref filteredTable);

            Cond2ColorFormat("SmPowerDiff", dataTableCount, ref fullTable, ref FullWS, 0, Color.Green, 15, Color.Red);
            Cond2ColorFormat("SmPowerDiff", filteredTableCount, ref filteredTable, ref FilteredWS, 0, Color.Green, 15, Color.Red);
            CommentColumn("SmPowerDiff", "SM side power difference between Expected and Actual", ref fullTable, ref FullWS);
            CommentColumn("SmPowerDiff", "SM side power difference between Expected and Actual", ref filteredTable, ref FilteredWS);
            RenameColumn("SmPowerDiff", "SMPD", ref fullTable);
            RenameColumn("SmPowerDiff", "SMPD", ref filteredTable);

            fullTable.ShowFilter = false;
            filteredTable.ShowFilter = false;

            FullWS.Cells[FullWS.Dimension.Address].AutoFitColumns();
            FilteredWS.Cells[FilteredWS.Dimension.Address].AutoFitColumns();
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
            ExcelDoc.SaveAs(new FileInfo(OutputFile));
        }

        private void CommentColumn(string ColumnName, string Comment, ref ExcelTable table, ref ExcelWorksheet ew)
        {
            ew.Cells[1, table.Columns[ColumnName].Position + 1].AddComment(Comment, "CSV");
        }

        private void RenameColumn(string ColumnName, string NewName, ref ExcelTable table)
        {
            table.Columns[ColumnName].Name = NewName;
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
    }
}
