using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SnmpSharpNet;

namespace cnMaestroReporting.SNMP
{
    public class Manager
    {
        private SemaphoreSlim _taskThrottle { get; }
        public Settings settings { get; set; } = new Settings();

        private IAgentParameters _agentParameters { get => new AgentParameters(settings.SnmpVer, settings.SnmpCommunity); }

        /// <summary>
        /// Base Constructor for setting up all the variables to be used for SNMP.
        /// </summary>
        /// <param name="community"></param>
        /// <param name="version"></param>
        /// <param name="retries"></param>
        /// <param name="timeout"></param>
        /// <param name="port"></param>
        /// <param name="threads"></param>
        public Manager(string community, int version, int retries = 0, int timeout = 2000, int port = 161, int threads = 4)
        {
            // Use external methods so that it handles the proper conversions just like independently setting.
            settings.Community = community;
            settings.Version = version;
            settings.Timeout = timeout;
            settings.Port = port;
            settings.Retries = retries;
            settings.Threads = threads;

            if (_taskThrottle == null)
                _taskThrottle = new SemaphoreSlim(threads);
        }

        public Manager(IConfigurationSection configSection)
        {
            configSection.Bind(settings);

            if (_taskThrottle == null)
                _taskThrottle = new SemaphoreSlim(settings.Threads);
        }

        /// <summary>
        /// Create a manager based on a CambiumSNMP.Settings
        /// </summary>
        /// <param name="settings"></param>
        public Manager(Settings settings) : this(settings.Community, settings.Version, settings.Retries, settings.Port, settings.Threads) { }

        /// <summary>
        /// Synchronously get snmp results from a speciifc IP Address for a param array of oids
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <param name="oids"></param>
        /// <returns></returns>
        public IDictionary<string, string> GetOids(string ipAddress, params string[] oids)
        {
            IPAddress ip = null;
            bool goodIP = IPAddress.TryParse(ipAddress, out ip);

            if (!goodIP)
                throw new InvalidCastException("IP Provided Was Invalid: {ipAddress}");

            Pdu pdu = new Pdu(PduType.Get);
            foreach (string oid in oids)
                pdu.VbList.Add(oid);

            using (UdpTarget target = new UdpTarget(ip, settings.Port, settings.Timeout, settings.Retries))
            {
                SnmpPacket result = target.Request(pdu, _agentParameters);
                
                IDictionary<string, string> outputResults = new Dictionary<string, string>();
                if (result != null)
                {
                    if (result.Pdu.ErrorStatus != 0)
                    {
                        // I've never run into it but we hard fail on a specific Pdu failure
                        // could adjust this to skip the error index and return the rest but rather hardfail.
                        throw new SnmpErrorStatusException($"SNMP Results included an error at index: {result.Pdu.ErrorIndex}", result.Pdu.ErrorStatus, result.Pdu.ErrorIndex);
                    }

                    foreach (Vb item in result.Pdu.VbList)
                    {
//                      Console.WriteLine(SnmpConstants.GetTypeName(item.Value.Type));
                        outputResults[item.Oid.ToString()] = item.Value.ToString();
                    }
                }
                else
                {
                    throw new SnmpException("We got back null from SNMP Polling");
                }

                target.Close();
                return outputResults;
            } 
        }



        /// <summary>
        /// Async method for getting a param array of oids results from a list of IPs. 
        /// We can dump as many IP's and OIDs into this as we want and it will respect 
        /// the thread limit because of our use of the semaphore slim during startup of the manager.
        /// </summary>
        /// <param name="ipAddresses"></param>
        /// <param name="oids"></param>
        public async Task<IDictionary<string, IDictionary<string, string>>> GetMultipleDeviceOidsAsync(IEnumerable<string> ipAddresses, IProgress<SnmpProgress> progress, params string[] oids)
        {
            var taskList = new List<Task>();
            ConcurrentDictionary<string, IDictionary<string, string>> allResults = new ConcurrentDictionary<string, IDictionary<string, string>>();
            int done = 0;
            int total = ipAddresses.Count();

            foreach (var ip in ipAddresses) {
                await _taskThrottle.WaitAsync(); // Wait for a free semaphore
                taskList.Add(
                    // This will run in a new thread parallel on threadpool) 
                    Task.Run(() =>
                        {
                            try
                            {
                                allResults.TryAdd(ip, GetOids(ip, oids)); // GetOids and then add them to our dictionary to be returned at the end.
                                done++;
                                progress.Report(new SnmpProgress() { CurrentProgressMessage = $"Fetched: {ip}", CurrentProgressAmount = done, TotalProgressAmount = total });
                            }
                            catch
                            {
                                // We got a failed snmp we should do something
                                // TODO: constructor should take a output for logging that we can write to.
                                done++;
                                progress.Report(new SnmpProgress() { CurrentProgressMessage = $"Errored: {ip}", CurrentProgressAmount = done, TotalProgressAmount = total });
                            }
                            finally
                            {
                                _taskThrottle.Release();
                            }
                        }));
            }
            Task.WaitAll(taskList.ToArray());
            progress.Report(new SnmpProgress() { CurrentProgressMessage = $"Complete", CurrentProgressAmount = done, TotalProgressAmount = total });

            return allResults;
        }

