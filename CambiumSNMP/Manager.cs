using System;
using System.Collections.Generic;
using System.Net;
using SnmpSharpNet;

namespace CambiumSNMP
{
    public class Manager
    {
        private OctetString _community { get; set; } = new OctetString("public");
        public string Community
        {
            get => _community.ToString();
            set => _community = new OctetString(value);
        }

        private SnmpVersion _version { get; set; } = SnmpVersion.Ver2;
        public int Version
        {
            get => (int)_version;
            set
            {
                // Using a custom instead because the library doesn't convert from int properly, Ver3 == 3 but Ver1, Ver2 are 0 and 1
                if (value == 1)
                {
                    _version = SnmpVersion.Ver1;
                }
                else if (value == 2)
                {
                    _version = SnmpVersion.Ver2;
                }
                else
                {
                    _version = SnmpVersion.Ver3;
                }
            }
        }

        public int Timeout { get; set; } = 2000;
        public int Port { get; set; } = 161;
        public int Retry { get; set; } = 0;

        private IAgentParameters _agentParameters { get => new AgentParameters(_version, _community); }

        public Manager(string community, int snmpVersion, int retries = 0, int timeout = 2000, int port = 161)
        {
            // Use external methods so that it handles the proper conversions just like independently setting.
            Community = community;
            Version = snmpVersion;
            Timeout = timeout;
            Port = port;
            Retry = Retry;
        }

        // Can create a clean Manager that just initializes default SNMP Ver2 and public community
        public Manager() { }

        public IDictionary<string, string> GetOids(string ipAddress, params string[] oids)
        {
            IPAddress ip = null;
            bool goodIP = IPAddress.TryParse(ipAddress, out ip);

            if (!goodIP)
                throw new InvalidCastException("IP Provided Was Invalid: {ipAddress}");

            Pdu pdu = new Pdu(PduType.Get);
            foreach (string oid in oids)
                pdu.VbList.Add(oid);

            using (UdpTarget target = new UdpTarget(ip, Port, Timeout, Retry))
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

        public CambiumSM GetCambiumSM(string ipAddress)
        {
            try
            {
                var sm = GetOids(ipAddress,
                    OIDs.sysName,
                    OIDs.sysLocation,
                    OIDs.sysContact,
                    OIDs.smAntennaGain,
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
                    cambiumAntennaGain: sm[OIDs.smAntennaGain],
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
