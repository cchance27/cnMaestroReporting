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
                //var Networks = cnApi.GetNetworksTask();
                var Towers = await cnApi.GetTowersAsync("default");

                //Currently Filtered to only a tower but set to "" to grab all devices.
                // TODO: we can add a fields= filter so we can reduce how much we're pulling from API since we don't need much
                var AllDeviceStats = cnApi.GetMultipleDevStatsAsync("tower=Atrium");
                var AllDevices = cnApi.GetMultipleDevicesAsync("tower=Atrium");
                Task.WaitAll(AllDevices, AllDeviceStats);

                var Devices = AllDevices.Result.Where(dev => dev.status == "online").ToDictionary(dev => dev.mac);
                var APstats = AllDeviceStats.Result.Where(dev => dev.mode == "ap" && dev.status == "online").ToDictionary(ap => ap.mac);

                ConcurrentBag<SubscriberRadioInfo> finalSubResults = new ConcurrentBag<SubscriberRadioInfo>();

                var snmp = new CambiumSNMP.Manager(snmpConf.Community, 2);
                foreach (var thisSmStats in AllDeviceStats.Result.Where(dev => dev.mode == "sm" && dev.status == "online"))
                {
                    // To get the IP We have to look it up in the devices list (stats has type but not ip, and vice versa).
                    var thisSM = AllDevices.Result.Where(dev => dev.mac == thisSmStats.mac).Single();
                    
                    // We need to grab airdelay from SNMP
                    // TODO: maybe a seperate routine just to grab airdelay not everything, since we only need that
                    var snmpSm = snmp.GetCambiumSM(thisSM.ip);
                    if (snmpSm == null)
                    {
                        Console.WriteLine("SNMP Error: " + thisSM.ip);
                        continue;
                    }

                    double smDistanceM = (double)RFCalc.MetersFromAirDelay(snmpSm.smAirDelayNs, snmpSm.smFrequencyHz, false);

                    var thisAPModel = Devices[thisSmStats.ap_mac].product;
                    var thisApTx = APstats[thisSmStats.ap_mac].radio.tx_power;
                    var thisAPName = Devices[thisSmStats.ap_mac].name;

                    var smFSPL = RFCalc.FreeSpacePathLoss(smDistanceM, snmpSm.smFrequencyHz);

                    // smEPL === The power transmitted from the AP and what we expect to see on the SM
                    var smEPL = RFCalc.EstimatedPowerLevel(
                        smDistanceM,
                        snmpSm.smFrequencyHz,
                        0, 
                        Tx: cambium.Types[thisAPModel].Radio(thisApTx, 16),
                        Rx: cambium.Types[thisSM.product].Radio(thisSmStats.radio.tx_power));

                    var smAPL = thisSmStats.radio.dl_rssi;

                    // apEPL === The power transmitted from the SM and what we expect to see on the AP
                    var apEPL = RFCalc.EstimatedPowerLevel(
                        smDistanceM,
                        snmpSm.smFrequencyHz,
                        0,
                        Tx: cambium.Types[thisSM.product].Radio(thisSmStats.radio.tx_power),
                        Rx: cambium.Types[thisAPModel].Radio(thisApTx, 16));

                    var apAPL = thisSmStats.radio.ul_rssi;

                    finalSubResults.Add(new SubscriberRadioInfo()
                    {
                        Name = thisSM.name,
                        Esn = thisSM.mac,
                        APName = thisAPName,
                        DistanceM = (int)smDistanceM,
                        IP = thisSM.ip,
                        Model = thisSM.product,
                        SmEPL = Math.Round(smEPL, 2),
                        SmAPL = smAPL ?? -1,
                        ApModel = thisAPModel,
                        ApEPL = Math.Round(apEPL, 2),
                        ApAPL = apAPL ?? -1,
                        APTxPower = thisApTx ?? cambium.Types[thisAPModel].MaxTransmit,
                        SMTxPower = thisSmStats.radio.tx_power ?? cambium.Types[thisSM.product].MaxTransmit,
                        SMMaxTxPower = cambium.Types[thisSM.product].MaxTransmit
                    });

                    Console.WriteLine($"Found Device: {thisSM.name} - SM PL Diff {Math.Abs((double)smEPL - (double)smAPL)} AP PL Diff: {Math.Abs((double)smEPL - (double)smAPL)}");
                }

                using (var ep = new ExcelPackage())
                {
                    var x = finalSubResults.ToList();

                    var ew = ep.Workbook.Worksheets.Add("450 Devices");
                    ew.Cells["A1"].LoadFromCollection<SubscriberRadioInfo>(x, true);
                    ew.Cells[ew.Dimension.Address].AutoFitColumns();
                    ep.SaveAs(new FileInfo("output.xlsx"));
                }
            }
            catch (WebException e)
            {
                Console.WriteLine(e.Message);
            }
            Console.WriteLine("Done");
            Console.ReadLine();
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