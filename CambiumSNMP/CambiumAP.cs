using System;

namespace CambiumSNMP
{
    public class CambiumAP
    {
        public string sysName { get; }
        public string sysLocation { get; }
        public string sysContact { get; }
        public Int32 apFrequencyHz { get; }
        public string apMuMimoMode { get; }
        public string cambiumProductFreq { get; }
        public string cambiumProductType { get; }
        public string cambiumMac { get; }
        public string cambiumSoftwareVer { get; }

        public CambiumAP(string sysName, string sysLocation, string sysContact, string apFrequencyHz, string apMuMimoMode, string cambiumProductFreq, string cambiumProductType, string cambiumMac, string cambiumSoftwareVer)
        {
            this.sysName = sysName;
            this.sysLocation = sysLocation;
            this.sysContact = sysContact;
            this.apMuMimoMode = apMuMimoMode;
            this.apFrequencyHz = Int32.Parse(apFrequencyHz);
            this.cambiumProductFreq = cambiumProductFreq;
            this.cambiumProductType = cambiumProductType;
            this.cambiumMac         = cambiumMac;
            this.cambiumSoftwareVer = cambiumSoftwareVer;
        }
    }                
}
