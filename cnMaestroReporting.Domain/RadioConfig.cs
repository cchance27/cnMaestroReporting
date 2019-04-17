using CommonCalculations;
using System.Collections.Generic;

namespace cnMaestroReporting.Domain
{
    public class RadioConfig
    {
        public Dictionary<string, RadioSettings> AP { get; set; }
        public Dictionary<string, RadioSettings> SM { get; set; }
    }

    public class RadioSettings
    {
        public int AntennaGain { get; set; }
        public int MaxTransmit { get; set; }

        public RFCalc.Radio Radio(int? actualTxPower) => new RFCalc.Radio
        {
            AntennaGain = AntennaGain,
            RadioPower = actualTxPower ?? MaxTransmit,
            InternalLoss = 1
        };

        public RFCalc.Radio Radio(int? actualTxPower, int AntennaGainOverride) => new RFCalc.Radio
        {
            AntennaGain = AntennaGainOverride,
            RadioPower = actualTxPower ?? MaxTransmit,
            InternalLoss = 1
        };
    }
}