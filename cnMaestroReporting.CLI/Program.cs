using cnMaestroReporting.cnMaestroAPI.cnDataType;
using cnMaestroReporting.Domain;
using CommonCalculations;
using Memoize;
using Microsoft.Extensions.Configuration;
using MoreLinq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static cnMaestroReporting.CLI.ProgressReporting;

namespace cnMaestroReporting.CLI
{
    static class Program
    {
        //private static cnMaestroAPI.Manager CnManager { get; set; } // Our access to cnMaestro
        private static RadioConfig _cambiumRadios; // Holds the model configurations we use for various cambium devices.
        private static IConfigurationRoot _generalConfig; // Holds the general config for the app and plugins
        private static cnMaestroAPI.Manager cnManager;
        private static string redisCacheServer = "172.27.165.244";
        private static int redisCacheTTLHours = 24;
        private static Redis cache;
        private static async Task Main()
        {
            _generalConfig = FetchConfiguration(); // Load our appSettings into generalConfig

            if (cache is null)
                cache = new Redis(redisCacheServer, redisCacheTTLHours);

            // Create our API Manager
            if (cnManager is null)
                cnManager = new cnMaestroAPI.Manager(_generalConfig.GetSection("cnMaestro"));

            // Delete SMs that have been offline for a certain duration of time to clean up cnMaestro.
            //await DeleteOldOfflineSMs(cnManager, 60);

            // Initialize our SNMP Controller
            var snmp = new SNMP.Manager(_generalConfig.GetSection("snmp"));

            IList<CnTower> towersFromApi = await cache.MemoizeAsync(nameof(cnManager.GetTowersAsync), () => cnManager.GetTowersAsync());
            IList<CnStatistics> deviceStatisticsFromApi = await cache.MemoizeAsync(nameof(cnManager.GetMultipleDevStatsAsync), () => cnManager.GetMultipleDevStatsAsync());
            IList<CnDevice> devicesFromApi = await cache.MemoizeAsync(nameof(cnManager.GetMultipleDevicesAsync), () => cnManager.GetMultipleDevicesAsync());

            //Dictionary of all Devices so we can lookup by mac address
            Dictionary<ESN, CnDevice> onlineDevicesFromApi = devicesFromApi.DistinctBy(dev => dev.mac).Where(dev => dev.status == "online").ToDictionary(dev => (ESN)dev.mac);
            IEnumerable<CnStatistics> onlineStatisticsFromApi = deviceStatisticsFromApi.Where(devStat => onlineDevicesFromApi.ContainsKey(devStat.mac));

            // Build a list of online ips for snmp checks
            string[] onlineSmIpAddresses = onlineStatisticsFromApi.DistinctBy(devStat => devStat.mac).Where(devStat => devStat.mode == DeviceMode.sm.ToString()).Select(dev => onlineDevicesFromApi[dev.mac].ip).ToArray();
            string[] onlineApIpAddresses = onlineStatisticsFromApi.DistinctBy(devStat => devStat.mac).Where(devStat => devStat.mode == DeviceMode.ap.ToString()).Select(dev => onlineDevicesFromApi[dev.mac].ip).ToArray();

            // Async fetch all the SNMP From devices and return us a Dictionary<ipAddressStr, Dictionary<OIDstr, ValueStr>>
            Progress<SNMP.SnmpProgress> progressIndicator = new Progress<SNMP.SnmpProgress>(ReportProgressSNMP);

            IDictionary<string, IDictionary<string, string>> snmpResultsSm = await cache.MemoizeAsync(nameof(snmp.GetMultipleDeviceOidsAsync) + "SMs", () => snmp.GetMultipleDeviceOidsAsync(onlineSmIpAddresses, progressIndicator, SNMP.OIDs.smAirDelayNs, SNMP.OIDs.smFrequencyHz)); // If we ever want to grab filters, but right now cant check vlan via snmp? SNMP.OIDs.filterPPPoE, SNMP.OIDs.filterAllIpv4, SNMP.OIDs.filterAllIpv6, SNMP.OIDs.filterArp, SNMP.OIDs.filterAllOther, SNMP.OIDs.filterDirection);
            IDictionary<string, IDictionary<string, string>> snmpResultsAp = await cache.MemoizeAsync(nameof(snmp.GetMultipleDeviceOidsAsync) + "APs", () => snmp.GetMultipleDeviceOidsAsync(onlineApIpAddresses, progressIndicator, SNMP.OIDs.sysContact)); // If we ever want to grab filters, but right now cant check vlan via snmp? SNMP.OIDs.filterPPPoE, SNMP.OIDs.filterAllIpv4, SNMP.OIDs.filterAllIpv6, SNMP.OIDs.filterArp, SNMP.OIDs.filterAllOther, SNMP.OIDs.filterDirection);

            // Just display which aps we found (debugging issue with duplicates)
            onlineStatisticsFromApi.Where(stat => stat.mode == DeviceMode.ap.ToString()).OrderBy(ap => ap.name).ForEach(ap => Console.WriteLine($"{ap.name} Detected with MAC {ap.mac}"));

            Dictionary<ESN, AccessPointRadioInfo> apInfo = await cache.Memoize(nameof(GenerateAllAccessPointInfo),
                () => GenerateAllAccessPointInfo(onlineDevicesFromApi, onlineStatisticsFromApi, snmpResultsAp));

            List<SubscriberRadioInfo> smInfo = await cache.Memoize(nameof(GenerateAllSmInfo),
                () => GenerateAllSmInfo(onlineDevicesFromApi, onlineStatisticsFromApi, snmpResultsSm, apInfo));

            var x = new Output.PPTX.Manager(smInfo, apInfo, cache);

            // Output to various ways.
            OutputXLSX(smInfo, apInfo);
            OutputKMZ(towersFromApi, apInfo, smInfo);
            OutputPTPPRJ(towersFromApi, apInfo, smInfo);

            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }
        
