using cnMaestroAPI.cnDataType;
using cnMaestroReporting.Domain;
using cnMaestroReporting.Prometheus.Entities;
using MemoizeRedis;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommonCalculations;
using cnSNMP;

namespace cnMaestroReporting.CLI
{
    static class Program
    {
        //private static cnMaestroAPI.Manager CnManager { get; set; } // Our access to cnMaestro
        private static RadioConfig? _cambiumRadios; // Holds the model configurations we use for various cambium devices.
        private static IConfigurationRoot? _generalConfig; // Holds the general config for the app and plugins
        private static cnMaestroAPI.Manager? _cnAPIManager;
        private static cnSNMP.Manager? _cnSNMPManager; 
        private static EngageIP.EIPSettings? _eipSettings;

        private static async Task Main()
        {
            _generalConfig = FetchConfiguration(); // Load our appSettings into generalConfig
            _cnAPIManager = new cnMaestroAPI.Manager();
            _cnSNMPManager = new cnSNMP.Manager(_generalConfig.GetRequiredSection("snmp"));
            _eipSettings = _generalConfig.GetSection("engageip").Get<EngageIP.EIPSettings>();
            var includeOffline = _generalConfig.GetValue<bool>("includeOffline");

            Console.WriteLine("Getting cnMaestro Towers...");
            IList<CnTower> towersFromApi = await Memoize.WithRedisAsync(() => _cnAPIManager.GetTowersAsync());
            Console.WriteLine("Getting cnMaestro Statistics...");
            IList<CnStatistics> deviceStatisticsFromApi = await Memoize.WithRedisAsync(() => _cnAPIManager.GetMultipleDevStatsAsync(""));
            Console.WriteLine("Getting cnMaestro Devices...");
            IList<CnDevice> devicesFromApi = await Memoize.WithRedisAsync(() => _cnAPIManager.GetMultipleDevicesAsync(""));
            Console.WriteLine("Getting Prometheus Data...");
            PromNetworkData? promNetworkData = await Memoize.WithRedisAsync(() => getPrometheusData());

            //Dictionary of all Devices so we can lookup by mac address
            Console.WriteLine("Filtering Devices to Online Devices/Statistics...");
            Dictionary<ESN, CnDevice> onlineDevicesFromApi = devicesFromApi.DistinctBy(dev => dev.mac).Where(dev => dev.status == "online").ToDictionary(dev => (ESN)dev.mac);
            IEnumerable<CnStatistics> onlineStatisticsFromApi = deviceStatisticsFromApi.Where(devStat => onlineDevicesFromApi.ContainsKey(devStat.mac));

            Dictionary<ESN, CnDevice> offlineDevicesFromApi = new();
            List<CnStatistics> offlineStatisticsFromApi = new();
            if (includeOffline)
            {
                offlineDevicesFromApi = devicesFromApi.DistinctBy(dev => dev.mac).Where(dev => dev.status == "offline").ToDictionary(dev => (ESN)dev.mac);
                offlineStatisticsFromApi = deviceStatisticsFromApi.Where(devStat => offlineDevicesFromApi.ContainsKey(devStat.mac)).ToList();
            }

            // Build a list of online ips for snmp checks
            Console.WriteLine("Building Online Device Arrays...");
            string[] onlineSmIpAddresses = onlineStatisticsFromApi.DistinctBy(devStat => devStat.mac).Where(devStat => devStat.mode == DeviceMode.sm.ToString()).Select(dev => onlineDevicesFromApi[dev.mac].ip).ToArray();
            string[] onlineApIpAddresses = onlineStatisticsFromApi.DistinctBy(devStat => devStat.mac).Where(devStat => devStat.mode == DeviceMode.ap.ToString()).Select(dev => onlineDevicesFromApi[dev.mac].ip).ToArray();

            // Let's get snmp values that we can't get from the cnMaestroAPI Currently.
            Console.WriteLine("Polling SNMP from SMs...");
            cnSNMP.CnSnmpResult[]? snmpResultsSm = _cnSNMPManager.GetMultipleDeviceOids(onlineSmIpAddresses, cnSNMP.OIDs.smAirDelayNs, cnSNMP.OIDs.smFrequencyHz); 
            Console.WriteLine("Polling SNMP from APs...");
            cnSNMP.CnSnmpResult[]? snmpResultsAp = _cnSNMPManager.GetMultipleDeviceOids(onlineApIpAddresses, cnSNMP.OIDs.sysContact);

            Console.WriteLine("Build Our Final DTO for AccessPointInfo...");
            IDictionary<ESN, AccessPointRadioInfo> apInfo = GenerateAllAccessPointInfo(onlineDevicesFromApi, onlineStatisticsFromApi, snmpResultsAp);

            Console.WriteLine("Build Our Final DTO for SubscriberRadioInfo...");
            List<SubscriberRadioInfo> smInfo = GenerateAllSmInfo(onlineDevicesFromApi, onlineStatisticsFromApi, offlineDevicesFromApi, offlineStatisticsFromApi, snmpResultsSm, apInfo, towersFromApi);

            // Output to various ways.
            OutputPPTX(smInfo, apInfo, promNetworkData);
            OutputXLSX(towersFromApi, smInfo, apInfo, promNetworkData);
            OutputKMZ(towersFromApi, apInfo, smInfo, promNetworkData);
            OutputPTPPRJ(towersFromApi, apInfo, smInfo);

            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        private static async Task<PromNetworkData> getPrometheusData()
        {
            var ApDl30d = await Prometheus.API.QueryAllDlTotal("30d");
            var ApUl30d = await Prometheus.API.QueryAllUlTotal("30d");

            var ApDl7d = await Prometheus.API.QueryAllDlTotal("7d");
            var ApUl7d = await Prometheus.API.QueryAllUlTotal("7d");

            var ApDl1d = await Prometheus.API.QueryAllDlTotal("1d");
            var ApUl1d = await Prometheus.API.QueryAllUlTotal("1d");

            var ApDlTp = await Prometheus.API.QueryAllDlMaxThroughput("30d");
            var ApUlTp = await Prometheus.API.QueryAllUlMaxThroughput("30d");

            var APMPGain = await Prometheus.API.QueryAllAvgMultiplexingGain("30d");
            var APGrpSize = await Prometheus.API.QueryAllAvgGroupSize("30d");

            return new PromNetworkData(ApDl30d, ApUl30d, ApDlTp, ApUlTp, ApDl7d, ApUl7d, ApDl1d, ApUl1d, APMPGain, APGrpSize);
        }

        private static List<SubscriberRadioInfo> GenerateAllSmInfo(Dictionary<ESN, CnDevice> onlineDevicesFromApi, IEnumerable<CnStatistics> onlineStatisticsFromApi, Dictionary<ESN, CnDevice> offlineDevicesFromApi, IEnumerable<CnStatistics> offlineStatisticsFromApi, cnSNMP.CnSnmpResult[]? snmpResultsSm, IDictionary<ESN, AccessPointRadioInfo> apInfo, IList<CnTower> towerInfo)
        {
            ArgumentNullException.ThrowIfNull(snmpResultsSm);

            ConcurrentBag<SubscriberRadioInfo> outputBag = new();

            Console.WriteLine("Generating All SubscriberRadioInfo Information");
            onlineStatisticsFromApi
                .Where(stat => stat.mode == DeviceMode.sm.ToString())
                .DistinctBy(stat => stat.mac)
                .AsParallel()
                .ForAll(stat =>
                {
                    var thisSnmpResults = snmpResultsSm.Where(s => s.ip == onlineDevicesFromApi[stat.mac].ip).First();
                    if (thisSnmpResults is not null && thisSnmpResults.oidResult is not null)
                    {
                        outputBag.Add(GenerateSmRadioInfo(
                            apDevice: onlineDevicesFromApi[stat.ap_mac],
                            apInfo: apInfo[stat.ap_mac],
                            smDevice: onlineDevicesFromApi[stat.mac],
                            smStats: stat,
                            smSnmp: thisSnmpResults.oidResult, 
                            tower: towerInfo.Where(x => x.name == apInfo[stat.ap_mac].Tower).FirstOrDefault()));
                    }
                    else
                    {
                        // We have an SM without a SNMP Response

                        Console.WriteLine($"SM Without SNMP Response: {stat.mac}");
                        outputBag.Add(GenerateSmRadioInfo(
                            apDevice: onlineDevicesFromApi[stat.ap_mac],
                            apInfo: apInfo[stat.ap_mac],
                            smDevice: onlineDevicesFromApi[stat.mac],
                            smStats: stat,
                            smSnmp: null,
                            tower: towerInfo.Where(x => x.name == apInfo[stat.ap_mac].Tower).FirstOrDefault()));
                    }
                });

            foreach (var stat in offlineStatisticsFromApi.Where(stat => stat.mode == DeviceMode.sm.ToString()).DistinctBy(stat => stat.mac))
            {
                Console.WriteLine($"Offline SM: {stat.mac}, AP Online: {onlineDevicesFromApi.ContainsKey(stat.ap_mac)}");
                if (onlineDevicesFromApi.ContainsKey(stat.ap_mac))
                    outputBag.Add(GenerateSmRadioInfo(
                            apDevice: onlineDevicesFromApi[stat.ap_mac], // AP Has to be online to be handled
                            apInfo: apInfo[stat.ap_mac],
                            smDevice: offlineDevicesFromApi[stat.mac], // This is an offline SM
                            smStats: stat,
                            smSnmp: null,
                            tower: towerInfo.Where(x => x.name == apInfo[stat.ap_mac].Tower).FirstOrDefault()));
            };

            var eip = new EngageIP.Session(_eipSettings);

            List<SubscriberRadioInfo> output = outputBag?.ToList() ?? new List<SubscriberRadioInfo>();
            for(var i = 0; i < output.Count(); i++)
            {
                Console.WriteLine($"Looking up EIP for {output[i].Name} ({output[i].Esn})");
                var eipResult = eip.lookupPackageByUserOrEsn(output[i].Esn);
                if (eipResult is not null && eipResult.HasValue)
                {
                    var updatedWithEip = output[i];
                    //eipResult.Value.ClientName;
                    //eipResult.Value.Owner;
                    //eipResult.Value.Answer;
                    updatedWithEip.EIPAccount = eipResult.Value.ClientName;
                    updatedWithEip.EIPService = eipResult.Value.Service;
                    updatedWithEip.EIPValue = eipResult.Value.Value;
                    //eipResult.Value.ExpDate;
                    //eipResult.Value.IsBusinessAccount;

                    output[i] = updatedWithEip;
                }
            }

            return output.ToList();
        }

        private static IDictionary<ESN, AccessPointRadioInfo> GenerateAllAccessPointInfo(Dictionary<ESN, CnDevice> onlineDevicesFromApi, IEnumerable<CnStatistics> onlineStatisticsFromApi, cnSNMP.CnSnmpResult[]? snmpResultsAp)
        {
            ArgumentNullException.ThrowIfNull(snmpResultsAp);
            ConcurrentDictionary<ESN, AccessPointRadioInfo> outputBag = new();

            Console.WriteLine("Generating All AccessPointRadioInfo Information");
            onlineStatisticsFromApi
                .Where(stat => stat.mode == DeviceMode.ap.ToString())
                .DistinctBy(stat => stat.mac)
                .AsParallel()
                .ForAll(stat =>
                {
                    var thisSnmpResults = snmpResultsAp.Where(s => s.ip == onlineDevicesFromApi[stat.mac].ip).First();
                    var sysContact = thisSnmpResults.oidResult?.Where(o => o.oid == cnSNMP.OIDs.sysContact).First();
                    outputBag.TryAdd((ESN)stat.mac, GenerateAccessPoint(stat, onlineDevicesFromApi, sysContact?.value ?? "").GetAwaiter().GetResult());
                });

            return outputBag;
        }

        // TODO: This should be split off into a seperate project cli, as it doesn't really belong in "reporting",
        // or maybe this project will be more than just reporting, haven't decided.

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
        private static void OutputPTPPRJ(IList<CnTower> towers, IDictionary<ESN, AccessPointRadioInfo> apInfo, List<SubscriberRadioInfo> subscriberInformation)
        {
            // Export to PTPPRJ
            var outputPTPPRJ = new Output.PTPPRJ.Manager(
                subscriberInformation,
                towers.Select(tower => new KeyValuePair<string, CnLocation>(tower.name, tower.location)),
                apInfo.Values.ToList());

            outputPTPPRJ.Generate();
            outputPTPPRJ.Save();
        }

        private static void OutputPPTX(List<SubscriberRadioInfo> smInfo, IDictionary<ESN, AccessPointRadioInfo> apInfo, PromNetworkData promNetworkData)
        {
            var outputPPTX = new Output.PPTX.Manager(smInfo, apInfo, promNetworkData);
        }


        private static void OutputXLSX(IList<CnTower> towers, List<SubscriberRadioInfo> subscriberInformation, IDictionary<ESN, AccessPointRadioInfo> apInformation, PromNetworkData promNetworkData)
        {
            ArgumentNullException.ThrowIfNull(_generalConfig);

            // Export to XLSX
            var outputXLSX = new Output.XLSX.Manager();
            outputXLSX.Generate(subscriberInformation, apInformation, promNetworkData, towers.Select(tower => new KeyValuePair<string, CnLocation>(tower.name, tower.location)));
            outputXLSX.Save();
        }

        private static void OutputKMZ(IList<CnTower> towers, IDictionary<ESN, AccessPointRadioInfo> apInfo, List<SubscriberRadioInfo> subscriberInformation, PromNetworkData promNetworkData)
        {
            ArgumentNullException.ThrowIfNull(_generalConfig);

            // Find our AP Bands
            var bands = apInfo.Values.DistinctBy(a => a.Channel.ToString()[0]).Select(a => a.Channel.ToString()[0]).ToArray();

            var towersKVPArray = towers.Select(tower => new KeyValuePair<string, CnLocation>(tower.name, tower.location));
            // Generate each bands KMZ
            foreach (char band in bands)
            {
                // Export to KMZ
                var outputKML = new Output.KML.Manager(
                    subscribers: subscriberInformation,
                    towers: towersKVPArray,
                    accesspoints: apInfo.Values.Where(a => a.Channel.ToString()[0] == band).ToList()
                    );

                var outputKML2 = new Output.KML.Manager(
                    subscribers: subscriberInformation,
                    towers: towersKVPArray,
                    accesspoints: apInfo.Values.Where(a => a.Channel.ToString()[0] == band).ToList()
                    );

                outputKML.GenerateKML(false, promNetworkData, towersKVPArray);
                outputKML2.GenerateKML(true, promNetworkData, towersKVPArray);
                outputKML.Save($"{band}Ghz");
                outputKML2.Save($"{band}Ghz Utilization");
            }
        }
        #endregion

        private static async Task<AccessPointRadioInfo> GenerateAccessPoint(CnStatistics ap, IDictionary<ESN, CnDevice> cnDevices, string sysContact)
        {
            ArgumentNullException.ThrowIfNull(_cnAPIManager);

            // We're going to use the start of the date 30 days ago, and the end of yesterday, this also helps with caching and nice round dates, maybe we should shift to have this global for 
            DateTime startTime = (DateOnly.FromDateTime(DateTime.Now).AddDays(-30)).ToDateTime(TimeOnly.MinValue);
            DateTime lastNight = (DateOnly.FromDateTime(DateTime.Now).AddDays(-1)).ToDateTime(TimeOnly.MaxValue);

            var cnrs = await _cnAPIManager.GetDevicePerfAsync(ap.mac, startTime, lastNight);
            var cnalarms = await _cnAPIManager.GetDeviceAlarmsHistoryAsync(ap.mac, startTime, lastNight, cnMaestroAPI.JsonType.CnSeverity.major);

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
                    DownlinkThroughput = (double)(rs.Value.dl_throughput ?? 0), 
                    DownlinkUtilization = (double)(rs.Value.dl_frame_utilization ?? 0), 
                    UplinkThroughput = (double)(rs.Value.ul_throughput ?? 0), 
                    UplinkUtilization = (double)(rs.Value.ul_frame_utilization ?? 0)
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
        public static SubscriberRadioInfo GenerateSmRadioInfo(CnDevice apDevice, AccessPointRadioInfo apInfo, CnDevice smDevice, CnStatistics smStats, cnSNMP.CnOidResult[]? smSnmp, CnTower? tower)
        {
            ArgumentNullException.ThrowIfNull(_cambiumRadios);
            ArgumentNullException.ThrowIfNull(tower);

            double smDistanceM = -1;
            int smAirDelayNs = -1;
            double smFrequencyHz = -1;

            if (smSnmp is not null)
            {
                _ = Double.TryParse(smSnmp.Where(s => s.oid == OIDs.smFrequencyHz).FirstOrDefault()?.value, out smFrequencyHz);
                _ = Int32.TryParse(smSnmp.Where(s => s.oid == OIDs.smAirDelayNs).FirstOrDefault()?.value, out smAirDelayNs);
                smDistanceM = RFCalc.MetersFromAirDelay(smAirDelayNs, smFrequencyHz, false);
            }


            // If we have smGain from cnMaestro let's use it if not fall back to our configured value.
            _ = Int32.TryParse(smStats.gain, out int smGain);
            if (smGain <= 0)
                smGain = _cambiumRadios.SM[smDevice.product].AntennaGain;

            // Odd irregularity where cnMaestro sends a -30 let's assume max Tx since it's obviously transmitting as we have a SM to calculate on the panel.
            var apTx = apInfo.TxPower;
            if (apTx <= 0)
                apTx = _cambiumRadios.AP[apDevice.product].MaxTransmit;

            // smEPL === The power transmitted from the AP and what we expect to see on the SM
            var smEPL = smFrequencyHz > 0 ? RFCalc.EstimatedPowerLevel(
                smDistanceM,
                smFrequencyHz,
                0,
                Tx: _cambiumRadios.AP[apDevice.product].Radio(apTx),
                Rx: _cambiumRadios.SM[smDevice.product].Radio(smStats.radio.tx_power, smGain)) : 0;

            // apEPL === The power transmitted from the SM and what we expect to see on the AP
            var apEPL = smFrequencyHz > 0 ? RFCalc.EstimatedPowerLevel(
                smDistanceM,
                smFrequencyHz,
                0,
                Tx: _cambiumRadios.SM[smDevice.product].Radio(smStats.radio.tx_power, smGain),
                Rx: _cambiumRadios.AP[apDevice.product].Radio(apTx)) : 0;

            //Console.WriteLine($"Generated SM DeviceInfo: {smDevice.name}");
            
            var GeoDistance = GeoCalc.GeoDistance((double)smDevice.location.coordinates[1], (double)smDevice.location.coordinates[0], (double)tower.location.coordinates[1], (double)tower.location.coordinates[0]);

            return new SubscriberRadioInfo()
            {
                Name = smDevice.name,
                Esn = smDevice.mac,
                Online = smFrequencyHz > 0,
                Tower = apDevice.tower,
                Firmware = smDevice.software_version,
                Latitude = smDevice.location.coordinates[1],
                Longitude = smDevice.location.coordinates[0],
                SmGain = smGain,
                APBand = smFrequencyHz > 3000000 && smFrequencyHz < 4500000 ? "3" : smFrequencyHz < 3000000 ? "0" : "5",
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
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile("radiotypes.json", optional: false, reloadOnChange: false).Build();

            // Setup config for the main eventloop
            _cambiumRadios = configuration.Get<RadioConfig>();

            // Return overall config so we can pass it to plugins
            return configuration;
        }
    }
}