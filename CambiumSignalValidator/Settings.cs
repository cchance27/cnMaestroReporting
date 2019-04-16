using System;
using System.Collections.Generic;
using System.Text;

namespace CambiumSignalValidator
{
    public class Settings
    {
        public string FileName { get; set; } 
        public int LowSignal { get; set; } = -80;
        public int BadPowerDiff { get; set; } = 10;
    }
}