        private static List<SubscriberRadioInfo> GenerateAllSmInfo(Dictionary<ESN, CnDevice> onlineDevicesFromApi, IEnumerable<CnStatistics> onlineStatisticsFromApi, IDictionary<string, IDictionary<string, string>> snmpResultsSm, Dictionary<ESN, AccessPointRadioInfo> apInfo)
        {
            return onlineStatisticsFromApi
                .Where(dev => dev.mode == DeviceMode.sm.ToString() && snmpResultsSm.Keys.Contains(onlineDevicesFromApi[dev.mac].ip))
                .DistinctBy(sm => sm.mac)
                .Select((smStat) => GenerateSmRadioInfo(
                    apDevice: onlineDevicesFromApi[smStat.ap_mac],
                    apInfo: apInfo[smStat.ap_mac],
                    smDevice: onlineDevicesFromApi[smStat.mac],
                    smStats: smStat,
                    smSnmp: snmpResultsSm[onlineDevicesFromApi[smStat.mac].ip])).ToList();
        }

        private static Dictionary<ESN, AccessPointRadioInfo> GenerateAllAccessPointInfo(Dictionary<ESN, CnDevice> onlineDevicesFromApi, IEnumerable<CnStatistics> onlineStatisticsFromApi, IDictionary<string, IDictionary<string, string>> snmpResultsAp)
        {
            return onlineStatisticsFromApi.Where(stat => stat.mode == DeviceMode.ap.ToString()).ToDictionary(
                ap => (ESN)ap.mac,
                ap => GenerateAccessPoint(
                    ap,
                    onlineDevicesFromApi,
                    snmpResultsAp[onlineDevicesFromApi[ap.mac].ip][SNMP.OIDs.sysContact]
                    ));
        }

        // TODO: This should be split off into a seperate project cli, as it doesn't really belong in "reporting",
        // or maybe this project will be more than just reporting, haven't decided.

        /// <summary>
        /// Remove all devices that have been offline for more days than the given number of days.
        /// </summary>
        /// <param name="cnManager"></param>
        /// <param name="daysBeforeDeleted"></param>
        /// <returns></returns>
        private static async Task DeleteOldOfflineSMs(cnMaestroAPI.Manager cnManager, int daysBeforeDeleted)
        {
            var offlineDevicesOnly = await cnManager.GetMultipleDevicesAsync("&fields=name%2Cstatus%2Cstatus_time%2Cmac&status=offline&type=pmp");
            offlineDevicesOnly = offlineDevicesOnly.Where((cn) => double.Parse(cn.status_time) > TimeSpan.FromDays(daysBeforeDeleted).TotalSeconds).ToList();

            Console.WriteLine($"Removing Devices offline {daysBeforeDeleted}+ Days. (Total: {offlineDevicesOnly.Count()})");

            Console.WriteLine("Press Any Key to begin removals...");
            Console.ReadLine();
            foreach (var cn in offlineDevicesOnly)
            {
                Console.WriteLine($"{cn.mac}: {cn.name} {cn.status} for {TimeSpan.FromSeconds(double.Parse(cn.status_time)).ToString()} ({cn.status_time})");
                var delResult = await cnManager.DeleteDeviceAsync(cn.mac);
                Console.WriteLine($"  - Deletion: {delResult}");
            };
        }

