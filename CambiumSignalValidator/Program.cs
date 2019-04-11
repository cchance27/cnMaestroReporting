using CommonCalculations;
using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.IO;
using cnMaestro.cnDataType;
using System.Linq;

namespace CambiumSignalValidator
{
    internal class Program
    {
        private static cnMaestro.Manager cnManager { get; set; }
        private static cnMaestro.Settings cnMaestroConf = new cnMaestro.Settings();
        private static CambiumSNMP.Settings snmpConf = new CambiumSNMP.Settings();

        private static async Task Main(string[] args)
        {
            fetchConfiguration(); // Load our appSettings

            // Currently the program is only being used for testing various functions we will eventually have a real workflow.


            cnManager = new cnMaestro.Manager(cnMaestroConf);

            await cnManager.ConnectAsync();
            var cnApi = new cnMaestro.Api(cnManager);
            try
            {
                //var Networks = cnApi.GetNetworksTask();
                var Towers = cnApi.GetTowersTask("default");
                var SpecificDevice = cnApi.GetDeviceStatsTask("0A:00:3E:B2:39:AD");
                var SpecificDeviceAp = cnApi.GetDeviceStatsTask("0A:00:3E:70:DB:E6");
                
                //TODO: LEFT OFF FILTERING NOT WORKING?
                //var OfflineDevices = cnApi.GetFilteredDevicesTask("tower=Belvedere");
                //TODO: Filtering change to KVP pair

                //var APperformance = await cnManager.CallApiAsync<CnPerformance>("/devices/0A:00:3E:BB:0B:A2/performance?start_time=2019-03-31T18:12:11+0000&stop_time=2019-04-01T18:12:11+0000");

                Task.WaitAll(Towers,SpecificDeviceAp, SpecificDevice);

                var utsTower = Towers.Result.SingleOrDefault(tower => tower.Name == "UTS Philipsburg");
                

                foreach (var tower in Towers.Result)
                {
                    Console.WriteLine(tower.Name);
                }

                var snmp  = new CambiumSNMP.Manager(snmpConf.Community, 2);
                var sm450 = snmp.GetCambiumSM("192.168.240.217");
                var sm450b = snmp.GetCambiumSM("192.168.251.83");

                var ap = snmp.GetCambiumAP("172.16.10.73");

                var smDistance = RFCalc.MetersFromAirDelay(sm450.smAirDelayNs, sm450.smFrequencyHz);
                double ExpectedPowerLevelSM = RFCalc.EstimatedPowerLevel(
                    DistanceM: (double)smDistance,
                    FrequencyHz: sm450.smFrequencyHz,
                    MiscLoss: 2,
                    Tx: RadioConfig.CambiumAP(TxPower: SpecificDeviceAp.Result[0].radio.tx_power),
                    Rx: RadioConfig.CambiumSM(TxPower: SpecificDevice.Result[0].radio.tx_power, Gain: sm450.cambiumAntennaGain));

                Console.ReadLine();
            }
            catch (WebException e)
            {
                Console.WriteLine(e.Message);
            }
            Console.ReadLine();
        }

        private static void fetchConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            IConfigurationRoot configuration = builder.Build();

            configuration.GetSection("cnMaestro").Bind(cnMaestroConf);
            configuration.GetSection("canopySnmp").Bind(snmpConf);
        }
    }
}