using System;
using System.Collections.Generic;
using System.Text;

namespace cnMaestroReporting.Output.PTPPRJ
{
    public class Settings
    {
        public string FileName { get; set; } = "output.ptpprj";
        public int SmInvalidBeyondRangeM { get; set; } = 3000;
        public int SmDistanceDiffValidM { get; set; } = 100;
        public int SmHeight { get; set; } = 6;
        public int TowerHeight { get; set; } = 20;
        public int ApRange { get; set; } = 2;
        public string ApRangeUnits { get; set; } = "miles";
        public int minimumFadeMarginAP { get; set; } = 0;
        public int minimumFadeMarginSM { get; set; } = 0;
        public float minimumAvailabilityAP { get; set; } = 99.0f;
        public float minimumAvailabilitySM { get; set; } = 99.0f;
    }
}
