namespace cnMaestroReporting.Output.PTPPRJ
{
    public class Equipment
    {
        public string max_range { get; set; } = "2";
        public string max_range_units { get; set; } = "miles"; //"miles"
        public string sm_range { get; set; } = "2"; 
        public string range_units { get; set; } = "miles"; //"kilometers"

        public string frame_period { get; set; } = "2.5"; 
        public string product { get; set; } = "PMP58450i";
        public string adjacent_channel_support { get; set; } = "0"; // 0/1
        public string control_slots { get; set; } = "3";
        public string color_code { get; set; } = "0"; 
        public string aes_encryption { get; set; } = "1"; // 0/1
        public string downlink_data { get; set; } = "80"; 
        public string sync_input { get; set; } = "AutoSync"; //"Generating Sync"
        public string broadcast_repeat_count { get; set; } = "0"; 
        public string bandwidth { get; set; } = "20"; 

        // Not implemented below
        // public string sm_max_payload_bytes { get; } //"1600"
        // public string ap_max_payload_bytes { get; } //"1600"
        // public string sm_registration_limit { get; } //"238"
        // public string ul_max_multiplier { get; } //"8"
        // public string dl_max_multiplier { get; } //"8"
        // public string data_channel { get; } //"1"
        // public string phase1_end { get; } //"Local"
    }
}