using CommonCalculations;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.IO;
using cnMaestro.cnDataType;
using System.Linq;
using System.Collections.Generic;
using OfficeOpenXml;
using CambiumSNMP;
using OfficeOpenXml.Table;
using System.Drawing;
using OfficeOpenXml.ConditionalFormatting;

namespace CambiumSignalValidator
{
    class Program
    {
        private static cnMaestro.Manager cnManager { get; set; }
        private static cnMaestro.Settings cnMaestroConf = new cnMaestro.Settings();
        private static CambiumSNMP.Settings snmpConf = new CambiumSNMP.Settings();
        private static Settings outputConf = new Settings();
        private static RadioConfig cambiumType;

        private static async Task Main(string[] args)
        {
            FetchConfiguration(); // Load our appSettings
            // Currently the program is only being used for testing various functions we will eventually have a real workflow.
            //TODO: Filtering change to KVP pair as doing it with strings is nasty

            cnManager = new cnMaestro.Manager(cnMaestroConf);
            await cnManager.ConnectAsync();
            var cnApi = new cnMaestro.Api(cnManager);
            var snmp = new CambiumSNMP.Manager(snmpConf.Community, snmpConf.Version, snmpConf.Retries);

            try
            {
                var Towers = await cnApi.GetTowersAsync(cnMaestroConf.Network); // TODO: Read network from config

                // TODO: we can add a fields= filter so we can reduce how much we're pulling from API
                var towerFilter = "";
                if (String.IsNullOrWhiteSpace(cnMaestroConf.Tower) == false)
                    towerFilter = "tower=" + Uri.EscapeDataString(cnMaestroConf.Tower);
                    
                var deviceStatTask = cnApi.GetMultipleDevStatsAsync(towerFilter);
                var deviceTask = cnApi.GetMultipleDevicesAsync(towerFilter);
                Task.WaitAll(deviceTask, deviceStatTask);

                var devices = deviceTask.Result
                    .Where(dev => dev.status == "online")
                    .ToDictionary(dev => dev.mac); // All devices by mac key

                var apStats = deviceStatTask.Result
                    .Where(dev => dev.mode == "ap" && dev.status == "online")
                    .ToDictionary(ap => ap.mac);   // All statistics by mac key 

                var smIPs = deviceStatTask.Result
                    .Where(dev => dev.mode == "sm" && dev.status == "online")
                    .Select(dev => devices[dev.mac].ip).ToArray(); // Array of SM IPs to poll

                // Async fetch all the SNMP From devices and return us a Dictionary<ipAddressStr, Dictionary<OIDstr, ValueStr>>
                var snmpResults = await snmp.GetMultipleDeviceOidsAsync(smIPs, OIDs.smAirDelayNs, OIDs.smFrequencyHz);

                // Nice select that returns all of our generated SM Info.
                IEnumerable<SubscriberRadioInfo> finalSubResults = deviceStatTask.Result
                    .Where(dev => dev.mode == "sm" && dev.status == "online" && snmpResults.Keys.Contains(devices[dev.mac].ip))
                    .Select((smStat) => GenerateSmRadioInfo(
                        apDevice: devices[smStat.ap_mac],
                        apStats: apStats[smStat.ap_mac],
                        smDevice: devices[smStat.mac],
                        smStats: smStat,
                        smSnmp: snmpResults[devices[smStat.mac].ip]));

                // Export to XLSX
                SaveSubscriberData(finalSubResults.ToList());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        /// <summary>
        /// Generate our unified SM View based on the AP & SM Device and Statistics from cnMaestro, 
        /// as well as snmp we had to pull direct, and return a clean object.
        /// </summary>
        /// <param name="apDevice"></param>
        /// <param name="apStats"></param>
        /// <param name="smDevice"></param>
        /// <param name="smStats"></param>
        /// <param name="smSnmp"></param>
        /// <returns></returns>
        public static SubscriberRadioInfo GenerateSmRadioInfo(CnDevice apDevice, CnStatistics apStats, CnDevice smDevice, CnStatistics smStats, IDictionary<string, string> smSnmp)
        {
            Double.TryParse(smSnmp[OIDs.smFrequencyHz], out double smFrequencyHz);
            Int32.TryParse(smSnmp[OIDs.smAirDelayNs], out int smAirDelayNs);

            double smDistanceM = RFCalc.MetersFromAirDelay(smAirDelayNs, smFrequencyHz, false);

            // If we have smGain from cnMaestro let's use it if not fall back to our configured value.
            Int32.TryParse(smStats.gain, out int smGain);
            if (smGain == 0)
                smGain = cambiumType.SM[smDevice.product].AntennaGain;

            // Odd irregularity where cnMaestro sends a -30 let's assume max Tx since it's obviously transmitting as we have a SM to calculate on the panel.
            var apTx = apStats.radio.tx_power ?? cambiumType.AP[apDevice.product].MaxTransmit;
            if (apTx < 0)
                apTx = cambiumType.AP[apDevice.product].MaxTransmit;

            // smEPL === The power transmitted from the AP and what we expect to see on the SM
            var smEPL = RFCalc.EstimatedPowerLevel(
                smDistanceM,
                smFrequencyHz,
                0,
                Tx: cambiumType.AP[apDevice.product].Radio(apTx),
                Rx: cambiumType.SM[smDevice.product].Radio(smStats.radio.tx_power, smGain));

            // apEPL === The power transmitted from the SM and what we expect to see on the AP
            var apEPL = RFCalc.EstimatedPowerLevel(
                smDistanceM,
                smFrequencyHz,
                0,
                Tx: cambiumType.SM[smDevice.product].Radio(smStats.radio.tx_power, smGain),
                Rx: cambiumType.AP[apDevice.product].Radio(apTx));

            Console.WriteLine($"Generated SM DeviceInfo: {smDevice.name}");

            return new SubscriberRadioInfo()
            {
                Name = smDevice.name,
                Esn = smDevice.mac,
                Location = apDevice.tower,
                Firmware = smDevice.software_version,
                Latitude = smDevice.location.coordinates[1],
                Longitude = smDevice.location.coordinates[0],
                SmGain = smGain,
                APName = apDevice.name,
                DistanceM = (int)smDistanceM,
                IP = smDevice.ip,
                Model = smDevice.product,
                SmEPL = Math.Round(smEPL, 2),
                SmAPL = smStats.radio.dl_rssi ?? -1,
                SmImbalance = smStats.radio.dl_rssi_imbalance ?? 0,
                ApModel = apDevice.product,
                ApEPL = Math.Round(apEPL, 2),
                ApAPL = smStats.radio.ul_rssi ?? -1,
                ApTxPower = apTx,
                SmTxPower = smStats.radio.tx_power ?? cambiumType.SM[smDevice.product].MaxTransmit,
                SmMaxTxPower = cambiumType.SM[smDevice.product].MaxTransmit, 
            };
        }

        //TODO: move the 
        /// <summary>
        /// This takes our Enumerable data and generates an Excel Worksheet to a filename.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="OutputFileName"></param>
        public static void SaveSubscriberData(IEnumerable<SubscriberRadioInfo> data)
        {
            if (String.IsNullOrWhiteSpace(outputConf.FileName))
                outputConf.FileName = $"Subscriber Report {DateTime.Now.ToString("yyyy-MM-dd")}.xlsx";

            using (var ep = new ExcelPackage())
            {
                var filtered = data.Where(dev =>
                   (dev.SmPowerDiff > outputConf.BadPowerDiff || dev.ApPowerDiff > outputConf.BadPowerDiff) &&
                   (dev.SmAPL < outputConf.LowSignal || dev.ApAPL < outputConf.LowSignal));

                var FilteredWS = ep.Workbook.Worksheets.Add("SM Signal Issues");
                FilteredWS.Cells["A1"].LoadFromCollection<SubscriberRadioInfo>(filtered, true);

                ExcelRange filteredRange = FilteredWS.Cells[FilteredWS.Dimension.Address];
                ExcelTable filteredTable = FilteredWS.Tables.Add(filteredRange, "SMSignalIssues");
                filteredRange.Sort(filteredTable.Columns["APName"].Position);

                var FullWS = ep.Workbook.Worksheets.Add("450 Subscribers");
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

                Cond2ColorFormat("SmAPL", data.Count(), ref fullTable, ref FullWS, -85, "#FF0000", -53, "#00FF00");
                Cond2ColorFormat("SmAPL", filtered.Count(), ref filteredTable, ref FilteredWS, -85, "#FF0000", -53, "#00FF00");
                Cond5IconFormat("SmAPL", data.Count(), ref fullTable, ref FullWS, -60, -70, -80, -90, -100);
                Cond5IconFormat("SmAPL", filtered.Count(), ref filteredTable, ref FilteredWS, -60, -70, -80, -90, -100);
                CommentColumn("SmAPL", "Actual Power Level the SM is receiving from the AP", ref fullTable, ref FullWS);
                CommentColumn("SmAPL", "Actual Power Level the SM is receiving from the AP", ref filteredTable, ref FilteredWS);

                Cond2ColorFormat("SmEPL", data.Count(), ref fullTable, ref FullWS, -85, "#FF0000", -53, "#00FF00");
                Cond2ColorFormat("SmEPL", filtered.Count(), ref filteredTable, ref FilteredWS, -85, "#FF0000", -53, "#00FF00");
                Cond5IconFormat("SmEPL", data.Count(), ref fullTable, ref FullWS, -60, -70, -80, -90, -100);
                Cond5IconFormat("SmEPL", filtered.Count(), ref filteredTable, ref FilteredWS, -60, -70, -80, -90, -100);
                CommentColumn("SmEPL", "Expected Power Level the SM should receive from the AP", ref fullTable, ref FullWS);
                CommentColumn("SmEPL", "Expected Power Level the SM should receive from the AP", ref filteredTable, ref FilteredWS);

                Cond2ColorFormat("ApAPL", data.Count(), ref fullTable, ref FullWS, -85, "#FF0000", -53, "#00FF00");
                Cond2ColorFormat("ApAPL", filtered.Count(), ref filteredTable, ref FilteredWS, -85, "#FF0000", -53, "#00FF00");
                CommentColumn("ApAPL", "Actual Power Level the AP is receiving from the SM", ref fullTable, ref FullWS);
                CommentColumn("ApAPL", "Actual Power Level the AP is receiving from the SM", ref filteredTable, ref FilteredWS);
                Cond5IconFormat("ApAPL", data.Count(), ref fullTable, ref FullWS, -60, -70, -80, -90, -100);
                Cond5IconFormat("ApAPL", filtered.Count(), ref filteredTable, ref FilteredWS, -60, -70, -80, -90, -100);

                Cond2ColorFormat("ApEPL", data.Count(), ref fullTable, ref FullWS, -85, "#FF0000", -53, "#00FF00");
                Cond2ColorFormat("ApEPL", filtered.Count(), ref filteredTable, ref FilteredWS, -85, "#FF0000", -53, "#00FF00");
                CommentColumn("ApEPL", "Expected Power Level the AP should receive from the SM", ref fullTable, ref FullWS);
                CommentColumn("ApEPL", "Expected Power Level the AP should receive from the SM", ref filteredTable, ref FilteredWS);
                Cond5IconFormat("ApEPL", data.Count(), ref fullTable, ref FullWS, -60, -70, -80, -90, -100);
                Cond5IconFormat("ApEPL", filtered.Count(), ref filteredTable, ref FilteredWS, -60, -70, -80, -90, -100);

                Cond2ColorFormat("ApPowerDiff", data.Count(), ref fullTable, ref FullWS, 0, "#00FF00", 15, "#FF0000");
                Cond2ColorFormat("ApPowerDiff", filtered.Count(), ref filteredTable, ref FilteredWS, 0, "#00FF00", 15, "#FF0000");
                CommentColumn("ApPowerDiff", "AP side power difference between Expected and Actual", ref fullTable, ref FullWS);
                CommentColumn("ApPowerDiff", "AP side power difference between Expected and Actual", ref filteredTable, ref FilteredWS);
                RenameColumn("ApPowerDiff", "APPD", ref fullTable);
                RenameColumn("ApPowerDiff", "APPD", ref filteredTable);

                Cond2ColorFormat("SmPowerDiff", data.Count(), ref fullTable, ref FullWS, 0, "#00FF00", 15, "#FF0000");
                Cond2ColorFormat("SmPowerDiff", filtered.Count(), ref filteredTable, ref FilteredWS, 0, "#00FF00", 15, "#FF0000");
                CommentColumn("SmPowerDiff", "SM side power difference between Expected and Actual", ref fullTable, ref FullWS);
                CommentColumn("SmPowerDiff", "SM side power difference between Expected and Actual", ref filteredTable, ref FilteredWS);
                RenameColumn("SmPowerDiff", "SMPD", ref fullTable);
                RenameColumn("SmPowerDiff", "SMPD", ref filteredTable);

                fullTable.ShowFilter = false;
                filteredTable.ShowFilter = false;

                FullWS.Cells[FullWS.Dimension.Address].AutoFitColumns();
                FilteredWS.Cells[FilteredWS.Dimension.Address].AutoFitColumns();
                ep.SaveAs(new FileInfo(outputConf.FileName));
            }
        }

        public static void CommentColumn(string ColumnName, string Comment, ref ExcelTable table, ref ExcelWorksheet ew)
        {
            ew.Cells[1, table.Columns[ColumnName].Position + 1].AddComment(Comment, "CSV");
        }

        public static void RenameColumn(string ColumnName, string NewName, ref ExcelTable table)
        {
            table.Columns[ColumnName].Name = NewName;
        }

        public static void Cond2ColorFormat(string ColumnName, int dataCount, ref ExcelTable tbl, ref ExcelWorksheet ew, int LowValue, string LowColor, int HighValue, string HighColor)
        {
            ExcelAddress e = new ExcelAddress(2, tbl.Columns[ColumnName].Position + 1, dataCount + 1, tbl.Columns[ColumnName].Position + 1);
            var cf = ew.ConditionalFormatting.AddTwoColorScale(e);
            cf.LowValue.Type = eExcelConditionalFormattingValueObjectType.Num;
            cf.LowValue.Value = LowValue;
            cf.LowValue.Color = ColorTranslator.FromHtml(LowColor);
            cf.HighValue.Type = eExcelConditionalFormattingValueObjectType.Num;
            cf.HighValue.Value = HighValue;
            cf.HighValue.Color = ColorTranslator.FromHtml(HighColor);

            cf.Style.Font.Bold = true;
        }

        public static void Cond5IconFormat(string ColumnName, int dataCount, ref ExcelTable tbl, ref ExcelWorksheet ew, int val1, int val2, int val3, int val4, int val5, bool showVal = true)
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

        /// <summary>
        /// Opens the configuration JSON's for the access methods (cnMaestro and SNMP), 
        /// as well as loading the various radiotypes from JSON
        /// </summary>
        private static void FetchConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile("radiotypes.json", optional: false, reloadOnChange: false);

            IConfigurationRoot configuration = builder.Build();

            configuration.GetSection("cnMaestro").Bind(cnMaestroConf);
            configuration.GetSection("cnMaestro").Bind(cnMaestroConf);
            configuration.GetSection("canopySnmp").Bind(snmpConf);
            
            cambiumType = configuration.Get<RadioConfig>();
            
        }
    }
}