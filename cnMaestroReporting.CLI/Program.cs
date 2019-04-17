using CommonCalculations;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using cnMaestroReporting.Domain;
using cnMaestroReporting.cnMaestroAPI.cnDataType;

namespace cnMaestroReporting.CLI
{
    class Program
    {
        private static cnMaestroAPI.Manager cnManager { get; set; }
        private static cnMaestroAPI.Settings cnMaestroConf = new cnMaestroReporting.cnMaestroAPI.Settings();
        private static SNMP.Settings snmpConf = new SNMP.Settings();
        private static Output.XLSX.Settings outputConf = new Output.XLSX.Settings();
        private static RadioConfig cambiumType;

        private static async Task Main(string[] args)
        {
            FetchConfiguration(); // Load our appSettings
            // Currently the program is only being used for testing various functions we will eventually have a real workflow.
            //TODO: Filtering change to KVP pair as doing it with strings is nasty

            cnManager = new cnMaestroAPI.Manager(cnMaestroConf);
            await cnManager.ConnectAsync();
            var cnApi = new cnMaestroAPI.Api(cnManager);
            var snmp = new SNMP.Manager(snmpConf.Community, snmpConf.Version, snmpConf.Retries);

            try
            {
                var Towers = await cnApi.GetTowersAsync(cnMaestroConf.Network);

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
                var snmpResults = await snmp.GetMultipleDeviceOidsAsync(smIPs, SNMP.OIDs.smAirDelayNs, SNMP.OIDs.smFrequencyHz);

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
                var outputManager = new Output.XLSX.Manager(outputConf);
                outputManager.Generate(finalSubResults.ToList());
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
            Double.TryParse(smSnmp[SNMP.OIDs.smFrequencyHz], out double smFrequencyHz);
            Int32.TryParse(smSnmp[SNMP.OIDs.smAirDelayNs], out int smAirDelayNs);

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