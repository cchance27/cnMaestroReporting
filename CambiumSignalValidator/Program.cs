using CommonCalculations;
using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.IO;
using cnMaestro.cnDataType;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using OfficeOpenXml;
using CambiumSNMP;
using System.Threading;

namespace CambiumSignalValidator
{
    class Program
    {
        private static cnMaestro.Manager cnManager { get; set; }
        private static cnMaestro.Settings cnMaestroConf = new cnMaestro.Settings();
        private static CambiumSNMP.Settings snmpConf = new CambiumSNMP.Settings();
        private static RadioConfig cambium;

        private static async Task Main(string[] args)
        {
            FetchConfiguration(); // Load our appSettings
            // Currently the program is only being used for testing various functions we will eventually have a real workflow.
            //TODO: Filtering change to KVP pair as doing it with strings is nasty

            cnManager = new cnMaestro.Manager(cnMaestroConf);

            await cnManager.ConnectAsync();
            var cnApi = new cnMaestro.Api(cnManager);
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

                var snmp = new CambiumSNMP.Manager(snmpConf.Community, snmpConf.Version, snmpConf.Retries);

                // Async fetch all the SNMP From devices and return us a Dictionary<ipAddressStr, Dictionary<OIDstr, ValueStr>>
                var snmpResults = await snmp.GetMultipleDeviceOidsAsync(smIPs, OIDs.smAirDelayNs, OIDs.smFrequencyHz);

                // Nice select that returns all of our generated SM Info.
                IEnumerable<SubscriberRadioInfo> finalSubResults = deviceStatTask.Result
                    .Where(dev => dev.mode == "sm" && dev.status == "online")
                    .Select((smStat) => GenerateSmRadioInfo(
                        apDevice: devices[smStat.ap_mac],
                        apStats: apStats[smStat.ap_mac],
                        smDevice: devices[smStat.mac],
                        smStats: smStat,
                        smSnmp: snmpResults[devices[smStat.mac].ip]));

                // Export to XLSX
                SaveSubscriberData(finalSubResults.ToList(), "output.xlsx");
            }
            catch (WebException e)
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

            // smEPL === The power transmitted from the AP and what we expect to see on the SM
            var smEPL = RFCalc.EstimatedPowerLevel(
                smDistanceM,
                smFrequencyHz,
                0,
                Tx: cambium.Types[apDevice.product].Radio(apStats.radio.tx_power, 16),
                Rx: cambium.Types[smDevice.product].Radio(smStats.radio.tx_power));

            //TODO : split SM and AP gain on the device types

            // apEPL === The power transmitted from the SM and what we expect to see on the AP
            var apEPL = RFCalc.EstimatedPowerLevel(
                smDistanceM,
                smFrequencyHz,
                0,
                Tx: cambium.Types[smDevice.product].Radio(smStats.radio.tx_power),
                Rx: cambium.Types[apDevice.product].Radio(apStats.radio.tx_power, 16));

            Console.WriteLine($"Generated SM DeviceInfo: {smDevice.name}");

            return new SubscriberRadioInfo()
            {
                Name = smDevice.name,
                Esn = smDevice.mac,
                APName = apDevice.name,
                DistanceM = (int)smDistanceM,
                IP = smDevice.ip,
                Model = smDevice.product,
                SmEPL = Math.Round(smEPL, 2),
                SmAPL = smStats.radio.dl_rssi ?? -1,
                ApModel = apDevice.product,
                ApEPL = Math.Round(apEPL, 2),
                ApAPL = smStats.radio.ul_rssi ?? -1,
                APTxPower = apStats.radio.tx_power ?? cambium.Types[apDevice.product].MaxTransmit,
                SMTxPower = smStats.radio.tx_power ?? cambium.Types[smDevice.product].MaxTransmit,
                SMMaxTxPower = cambium.Types[smDevice.product].MaxTransmit
            };
        }

        /// <summary>
        /// This takes our Enumerable data and generates an Excel Worksheet to a filename.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="OutputFileName"></param>
        public static void SaveSubscriberData(IEnumerable<SubscriberRadioInfo> data, string OutputFileName)
        {
            using (var ep = new ExcelPackage())
            {
                var ew = ep.Workbook.Worksheets.Add("450 Devices");
                ew.Cells["A1"].LoadFromCollection<SubscriberRadioInfo>(data, true);
                ew.Cells[ew.Dimension.Address].AutoFitColumns();
                ep.SaveAs(new FileInfo(OutputFileName));
            }
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
            configuration.GetSection("canopySnmp").Bind(snmpConf);
            
            cambium = configuration.Get<RadioConfig>();
            
        }
    }
}