using System;

namespace CambiumSNMP
{
    public class CambiumSM
    {
        public string sysName { get; }
        public string sysLocation { get; }
        public string sysContact { get; }
        public Int32 cambiumAntennaGain { get; }
        public Int32 smFrequencyHz { get; }
        public string smRadioDbm { get; }
        public string smRadioDbmH { get; }
        public string smRadioDbmV { get; }
        public string smRadioDbmMin { get; }
        public string smRadioDbmMax { get; }
        public string smRadioDbmAvg { get; }
        public Byte smRadioTxPower { get; }
        public string smRegisteredApMac { get; }
        public string smAdaptRateFull { get; }
        public Byte smAdaptRate { get; }
        public string smSessionTime { get; }
        public string smTotalBER { get; }
        public Int32 smAirDelayNs { get; }
        public string smModFragmentPct { get; }
        public string smSnrV { get; }
        public string smSnrH { get; }
        public string cambiumProductFreq { get; }
        public string cambiumProductType { get; }
        public string cambiumMac { get; }
        public string cambiumSoftwareVer { get; }
        public Byte cambiumActiveTxPower { get; }

        public CambiumSM(string sysName, string sysLocation, string sysContact, string cambiumAntennaGain, string smFrequencyHz, string smRadioDbm, string smRadioDbmH, string smRadioDbmV, string smRadioDbmMin, string smRadioDbmMax, string smRadioDbmAvg, string smRadioTxPower, string smRegisteredApMac, string smAdaptRateFull, string smAdaptRate, string smSessionTime, string smTotalBER, string smAirDelayNs, string smModFragmentPct, string smSnrV, string smSnrH, string cambiumProductFreq, string cambiumProductType, string cambiumMac, string cambiumSoftwareVer, string cambiumActiveTxPower)
        {
            this.sysName = sysName;
            this.sysLocation = sysLocation;
            this.sysContact = sysContact;
            this.cambiumAntennaGain = Int32.Parse(cambiumAntennaGain);
            this.smFrequencyHz = Int32.Parse(smFrequencyHz);
            this.smRadioDbm = smRadioDbm;
            this.smRadioDbmH = smRadioDbmH;
            this.smRadioDbmV = smRadioDbmV;
            this.smRadioDbmMin = smRadioDbmMin;
            this.smRadioDbmMax = smRadioDbmMax;
            this.smRadioDbmAvg = smRadioDbmAvg;
            smRadioTxPower = smRadioTxPower.Split(" ")[0];
            this.smRadioTxPower = Byte.Parse(smRadioTxPower);
            this.smRegisteredApMac = smRegisteredApMac;
            this.smAdaptRateFull = smAdaptRateFull;
            this.smAdaptRate = Byte.Parse(smAdaptRate);
            this.smSessionTime = smSessionTime;
            this.smTotalBER = smTotalBER;
            this.smAirDelayNs = Int32.Parse(smAirDelayNs);
            this.smModFragmentPct = smModFragmentPct;
            this.smSnrV = smSnrV;
            this.smSnrH = smSnrH;
            this.cambiumProductFreq = cambiumProductFreq;
            this.cambiumProductType = cambiumProductType;
            this.cambiumMac = cambiumMac;
            this.cambiumSoftwareVer = cambiumSoftwareVer;

            cambiumActiveTxPower = cambiumActiveTxPower.Split(" ")[0];
            if (cambiumActiveTxPower != "SNMP") // We only do this if we didn't get a SNMP error.
                Byte.Parse(cambiumActiveTxPower);
        }
    }                
}
