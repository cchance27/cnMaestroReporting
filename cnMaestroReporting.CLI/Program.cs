using cnMaestroReporting.cnMaestroAPI.cnDataType;
using cnMaestroReporting.Domain;
using CommonCalculations;
using Microsoft.Extensions.Configuration;
using MoreLinq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace cnMaestroReporting.CLI
{
    internal class Program
    {
        private static cnMaestroAPI.Manager CnManager { get; set; } // Our access to cnMaestro
        private static RadioConfig _cambiumRadios; // Holds the model configurations we use for various cambium devices.
        private static IConfigurationRoot _generalConfig; // Holds the general config for the app and plugins

        private static async Task Main(string[] args)
        {
            _generalConfig = FetchConfiguration(); // Load our appSettings into generalConfig

            //cnManager sets up generic settings and configuration for the overall connection to cnMaestro (authentication)
            CnManager = new cnMaestroAPI.Manager(_generalConfig.GetSection("cnMaestro"));
            await CnManager.ConnectAsync();

            // Initialize our SNMP Controller
            var snmp = new SNMP.Manager(_generalConfig.GetSection("snmp"));

            // Get all the device info from cnMaestro we will be using for the program loop
            var towers = await CnManager.GetTowersAsync();
            var deviceStatTask = CnManager.GetMultipleDevStatsAsync();
            var deviceTask = CnManager.GetMultipleDevicesAsync();
            Task.WaitAll(deviceTask, deviceStatTask);

            //Dictionary of all Devices so we can lookup by mac address
            var devices = deviceTask.Result
                .DistinctBy(dev => dev.mac)
                .Where(dev => dev.status == "online")
                .ToDictionary(dev => dev.mac);

            // List of online SM's that we can use for snmp polling
            var subscriberIps = deviceStatTask.Result
                .DistinctBy(sm => sm.mac)
                .Where(dev => dev.mode == "sm" && dev.status == "online")
                .Select(dev => devices[dev.mac].ip).ToArray();

            // List of online AP's that we can use for snmp polling
            var apIPs = deviceStatTask.Result
                .DistinctBy(ap => ap.mac)
                .Where(dev => dev.mode == "ap" && dev.status == "online")
                .Select(dev => devices[dev.mac].ip).ToArray();

            // Async fetch all the SNMP From devices and return us a Dictionary<ipAddressStr, Dictionary<OIDstr, ValueStr>>
            var progressIndicator = new Progress<SNMP.SnmpProgress>(ReportProgress);
            var snmpResultsSmTask = snmp.GetMultipleDeviceOidsAsync(subscriberIps, progressIndicator, SNMP.OIDs.smAirDelayNs, SNMP.OIDs.smFrequencyHz); // If we ever want to grab filters, but right now cant check vlan via snmp? SNMP.OIDs.filterPPPoE, SNMP.OIDs.filterAllIpv4, SNMP.OIDs.filterAllIpv6, SNMP.OIDs.filterArp, SNMP.OIDs.filterAllOther, SNMP.OIDs.filterDirection);
            var snmpResultsApTask = snmp.GetMultipleDeviceOidsAsync(apIPs, progressIndicator, SNMP.OIDs.sysContact); // If we ever want to grab filters, but right now cant check vlan via snmp? SNMP.OIDs.filterPPPoE, SNMP.OIDs.filterAllIpv4, SNMP.OIDs.filterAllIpv6, SNMP.OIDs.filterArp, SNMP.OIDs.filterAllOther, SNMP.OIDs.filterDirection);
            Task.WaitAll(snmpResultsSmTask, snmpResultsApTask);

            // Enumerable of OnlineAPs
            var onlineAPs = deviceStatTask.Result
                .Where(dev => dev.mode == "ap" && dev.status == "online");

            // TODO: API WONKYNESS IN 2.2.0r60 appears broken is returning duplicates so we had to add a distinctby to drop duplicates, and we're apparently not getting sent all devices/aps.

            // Just display which aps we found (debugging issue with duplicates)
            onlineAPs.DistinctBy(ap=>ap.mac).OrderBy(ap => ap.name)
                .ForEach(ap => Console.WriteLine($"{ap.name} Detected with MAC {ap.mac}"));

            // convert our IList task to a strongly typed object for easy use.
            var apInfo = onlineAPs.DistinctBy(ap => ap.mac).ToDictionary(
                ap => ap.mac, 
                ap => generateAccessPoint(
                    ap, 
                    devices[ap.mac].ip, 
                    snmpResultsApTask.Result[devices[ap.mac].ip][SNMP.OIDs.sysContact]
                    ));

            // Nice select that returns all of our generated SM Info.
            List<SubscriberRadioInfo> finalSubResults = deviceStatTask.Result
                .Where(dev => dev.mode == "sm" && dev.status == "online" && snmpResultsSmTask.Result.Keys.Contains(devices[dev.mac].ip))
                .DistinctBy(sm => sm.mac)
                .Select((smStat) => GenerateSmRadioInfo(
                    apDevice: devices[smStat.ap_mac],
                    apInfo: apInfo[smStat.ap_mac],
                    smDevice: devices[smStat.mac],
                    smStats: smStat,
                    smSnmp: snmpResultsSmTask.Result[devices[smStat.mac].ip])).ToList();

            // Export to XLSX
            var outputXLSX = new Output.XLSX.Manager(_generalConfig.GetSection("outputs:xlsx"));
            outputXLSX.Generate(finalSubResults);
            outputXLSX.Save();

            // Find our AP Bands
            var bands = apInfo.Values.DistinctBy(a => a.Channel.ToString()[0]).Select(a => a.Channel.ToString()[0]).ToArray();
            // Generate each bands KMZ
            foreach (char band in bands)
            {
                // Export to KMZ
                var outputKML = new Output.KML.Manager(
                    configSection: _generalConfig.GetSection("outputs:kml"),
                    subscribers: finalSubResults,
                    towers: towers.Select(tower => new KeyValuePair<string, CnLocation>(tower.Name, tower.Location)),
                    accesspoints: apInfo.Values.Where(a => a.Channel.ToString()[0] == band).ToList()
                    );
                outputKML.GenerateKML();
                outputKML.Save($"{band.ToString()}Ghz");
            }

            // Export to PTPPRJ
            var outputPTPPRJ = new Output.PTPPRJ.Manager(_generalConfig.GetSection("outputs:ptpprj"),
                finalSubResults,
                towers.Select(tower => new KeyValuePair<string, CnLocation>(tower.Name, tower.Location)),
                apInfo.Values.ToList());

            outputPTPPRJ.Generate();
            outputPTPPRJ.Save();

            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        static AccessPointRadioInfo generateAccessPoint(CnStatistics ap, string ip, string sysContact)
        {
            var apRI = new AccessPointRadioInfo()
            {
                Name = ap.name,
                Esn = ap.mac,
                IP = ip,
                ConnectedSMs = ap.connected_sms,
                Lan = ap.lan_status,
                Channel = Double.Parse(ap.radio.frequency),
                ColorCode = Byte.Parse(ap.radio.color_code),
                SyncState = ap.radio.sync_state,
                TxPower = ap.radio.tx_power ?? 0,
                Tower = ap.tower,
                Azimuth = 0,
                Downtilt = 0,
                Uptime = TimeSpan.FromSeconds(Double.Parse(ap.status_time))
            };

            // Parse the sysContact into Azimuth and Downtilt
            var azdtMatch = Regex.Match(sysContact, @"\[(?<azimuth>\d*)AZ\s(?<downtilt>\d*)DT\]");
            if (azdtMatch.Success)
            {
                // If we can parse the azimuth and downtilt save it to the AP if we can't set it to an invalid value so we know it wasn't good (0 would be a valid value so would -1)
                var goodAzimuth = Int32.TryParse(azdtMatch.Groups["azimuth"].ToString(), out int azimuth);
                var goodDowntilt = Int32.TryParse(azdtMatch.Groups["downtilt"].ToString(), out int downtilt);
                apRI.Azimuth = goodAzimuth ? azimuth : 999;
                apRI.Downtilt = goodDowntilt ? downtilt : 999;
            }

            return apRI;
        }

        /// <summary>
        /// Generate our unified SM View based on the AP & SM Device and Statistics from cnMaestro,
        /// as well as snmp we had to pull direct, and return a clean object.
        /// </summary>
        /// <param name="apDevice"></param>
        /// <param name="apInfo"></param>
        /// <param name="smDevice"></param>
        /// <param name="smStats"></param>
        /// <param name="smSnmp"></param>
        /// <returns></returns>
        public static SubscriberRadioInfo GenerateSmRadioInfo(CnDevice apDevice, AccessPointRadioInfo apInfo, CnDevice smDevice, CnStatistics smStats, IDictionary<string, string> smSnmp)
        {
            Double.TryParse(smSnmp[SNMP.OIDs.smFrequencyHz], out double smFrequencyHz);
            Int32.TryParse(smSnmp[SNMP.OIDs.smAirDelayNs], out int smAirDelayNs);

            double smDistanceM = RFCalc.MetersFromAirDelay(smAirDelayNs, smFrequencyHz, false);

            // If we have smGain from cnMaestro let's use it if not fall back to our configured value.
            Int32.TryParse(smStats.gain, out int smGain);
            if (smGain <= 0)
                smGain = _cambiumRadios.SM[smDevice.product].AntennaGain;

            // Odd irregularity where cnMaestro sends a -30 let's assume max Tx since it's obviously transmitting as we have a SM to calculate on the panel.
            var apTx = apInfo.TxPower;
            if (apTx <= 0)
                apTx = _cambiumRadios.AP[apDevice.product].MaxTransmit;

            // smEPL === The power transmitted from the AP and what we expect to see on the SM
            var smEPL = RFCalc.EstimatedPowerLevel(
                smDistanceM,
                smFrequencyHz,
                0,
                Tx: _cambiumRadios.AP[apDevice.product].Radio(apTx),
                Rx: _cambiumRadios.SM[smDevice.product].Radio(smStats.radio.tx_power, smGain));

            // apEPL === The power transmitted from the SM and what we expect to see on the AP
            var apEPL = RFCalc.EstimatedPowerLevel(
                smDistanceM,
                smFrequencyHz,
                0,
                Tx: _cambiumRadios.SM[smDevice.product].Radio(smStats.radio.tx_power, smGain),
                Rx: _cambiumRadios.AP[apDevice.product].Radio(apTx));

            Console.WriteLine($"Generated SM DeviceInfo: {smDevice.name}");

            //TODO: change coordinate system to use a strongly typed lat/long not the coordinates from cnLocation that are just an array as its confusing.
            var GeoDistance = GeoCalc.GeoDistance((double)smDevice.location.coordinates[1], (double)smDevice.location.coordinates[0], (double)apDevice.location.coordinates[1], (double)apDevice.location.coordinates[0]);

            return new SubscriberRadioInfo()
            {
                Name = smDevice.name,
                Esn = smDevice.mac,
                Tower = apDevice.tower,
                Firmware = smDevice.software_version,
                Latitude = smDevice.location.coordinates[1],
                Longitude = smDevice.location.coordinates[0],
                SmGain = smGain,
                APName = apDevice.name,
                DistanceM = (int)smDistanceM,
                DistanceGeoM = (int)GeoDistance,
                IP = smDevice.ip,
                Model = smDevice.product,
                SmEPL = Math.Round(smEPL, 2),
                SmAPL = smStats.radio.dl_rssi ?? -1,
                SmSNRH = smStats.radio.dl_snr_h ?? -1,
                SmSNRV = smStats.radio.dl_snr_v ?? -1,
                ApSNRH = smStats.radio.ul_snr_h ?? -1,
                ApSNRV = smStats.radio.ul_snr_v ?? -1,
                SmImbalance = smStats.radio.dl_rssi_imbalance ?? 0,
                ApModel = apDevice.product,
                ApEPL = Math.Round(apEPL, 2),
                ApAPL = smStats.radio.ul_rssi ?? -1,
                ApTxPower = apTx,
                SmTxPower = smStats.radio.tx_power ?? _cambiumRadios.SM[smDevice.product].MaxTransmit,
                SmMaxTxPower = _cambiumRadios.SM[smDevice.product].MaxTransmit,
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
            _cambiumRadios = configuration.Get<RadioConfig>();

            // Return overall config so we can pass it to plugins
            return configuration;
        }

        /// <summary>
        /// Output for thread reporting
        /// </summary>
        /// <param name="progress"></param>
        private static void ReportProgress(cnMaestroReporting.SNMP.SnmpProgress progress)
        {
            Console.WriteLine($"SNMP Update: {progress.CurrentProgressMessage} ({progress.CurrentProgressAmount}/{progress.TotalProgressAmount})");
        }

    }
}