namespace cnMaestroReporting.Output.PTPPRJ
{
    public class Subscriber
    {
        public string place_id; //"0"
        public string antenna; //"cf7f457e-6015-4021-bc6b-422cfa1f287f"
        public string antenna_height { get; set; } = "6.0"; //"6.0"
        public string shape { get; } = "rectangle"; //"rectangle"
        public Equipment equipment;

        // Not currently implementing the below
        // public string use_user_power_mw { get; } //"0"
        // public string user_power_mw { get; } //"1000"
        // public string use_user_power { get; } //"0"
        // public string user_power { get; } //"18"
        // public string user_cable_loss { get; } = "none"; //"None"
        // public string feeder_loss { get; }//"1"
        // public string feeder_type { get; } //"LMR400"
        // public string feeder_calculate { get; } //"0"
        // public string feeder_length { get; } //"10"
        // public string ap_antenna_gain { get; } //"17.1435342494"
        // public string import_spatial_frequency { get; } // "0"
        // public string tilt { get; } //"3.52194336867"
        // public string ap_tilt { get; } //"-3.52385741112"
        // public string antenna_azimuth { get; } //"270.223885244"
        // public string colour { get; } // Just allow it to be set by LinkPlanner
        // public string dirty { get; } // Not known what this is for
    }
}
}