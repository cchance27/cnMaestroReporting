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
using Utils;
using static cnMaestroReporting.Prometheus.PrometheusExtension;
using System.Collections.Immutable;
using CnMaestroWebAPI = cnmWebApi;

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
        private static CnMaestroWebAPI.Manager? _cnWeb;
        private static int days;
        private static bool includeOffline;
        private static bool pollEngageIP;

        private static string[] onlineSmIpAddresses = Array.Empty<string>();
        private static string[] onlineApIpAddresses = Array.Empty<string>();

        private static async Task Main()
        {
            _generalConfig = FetchConfiguration(); // Load our appSettings into generalConfig
            _cnAPIManager = new cnMaestroAPI.Manager();
            _cnSNMPManager = new cnSNMP.Manager(_generalConfig.GetRequiredSection("snmp"));
            _cnWeb = new CnMaestroWebAPI.Manager(_generalConfig.GetSection("cnMaestroWeb").Get<CnMaestroWebAPI.CnMaestroWebConfig>());
            _eipSettings = _generalConfig.GetSection("engageip").Get<EngageIP.EIPSettings>();
            includeOffline = _generalConfig.GetValue<bool>("includeOffline");
            pollEngageIP = _generalConfig.GetValue<bool>("pollEngageIP");

            days = _generalConfig.GetValue<int>("days");

            //await DeleteOldOfflineSMs(_cnAPIManager, 90);
            //return;

            await _cnWeb.Login();

 
            Console.WriteLine("Getting cnMaestro Towers...");
            IList<CnTower> towersFromApi = await Memoize.WithRedisAsync(() => _cnAPIManager.GetTowersAsync());
            Console.WriteLine("Getting cnMaestro Statistics...");
            IList<CnStatistics> deviceStatisticsFromApi = await Memoize.WithRedisAsync(() => _cnAPIManager.GetMultipleDevStatsAsync(""));
            Console.WriteLine("Getting cnMaestro Devices...");
            IList<CnDevice> devicesFromApi = await Memoize.WithRedisAsync(() => _cnAPIManager.GetMultipleDevicesAsync(""));

            Console.WriteLine("Getting Prometheus Data...");
            PromNetworkData? promNetworkData = await Memoize.WithRedisAsync(() => getCurrentProm());

            Console.WriteLine($"Getting Previous Prometheus Data {days} ago {DateTime.Now.Subtract(System.TimeSpan.FromDays(days))}...");
            PromNetworkData? promNetworkDataPrevious = await Memoize.WithRedisAsync(() => getPreviousProm());

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
            onlineSmIpAddresses = onlineStatisticsFromApi.DistinctBy(devStat => devStat.mac).Where(devStat => devStat.mode == DeviceMode.sm.ToString()).Select(dev => onlineDevicesFromApi[dev.mac].ip).ToArray();
            onlineApIpAddresses = onlineStatisticsFromApi.DistinctBy(devStat => devStat.mac).Where(devStat => devStat.mode == DeviceMode.ap.ToString()).Select(dev => onlineDevicesFromApi[dev.mac].ip).ToArray();

            cnSNMP.CnSnmpResult[]? snmpResultsAp = await Memoize.WithRedisAsync(() => grabApSnmp());
            cnSNMP.CnSnmpResult[]? snmpResultsSm = await Memoize.WithRedisAsync(() => grabSmSnmp());
            
            Console.WriteLine("Build Our Final DTO for AccessPointInfo...");
            IDictionary<ESN, AccessPointRadioInfo> apInfo = GenerateAllAccessPointInfo(onlineDevicesFromApi, onlineStatisticsFromApi, snmpResultsAp);

            Console.WriteLine("Build Our Final DTO for SubscriberRadioInfo...");
            List<SubscriberRadioInfo> smInfo = await GenerateAllSmInfo(onlineDevicesFromApi, onlineStatisticsFromApi, offlineDevicesFromApi, offlineStatisticsFromApi, snmpResultsSm, apInfo, towersFromApi);

            // Add Data Usage to smInfo
            var smGbOfDataUsage = await _cnWeb.GetAllSMsGbDataUsage(smInfo.Select(x => x.Esn).ToList(), days);
            smInfo = smInfo.Select(sm => {
                sm.DLGBDataUsage = smGbOfDataUsage[sm.Esn]
                    .Where(day => day.Key >= DateOnly.FromDateTime(DateTime.Now.AddDays(-7)))
                    .Sum(x => { return x.Value.Downlink; });
                sm.ULGBDataUsage = smGbOfDataUsage[sm.Esn]
                    .Where(day => day.Key >= DateOnly.FromDateTime(DateTime.Now.AddDays(-7)))
                    .Sum(x => { return x.Value.Uplink; });

                return sm;
            }).ToList();

            // Output to various ways.
            //OutputPPTX(smInfo, apInfo, promNetworkData, promNetworkDataPrevious, smGbOfDataUsage);
            OutputXLSX(towersFromApi, smInfo, apInfo, promNetworkData, smGbOfDataUsage);
            //OutputKMZ(towersFromApi, apInfo, smInfo, promNetworkData);
            //OutputPTPPRJ(towersFromApi, apInfo, smInfo);

            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        #region SNMP Data
        private async static Task<cnSNMP.CnSnmpResult[]?> grabSmSnmp()
        {
            await Task.Delay(100);
            Console.WriteLine("Polling SNMP from SMs...");
            return _cnSNMPManager.GetMultipleDeviceOids(onlineSmIpAddresses, cnSNMP.OIDs.smAirDelayNs, cnSNMP.OIDs.smFrequencyHz);
        }

        private async static Task<cnSNMP.CnSnmpResult[]?> grabApSnmp()
        {
            await Task.Delay(100);
            Console.WriteLine("Polling SNMP from APs...");
            return _cnSNMPManager.GetMultipleDeviceOids(onlineApIpAddresses, cnSNMP.OIDs.sysContact, cnSNMP.OIDs.eirp);
        }
        #endregion

        #region Prometheus Data
        private static async Task<PromNetworkData> getCurrentProm() => await getPrometheusData(days, null);
        private static async Task<PromNetworkData> getPreviousProm() => await getPrometheusData(days, DateTime.Now.Subtract(System.TimeSpan.FromDays(days)));
        private static async Task<PromNetworkData> getPrometheusData(int days = 7, DateTime? endDateTime = null)
        {
            string timeframe = $"{days}d";

            var ApDl = await Prometheus.API.QueryAllDlTotal(timeframe, endDateTime);
            var ApUl = await Prometheus.API.QueryAllUlTotal(timeframe, endDateTime);

            var ApDlTp = await Prometheus.API.QueryAllDlMaxThroughput(timeframe, endDateTime);
            var ApUlTp = await Prometheus.API.QueryAllUlMaxThroughput(timeframe, endDateTime);

            var APMPGain = await Prometheus.API.QueryAllAvgMultiplexingGain(timeframe, endDateTime);
            var APGrpSize = await Prometheus.API.QueryAllAvgGroupSize(timeframe, endDateTime);
            
            var SMCount = await Prometheus.API.QueryAllMaxSMCount(timeframe, endDateTime);

            var SMDlMod = await Prometheus.API.QueryAllDlSMModulation(timeframe, endDateTime);

            return new PromNetworkData(ApDl, ApUl, ApDlTp, ApUlTp, APMPGain, APGrpSize, SMCount, SMDlMod);
        }
        #endregion

        private static async Task<List<SubscriberRadioInfo>> GenerateAllSmInfo(Dictionary<ESN, CnDevice> onlineDevicesFromApi, IEnumerable<CnStatistics> onlineStatisticsFromApi, Dictionary<ESN, CnDevice> offlineDevicesFromApi, IEnumerable<CnStatistics> offlineStatisticsFromApi, cnSNMP.CnSnmpResult[]? snmpResultsSm, IDictionary<ESN, AccessPointRadioInfo> apInfo, IList<CnTower> towerInfo)
        {
            ArgumentNullException.ThrowIfNull(snmpResultsSm);

            ConcurrentBag<SubscriberRadioInfo> outputBag = new();

            Console.WriteLine("Generating All SubscriberRadioInfo Information");
            onlineStatisticsFromApi
                .Where(stat => 
                    stat.mode == DeviceMode.sm.ToString() && 
                    apInfo[stat.ap_mac] is not null)
                .DistinctBy(stat => stat.mac)
                .AsParallel()
                .WithDegreeOfParallelism(5)
                .ForAll(stat =>
                {
                    var thisSnmpResults = snmpResultsSm.Where(s => s.ip == onlineDevicesFromApi[stat.mac].ip).FirstOrDefault();
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

            foreach (var stat in offlineStatisticsFromApi
                .Where(stat => 
                    stat.mode == DeviceMode.sm.ToString() && 
                    apInfo[stat.ap_mac] is not null)
                .DistinctBy(stat => stat.mac))
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

            // Non-Parralel Lookup in engageip to avoid flooding, would be better if we could just get a table report and then do local lookups.
            List<SubscriberRadioInfo> output = outputBag?.ToList() ?? new List<SubscriberRadioInfo>();
            for(var i = 0; i < output.Count(); i++)
            {
                var currentESN = output[i].Esn;
                if (pollEngageIP)
                {
                    Console.WriteLine($"Looking up EIP for {output[i].Name} ({currentESN})");
                    var eipResult = await Memoize.WithRedisAsync(() => eip.lookupPackageByUserOrEsn(currentESN));
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
                    var eirp = thisSnmpResults.oidResult?.Where(o => o.oid == cnSNMP.OIDs.eirp).First().value ?? "0";
                    if (eirp.StartsWith("SNMP")) 
                        eirp = "0";
                    outputBag.TryAdd((ESN)stat.mac, GenerateAccessPoint(stat, onlineDevicesFromApi, sysContact?.value ?? "", eirp, days).GetAwaiter().GetResult());
                });

            return outputBag;
        }

        // TODO: This should be split off into a seperate project cli, as it doesn't really belong in "reporting",
        // or maybe this project will be more than just reporting, haven't decided.

        private static async Task DeleteOldOfflineSMs(cnMaestroAPI.Manager cnManager, int daysBeforeDeleted)
        {
            var offlineDevicesOnly = await cnManager.GetMultipleDevicesAsync("&fields=name%2Cstatus%2Cstatus_time%2Cmac&status=offline&type=pmp");
            offlineDevicesOnly = offlineDevicesOnly.Where((cn) => double.Parse(cn.status_time) > System.TimeSpan.FromDays(daysBeforeDeleted).TotalSeconds).ToList();

            Console.WriteLine($"Removing Devices offline {daysBeforeDeleted}+ Days. (Total: {offlineDevicesOnly.Count()})");

            Console.WriteLine("Press Any Key to begin removals...");
            Console.ReadLine();
            foreach (var cn in offlineDevicesOnly)
            {
                Console.WriteLine($"{cn.mac}: {cn.name} {cn.status} for {System.TimeSpan.FromSeconds(double.Parse(cn.status_time)).ToString()} ({cn.status_time})");
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

        private static void OutputPPTX(List<SubscriberRadioInfo> smInfo, IDictionary<ESN, AccessPointRadioInfo> apInfo, PromNetworkData promNetworkData, PromNetworkData promNetworkDataPrevious, IImmutableDictionary<string, Dictionary<DateOnly, CnMaestroWebAPI.DataUsage>> smGbOfDataUsage)
        {
            var outputPPTX = new Output.PPTX.Manager(smInfo, apInfo, promNetworkData, promNetworkDataPrevious, smGbOfDataUsage, days);
        }


        private static void OutputXLSX(IList<CnTower> towers, List<SubscriberRadioInfo> subscriberInformation, IDictionary<ESN, AccessPointRadioInfo> apInformation, PromNetworkData promNetworkData, IImmutableDictionary<string, Dictionary<DateOnly, CnMaestroWebAPI.DataUsage>> smGbOfDataUsage)
        {
            ArgumentNullException.ThrowIfNull(_generalConfig);

            // Export to XLSX
            var outputXLSX = new Output.XLSX.Manager();
            outputXLSX.Generate(subscriberInformation, apInformation, promNetworkData, towers.Select(tower => new KeyValuePair<string, CnLocation>(tower.name, tower.location)), smGbOfDataUsage, days);
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

        private static async Task<AccessPointRadioInfo> GenerateAccessPoint(CnStatistics ap, IDictionary<ESN, CnDevice> cnDevices, string sysContact, string eirp, int days)
        {
            ArgumentNullException.ThrowIfNull(_cambiumRadios);
            ArgumentNullException.ThrowIfNull(_cnAPIManager);

            // We're going to use the start of the date X days ago, and the end of yesterday, this also helps with caching and nice round dates, maybe we should shift to have this global for 
            DateTime startTime = (DateOnly.FromDateTime(DateTime.Now).AddDays(-1 * days)).ToDateTime(TimeOnly.MinValue);
            DateTime lastNight = (DateOnly.FromDateTime(DateTime.Now).AddDays(-1)).ToDateTime(TimeOnly.MaxValue);

            var cnrs = await _cnAPIManager.GetDevicePerfAsync(ap.mac, startTime, lastNight);
            var cnalarms = await _cnAPIManager.GetDeviceAlarmsHistoryAsync(ap.mac, startTime, lastNight, cnMaestroAPI.JsonType.CnSeverity.major);

            Dictionary<string, CnFixedRadioPerformance> cnradios = cnrs.DistinctBy(x => x.timestamp).ToDictionary(r => r.timestamp, r => r.radio);

            var thisAp = cnDevices[ap.mac];

            var txPower = ap.radio?.tx_power ?? int.Parse(eirp) - _cambiumRadios.AP[thisAp.product].AntennaGain;

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
                TxPower = txPower,
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
                Uptime = System.TimeSpan.FromSeconds(Double.Parse(ap.status_time)),
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
            // TODO: Sometimes APs dont have towers, we need to handle these or drop them somewhere.

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
            var apTx = apInfo.TxPower > 0 ? apInfo.TxPower : _cambiumRadios.AP[apDevice.product].MaxTransmit;

            var smTx = smStats.radio.tx_power ?? _cambiumRadios.SM[smDevice.product].MaxTransmit;

            // smEPL === The power transmitted from the AP and what we expect to see on the SM
            var smEPL = smFrequencyHz > 0 ? RFCalc.EstimatedPowerLevel(
                smDistanceM,
                smFrequencyHz,
                0,
                Tx: _cambiumRadios.AP[apDevice.product].Radio(apTx),
                Rx: _cambiumRadios.SM[smDevice.product].Radio(smTx, smGain)) : 0;

            // apEPL === The power transmitted from the SM and what we expect to see on the AP
            var apEPL = smFrequencyHz > 0 ? RFCalc.EstimatedPowerLevel(
                smDistanceM,
                smFrequencyHz,
                0,
                Tx: _cambiumRadios.SM[smDevice.product].Radio(smTx, smGain),
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
                SmTxPower = smTx,
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
