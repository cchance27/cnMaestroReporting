namespace cnMaestroReporting.Output.PTPPRJ
{
    public class AccessPointSettings
    {
        // Primary Settings
        public string antenna;
        public string number;
        public string antenna_height;
        public string antenna_azimuth;
        public string ap_frequency;
        public string tilt { get; set; } = "0"; // Negative downtilt -5 = downtilt 5
        public string receive_target_level { get; set; } = "-56"; // -56
        public string modelled_beamwidth { get; set; } = "120";
        public string shape { get; } = "triangle";

        // SM Children settings
        public string sm_antenna_height { get; set; } = "6";

        // Noise settings
        public string use_noise { get; set; } = "1"; // 0/1 enable disable ap noise in calc
        public string noise_density { get; set; } = "-90"; // noise level on the ap
        public string use_noise_sm { get; set; } = "1"; // 0/1 enable/disable sm noise in calc
        public string noise_density_sm { get; set; } = "-90" // noise level on sm
        
        // Not implementing below items
        /// Unknowns
        //public string template_id; //"None"
        //public string synthesizer_step_size; // "0.00625"
        //public string max_payload_bytes;
        //public string sm_max_payload_bytes;
        //public string sm_frequency;

        /// User power settings
        //public string use_user_eirp;
        //public string user_eirp;
        //public string use_user_power;
        //public string user_power;
        //public string use_user_power_mw;
        //public string user_power_mw;
        //public string user_cable_loss;

        /// Feeder settings
        //public string feeder_type;
        //public string feeder_loss;
        //public string feeder_calculate;
        //public string feeder_length;
    }
}