        /// <summary>
        /// Wrapper to return a complete CambiumSM (not currently used but available if needed)
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        public CambiumSM GetCambiumSM(string ipAddress)
        {
            try
            {
                var sm = GetOids(ipAddress,
                    OIDs.sysName,
                    OIDs.sysLocation,
                    OIDs.sysContact,
                    OIDs.antennaGain,
                    OIDs.smFrequencyHz,
                    OIDs.smRadioDbm,
                    OIDs.smRadioDbmH,
                    OIDs.smRadioDbmV,
                    OIDs.smRadioDbmMin,
                    OIDs.smRadioDbmMax,
                    OIDs.smRadioDbmAvg,
                    OIDs.smRadioTxPower,
                    OIDs.smRegisteredApMac,
                    OIDs.smAdaptRateFull,
                    OIDs.smAdaptRate,
                    OIDs.smSessionTime,
                    OIDs.smTotalBER,
                    OIDs.smAirDelayNs,
                    OIDs.smModFragmentPct,
                    OIDs.smSnrV,
                    OIDs.smSnrH,
                    OIDs.cambiumProductFreq,
                    OIDs.cambiumProductType,
                    OIDs.cambiumMac,
                    OIDs.cambiumSoftwareVer
                    );

                return new CambiumSM(
                    sysName: sm[OIDs.sysName],
                    sysLocation: sm[OIDs.sysLocation],
                    sysContact: sm[OIDs.sysContact],
                    cambiumAntennaGain: sm[OIDs.antennaGain],
                    smFrequencyHz: sm[OIDs.smFrequencyHz],
                    smRadioDbm: sm[OIDs.smRadioDbm],
                    smRadioDbmH: sm[OIDs.smRadioDbmH],
                    smRadioDbmV: sm[OIDs.smRadioDbmV],
                    smRadioDbmMin: sm[OIDs.smRadioDbmMin],
                    smRadioDbmMax: sm[OIDs.smRadioDbmMax],
                    smRadioDbmAvg: sm[OIDs.smRadioDbmAvg],
                    smRadioTxPower: sm[OIDs.smRadioTxPower],
                    smRegisteredApMac: sm[OIDs.smRegisteredApMac],
                    smAdaptRateFull: sm[OIDs.smAdaptRateFull],
                    smAdaptRate: sm[OIDs.smAdaptRate],
                    smSessionTime: sm[OIDs.smSessionTime],
                    smTotalBER: sm[OIDs.smTotalBER],
                    smAirDelayNs: sm[OIDs.smAirDelayNs],
                    smModFragmentPct: sm[OIDs.smModFragmentPct],
                    smSnrV: sm[OIDs.smSnrV],
                    smSnrH: sm[OIDs.smSnrH],
                    cambiumProductFreq: sm[OIDs.cambiumProductFreq],
                    cambiumProductType: sm[OIDs.cambiumProductType],
                    cambiumMac: sm[OIDs.cambiumMac],
                    cambiumSoftwareVer: sm[OIDs.cambiumSoftwareVer]
                    );
            }
            catch (Exception e)
            {
                Console.WriteLine($"SM Fetch Error: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Wrapper to return a complete CambiumAP (not currently used but available if needed), not getting SM details etc yet.
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        public CambiumAP GetCambiumAP(string ipAddress)
        {
            try
            {
                var ap = GetOids(ipAddress,
                    OIDs.sysName,
                    OIDs.sysLocation,
                    OIDs.sysContact,
                    OIDs.apFrequencyHz, 
                    OIDs.apMuMimoMode,
                    OIDs.cambiumProductFreq,
                    OIDs.cambiumProductType,
                    OIDs.cambiumMac,
                    OIDs.cambiumSoftwareVer
                    );

                return new CambiumAP(
                    sysName: ap[OIDs.sysName],
                    sysLocation: ap[OIDs.sysLocation],
                    sysContact: ap[OIDs.sysContact],
                    apFrequencyHz: ap[OIDs.apFrequencyHz],
                    apMuMimoMode: ap[OIDs.apMuMimoMode],
                    cambiumProductFreq: ap[OIDs.cambiumProductFreq],
                    cambiumProductType: ap[OIDs.cambiumProductType],
                    cambiumMac: ap[OIDs.cambiumMac],
                    cambiumSoftwareVer: ap[OIDs.cambiumSoftwareVer]
                    );
            }
            catch (Exception e)
            {
                Console.WriteLine($"AP Fetch Error: {e.Message}");
                return null;
            }
        }
    }
}
