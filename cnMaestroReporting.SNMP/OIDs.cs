namespace cnMaestroReporting.SNMP
{
    // For storing the most common OIDs
    public static class OIDs
    {
        private const string
            _cambiumGenStatus = "1.3.6.1.4.1.161.19.3.3.1.",
            _cambiumGenConfig = "1.3.6.1.4.1.161.19.3.3.2.",
            _cambiumSmStatus = "1.3.6.1.4.1.161.19.3.2.2.",
            _cambiumApConfig = "1.3.6.1.4.1.161.19.3.1.1.",
            _cambiumApStatus = "1.3.6.1.4.1.161.19.3.1.7.";
                               
        public const string 
            sysName = "1.3.6.1.2.1.1.5.0",
            sysLocation = "1.3.6.1.2.1.1.6.0",
            sysContact = "1.3.6.1.2.1.1.4.0",
            sysDescr = "1.3.6.1.2.1.1.1.0",

            cambiumProductFreq = _cambiumGenStatus + "10.0",
            cambiumProductType = _cambiumGenStatus + "266.0",
            cambiumMac = _cambiumGenStatus + "3.0",
            cambiumSoftwareVer = _cambiumGenStatus + "1.0",

            apFrequencyHz = _cambiumApConfig + "2.0",
            apMuMimoMode = _cambiumApStatus + "38.0",

            smAntennaGain = _cambiumGenConfig + "14.0",
            smFrequencyHz = _cambiumSmStatus + "67.0",
            smRadioDbm = _cambiumSmStatus + "8.0",
            smRadioDbmH = _cambiumSmStatus + "117.0",
            smRadioDbmV = _cambiumSmStatus + "118.0",
            smRadioDbmMin = _cambiumSmStatus + "58.0",
            smRadioDbmMax = _cambiumSmStatus + "59.0",
            smRadioDbmAvg = _cambiumSmStatus + "61.0",
            smRadioTxPower = _cambiumSmStatus + "23.0",
            smRegisteredApMac = _cambiumSmStatus + "9.0",
            smAdaptRateFull = _cambiumSmStatus + "20.0",
            smAdaptRate = _cambiumSmStatus + "128.0",
            smSessionTime = _cambiumSmStatus + "36.0",
            smTotalBER = _cambiumSmStatus + "57.0",
            smAirDelayNs = _cambiumSmStatus + "64.0",
            smModFragmentPct = _cambiumSmStatus + "86.0",
            smSnrV = _cambiumSmStatus + "95.0",
            smSnrH = _cambiumSmStatus + "106.0";
    }
}