        #region Output Reports
        private static void OutputPTPPRJ(IList<CnTower> towers, Dictionary<ESN, AccessPointRadioInfo> apInfo, List<SubscriberRadioInfo> subscriberInformation)
        {
            // Export to PTPPRJ
            var outputPTPPRJ = new Output.PTPPRJ.Manager(_generalConfig.GetSection("outputs:ptpprj"),
                subscriberInformation,
                towers.Select(tower => new KeyValuePair<string, CnLocation>(tower.name, tower.location)),
                apInfo.Values.ToList());

            outputPTPPRJ.Generate();
            outputPTPPRJ.Save();
        }

        private static void OutputXLSX(List<SubscriberRadioInfo> subscriberInformation, Dictionary<ESN, AccessPointRadioInfo> apInformation)
        {
            // Export to XLSX
            var outputXLSX = new Output.XLSX.Manager(_generalConfig.GetSection("outputs:xlsx"));
            outputXLSX.Generate(subscriberInformation, apInformation);
            outputXLSX.Save();
        }

        private static void OutputKMZ(IList<CnTower> towers, Dictionary<ESN, AccessPointRadioInfo> apInfo, List<SubscriberRadioInfo> subscriberInformation)
        {

            // Find our AP Bands
            var bands = apInfo.Values.DistinctBy(a => a.Channel.ToString()[0]).Select(a => a.Channel.ToString()[0]).ToArray();

            // Generate each bands KMZ
            foreach (char band in bands)
            {
                // Export to KMZ
                var outputKML = new Output.KML.Manager(
                    configSection: _generalConfig.GetSection("outputs:kml"),
                    subscribers: subscriberInformation,
                    towers: towers.Select(tower => new KeyValuePair<string, CnLocation>(tower.name, tower.location)),
                    accesspoints: apInfo.Values.Where(a => a.Channel.ToString()[0] == band).ToList()
                    );
                outputKML.GenerateKML();
                outputKML.Save($"{band}Ghz");
            }
        }
        #endregion

        private static AccessPointRadioInfo GenerateAccessPoint(CnStatistics ap, IDictionary<ESN, CnDevice> cnDevices, string sysContact)
        {
            var cnrs = cnManager.GetDevicePerfAsync(ap.mac, DateTime.Now.AddDays(-30), DateTime.Now).GetAwaiter().GetResult();
            var cnalarms = cnManager.GetDeviceAlarmsHistoryAsync(ap.mac, DateTime.Now.AddDays(-30), DateTime.Now, cnMaestroAPI.JsonType.CnSeverity.major).GetAwaiter().GetResult();

            Dictionary<string, CnFixedRadioPerformance> cnradios = cnrs.ToDictionary(r => r.timestamp, r => r.radio);

            var thisAp = cnDevices[ap.mac];

            var apRI = new AccessPointRadioInfo()
            {
                Name = ap.name,
                Hardware = thisAp.product,
                Esn = ap.mac,
                IP = thisAp.ip,
                ConnectedSMs = ap.connected_sms,
                Lan = ap.lan_status,
                Channel = Double.Parse(ap.radio.frequency),
                ColorCode = Byte.Parse(ap.radio.color_code),
                SyncState = ap.radio.sync_state,
                TxPower = ap.radio.tx_power ?? 0,
                Tower = ap.tower,
                Statistics = cnradios.Where(rs => rs.Value is not null).Select(rs => new AccessPointStatistic { 
                    TimeStamp = rs.Key,
                    DownlinkThroughput = (double)rs.Value.dl_throughput, 
                    DownlinkUtilization = (double)rs.Value.dl_frame_utilization, 
                    UplinkThroughput = (double)rs.Value.ul_throughput, 
                    UplinkUtilization = (double)rs.Value.ul_frame_utilization
                }),
                Azimuth = 0,
                Downtilt = 0,
                Uptime = TimeSpan.FromSeconds(Double.Parse(ap.status_time)),
                Alarms = cnalarms
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
            _ = Double.TryParse(smSnmp[SNMP.OIDs.smFrequencyHz], out double smFrequencyHz);
            _ = Int32.TryParse(smSnmp[SNMP.OIDs.smAirDelayNs], out int smAirDelayNs);

            double smDistanceM = RFCalc.MetersFromAirDelay(smAirDelayNs, smFrequencyHz, false);

            // If we have smGain from cnMaestro let's use it if not fall back to our configured value.
            _ = Int32.TryParse(smStats.gain, out int smGain);
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
                APEsn = apDevice.mac,
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
                DownlinkModulation = smStats.radio.dl_modulation,
                UplinkModulation = smStats.radio.ul_modulation,
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
    }
}