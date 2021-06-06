namespace cnMaestroReporting.SNMP
{
    public class SnmpProgress
    {
        //current progress
        public int CurrentProgressAmount { get; set; }
        //total progress
        public int TotalProgressAmount { get; set; }
        //Message
        public string CurrentProgressMessage { get; set; }
    }
}