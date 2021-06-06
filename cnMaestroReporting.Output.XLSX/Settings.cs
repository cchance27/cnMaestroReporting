using System;
using System.Collections.Generic;
using System.Text;

namespace cnMaestroReporting.Output.XLSX
{
    public class Settings
    {
        public int LowSignal { get; set; } = -80;
        public int LowSNR { get; set; } = 20;
        public int BadPowerDiff { get; set; } = 10;
    }
}
