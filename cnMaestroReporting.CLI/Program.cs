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
        private static cnMaestroAPI.Manager cnManager { get; set; } // Our access to cnMaestro
        private static RadioConfig cambiumRadios; // Holds the model configurations we use for various cambium devices.
        private static IConfigurationRoot generalConfig; // Holds the general config for the app and plugins

        private static async Task Main(string[] args)
        {
            generalConfig = FetchConfiguration(); // Load our appSettings into generalConfig
            
            //cnManager sets up generic settings and configuration for the overall connection to cnMaestro (authentication)
            cnManager = new cnMaestroAPI.Manager(generalConfig.GetSection("cnMaestro"));
            await cnManager.ConnectAsync();

            // Initialize our SNMP Controller
            var snmp = new SNMP.Manager(generalConfig.GetSection("snmp"));

            try
            {
                // Get all the device info from cnMaestro we will be using for the program loop
                var towers = await cnManager.Api.GetTowersAsync();
                var deviceStatTask = cnManager.Api.GetMultipleDevStatsAsync();
                var deviceTask = cnManager.Api.GetMultipleDevicesAsync();
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
                var outputXLSX = new Output.XLSX.Manager(generalConfig.GetSection("outputs:xlsx"));
                outputXLSX.Generate(finalSubResults.ToList());
                outputXLSX.Save();

                // Export to KMZ
                var outputKML = new Output.KML.Manager(generalConfig.GetSection("outputs:kml"));
                outputKML.GenerateKML(
                    finalSubResults.ToList(), 
                    towers.Select(tower => new KeyValuePair<string, CnLocation>(tower.Name, tower.Location)), 
                    apStats.Select(apStat => new KeyValuePair<string, string>(apStat.Value.name, apStat.Value.tower)));
                outputKML.Save();
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
                smGain = cambiumRadios.SM[smDevice.product].AntennaGain;

            // Odd irregularity where cnMaestro sends a -30 let's assume max Tx since it's obviously transmitting as we have a SM to calculate on the panel.
            var apTx = apStats.radio.tx_power ?? cambiumRadios.AP[apDevice.product].MaxTransmit;
            if (apTx < 0)
                apTx = cambiumRadios.AP[apDevice.product].MaxTransmit;

            // smEPL === The power transmitted from the AP and what we expect to see on the SM
            var smEPL = RFCalc.EstimatedPowerLevel(
                smDistanceM,
                smFrequencyHz,
                0,
                Tx: cambiumRadios.AP[apDevice.product].Radio(apTx),
                Rx: cambiumRadios.SM[smDevice.product].Radio(smStats.radio.tx_power, smGain));

            // apEPL === The power transmitted from the SM and what we expect to see on the AP
            var apEPL = RFCalc.EstimatedPowerLevel(
                smDistanceM,
                smFrequencyHz,
                0,
                Tx: cambiumRadios.SM[smDevice.product].Radio(smStats.radio.tx_power, smGain),
                Rx: cambiumRadios.AP[apDevice.product].Radio(apTx));

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
                SmTxPower = smStats.radio.tx_power ?? cambiumRadios.SM[smDevice.product].MaxTransmit,
                SmMaxTxPower = cambiumRadios.SM[smDevice.product].MaxTransmit, 
            };
        }

        /// <summary>
        /// Opens the configuration JSON's for the access methods (cnMaestro and SNMP), 
        /// as well as loading the various radiotypes from JSON
        /// </summary>
        private static IConfigurationRoot FetchConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile("radiotypes.json", optional: false, reloadOnChange: false);

            IConfigurationRoot configuration = builder.Build();
            
            // Setup config for the main eventloop
            cambiumRadios = configuration.Get<RadioConfig>();

            // Return overall config so we can pass it to plugins
            return configuration;
        }
    }
}