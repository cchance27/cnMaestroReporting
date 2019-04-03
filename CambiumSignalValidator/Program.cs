using cnMaestro;
using cnMaestro.cnDataType;
using CommonCalculations;
using System;
using System.Net;
using System.Threading.Tasks;

namespace CambiumSignalValidator
{
    internal class Program
    {
        private const string clientID = "9QnDYSkLBqOQ2UCs";
        private const string clientSecret = "zDTMC4CCH7XSomOedhG4Nas3biUx4d";
        private const string apiDomain = "cnmaestro.caribserve.net";

        private static Manager cnManager { get; set; }

        private static async Task Main(string[] args)
        {
            cnManager = new Manager(clientID, clientSecret, "cnMaestro.caribserve.net", 5);
            await cnManager.ConnectAsync();

            double dbmLoss = RFCalc.EstimatedPowerLevel(213, 5505, 2, 
                new RFCalc.Radio { AntennaGain = 9, InternalLoss = 1, RadioPower = 20 }, 
                new RFCalc.Radio { AntennaGain = 16, RadioPower = 20, InternalLoss = 1 });

            Console.WriteLine(dbmLoss.ToString("N2"));

            try
            {
                var networks = await cnManager.GetFullApiResultsAsync<CnNetworks>("/networks");
                var APs = await cnManager.GetFullApiResultsAsync<CnTowers>("/networks/default/towers", 5);
                //var towers = await cnManager.CallApiAsync<CnTowers>("/networks/default/towers");
                //var AP = await cnManager.CallApiAsync<CnDevice>("/devices/0A:00:3E:BB:0B:A2");
                //var APs = await cnManager.CallApiAsync<CnDevice>("/devices");
                //var APstatistics = await cnManager.CallApiAsync<CnStatistics>("/devices/0A:00:3E:BB:0B:A2/statistics");
                //var APperformance = await cnManager.CallApiAsync<CnPerformance>("/devices/0A:00:3E:BB:0B:A2/performance?start_time=2019-03-31T18:12:11+0000&stop_time=2019-04-01T18:12:11+0000");

                Console.ReadLine();
            }
            catch (WebException e)
            {
                Console.WriteLine(e.Message);
            }
            Console.ReadLine();
        }
    }
}