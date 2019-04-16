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

namespace CambiumSignalValidator
{
    internal partial class Program
    {
        private static cnMaestro.Manager cnManager { get; set; }
        private static cnMaestro.Settings cnMaestroConf = new cnMaestro.Settings();
        private static CambiumSNMP.Settings snmpConf = new CambiumSNMP.Settings();
        private static RadioConfig cambium;

        private static async Task Main(string[] args)
        {
            fetchConfiguration(); // Load our appSettings

            // Currently the program is only being used for testing various functions we will eventually have a real workflow.
            //TODO: Filtering change to KVP pair as doing it with strings is nasty

            cnManager = new cnMaestro.Manager(cnMaestroConf);

            await cnManager.ConnectAsync();
            var cnApi = new cnMaestro.Api(cnManager);
            try
            {
                var Towers = await cnApi.GetTowersAsync(cnMaestroConf.Network); // TODO: Read network from config

                //Currently Filtered to only a tower but set to "" to grab all devices.
                // TODO: we can add a fields= filter so we can reduce how much we're pulling from API since we don't need much
                // also we can do the URL Encoding for the towers automatically.
                var AllDeviceStats = cnApi.GetMultipleDevStatsAsync("tower=Atrium");
                var AllDevices = cnApi.GetMultipleDevicesAsync("tower=Atrium");
                Task.WaitAll(AllDevices, AllDeviceStats);

                var Devices = AllDevices.Result.Where(dev => dev.status == "online").ToDictionary(dev => dev.mac);
                var APstats = AllDeviceStats.Result.Where(dev => dev.mode == "ap" && dev.status == "online").ToDictionary(ap => ap.mac);

                ConcurrentBag<SubscriberRadioInfo> finalSubResults = new ConcurrentBag<SubscriberRadioInfo>();

                var snmp = new CambiumSNMP.Manager(snmpConf.Community, snmpConf.Version, snmpConf.Retries);

                // TODO: move this routine into a Task so it can be threaded.
                foreach (var thisSmStats in AllDeviceStats.Result.Where(dev => dev.mode == "sm" && dev.status == "online"))
                {
                    // To get the IP We have to look it up in the devices list (stats has type but not ip, and vice versa).
                    var thisSM = AllDevices.Result.Where(dev => dev.mac == thisSmStats.mac).Single();
                    
                    // We need to grab airdelay from SNMP
                    var snmpSm = snmp.GetOids(thisSM.ip, OIDs.smAirDelayNs, OIDs.smFrequencyHz);
                    if (snmpSm == null)
                    {
                        Console.WriteLine("SNMP Error: " + thisSM.ip);
                        continue;
                    }

                    Double.TryParse(snmpSm[OIDs.smFrequencyHz], out double smFrequencyHz);
                    Int32.TryParse(snmpSm[OIDs.smAirDelayNs], out int smAirDelayNs);

                    double smDistanceM = RFCalc.MetersFromAirDelay(smAirDelayNs, smFrequencyHz, false);

                    var thisAPModel = Devices[thisSmStats.ap_mac].product;
                    var thisApTx = APstats[thisSmStats.ap_mac].radio.tx_power;
                    var thisAPName = Devices[thisSmStats.ap_mac].name;

                    var smFSPL = RFCalc.FreeSpacePathLoss(smDistanceM, smFrequencyHz);

                    // smEPL === The power transmitted from the AP and what we expect to see on the SM
                    var smEPL = RFCalc.EstimatedPowerLevel(
                        smDistanceM,
                        smFrequencyHz,
                        0, 
                        Tx: cambium.Types[thisAPModel].Radio(thisApTx, 16),
                        Rx: cambium.Types[thisSM.product].Radio(thisSmStats.radio.tx_power));

                    // apEPL === The power transmitted from the SM and what we expect to see on the AP
                    var apEPL = RFCalc.EstimatedPowerLevel(
                        smDistanceM,
                        smFrequencyHz,
                        0,
                        Tx: cambium.Types[thisSM.product].Radio(thisSmStats.radio.tx_power),
                        Rx: cambium.Types[thisAPModel].Radio(thisApTx, 16));

                    finalSubResults.Add(new SubscriberRadioInfo()
                    {
                        Name = thisSM.name,
                        Esn = thisSM.mac,
                        APName = thisAPName,
                        DistanceM = (int)smDistanceM,
                        IP = thisSM.ip,
                        Model = thisSM.product,
                        SmEPL = Math.Round(smEPL, 2),
                        SmAPL = thisSmStats.radio.dl_rssi ?? -1,
                        ApModel = thisAPModel,
                        ApEPL = Math.Round(apEPL, 2),
                        ApAPL = thisSmStats.radio.ul_rssi ?? -1,
                        APTxPower = thisApTx ?? cambium.Types[thisAPModel].MaxTransmit,
                        SMTxPower = thisSmStats.radio.tx_power ?? cambium.Types[thisSM.product].MaxTransmit,
                        SMMaxTxPower = cambium.Types[thisSM.product].MaxTransmit
                    });

                    Console.WriteLine($"Found Device: {thisSM.name} - SM PL Diff {Math.Abs((double)smEPL - (double)thisSmStats.radio.dl_rssi)} AP PL Diff: {Math.Abs((double)smEPL - (double)thisSmStats.radio.ul_rssi)}");
                }

                SaveCSV(finalSubResults.ToList(), "output.xlsx");
            }
            catch (WebException e)
            {
                Console.WriteLine(e.Message);
            }
            Console.WriteLine("Done");
            Console.ReadLine();
        }

        public static void SaveCSV(IEnumerable<SubscriberRadioInfo> data, string OutputFile)
        {
            using (var ep = new ExcelPackage())
            {
                var ew = ep.Workbook.Worksheets.Add("450 Devices");
                ew.Cells["A1"].LoadFromCollection<SubscriberRadioInfo>(data, true);
                ew.Cells[ew.Dimension.Address].AutoFitColumns();
                ep.SaveAs(new FileInfo(OutputFile));
            }
        }

        private static void fetchConfiguration()
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