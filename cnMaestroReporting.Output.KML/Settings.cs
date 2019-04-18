using System;
using System.Collections.Generic;
using System.Text;

namespace cnMaestroReporting.Output.KML
{
    public class Settings
    {
        public string FileName { get; set; }
        public Dictionary<string, StyleConfig> Icons { get; set; }
    }

    public class StyleConfig
    {
        public string Icon { get; set; }
        public float TextScale { get; set; }
        public float IconScale { get; set; }
        public int SignalLevel { get; set; }
    }
}
