using SnmpSharpNet;
using System;

namespace cnMaestroReporting.SNMP
{
    public class Settings
    {
        public int Retries { get; set; } = 1;
        public int Timeout { get; set; } = 2000;
        public int Port { get; set; } = 161;
        public int Threads { get; set; } = 1;

        public OctetString SnmpCommunity { get; set; } = new OctetString("public");
        public string Community
        {
            get => SnmpCommunity.ToString();
            set => SnmpCommunity = new OctetString(value);
        }

        public SnmpVersion SnmpVer { get; set; } = SnmpVersion.Ver2;
        public int Version
        {
            get => (int)SnmpVer;
            set
            {
                // Using a custom instead because the library doesn't convert from int properly, Ver3 == 3 but Ver1, Ver2 are 0 and 1
                if (value == 1)
                {
                    SnmpVer = SnmpVersion.Ver1;
                }
                else if (value == 2)
                {
                    SnmpVer = SnmpVersion.Ver2;
                }
                else
                {
                    throw new NotSupportedException("Only SNMP Version 1 and Version 2 are supported currently.");
                }
            }
        }

    }
}
