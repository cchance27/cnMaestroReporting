using CommonCalculations;
using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.IO;
using cnMaestro.cnDataType;

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

            cnManager = new cnMaestro.Manager(
                cnMaestroConf.ApiClientID,
                cnMaestroConf.ApiClientSecret,
                cnMaestroConf.ApiDomain,
                cnMaestroConf.ApiPageLimit,
                cnMaestroConf.ApiThreads
                );

            await cnManager.ConnectAsync();
            var cnApi = new cnMaestro.Api(cnManager);
            try
            {
                var Networks = cnApi.GetNetworksTask();
                var Towers = cnApi.GetTowersTask("default");
                var SpecificDevice = cnApi.GetDeviceTask("0A:00:3E:BB:0B:A2");

                //TODO: LEFT OFF FILTERING NOT WORKING?
                //var OfflineDevices = cnApi.GetFilteredDevicesTask("tower=Belvedere");
                //TODO: Filtering change to KVP pair

                //TODO: move to repository calls instad of manual building them.
                //var APstatistics = await cnManager.CallApiAsync<CnStatistics>("/devices/0A:00:3E:BB:0B:A2/statistics");
                //var APperformance = await cnManager.CallApiAsync<CnPerformance>("/devices/0A:00:3E:BB:0B:A2/performance?start_time=2019-03-31T18:12:11+0000&stop_time=2019-04-01T18:12:11+0000");

                Task.WaitAll(SpecificDevice, Towers, Networks);

                foreach (CnTowers tower in Towers.Result)
                {
                    Console.WriteLine(tower.)
                }
                var snmp  = new CambiumSNMP.Manager(snmpConf.Community, 2);
                var sm = snmp.GetCambiumSM("192.168.253.8");
                var ap = snmp.GetCambiumAP("172.16.10.73");

                var smDistance = RFCalc.MetersFromAirDelay(sm.smAirDelayNs, sm.smFrequencyHz);
                double ExpectedPowerLevelSM = RFCalc.EstimatedPowerLevel(
                    DistanceM: (double)smDistance,
                    FrequencyHz: sm.smFrequencyHz,
                    MiscLoss: 2,
                    Tx: RadioConfig.CambiumAP(TxPower: 24),
                    Rx: RadioConfig.CambiumSM(TxPower: sm.smRadioTxPower, Gain: sm.cambiumAntennaGain));

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