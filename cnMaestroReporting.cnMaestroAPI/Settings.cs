namespace cnMaestroReporting.cnMaestroAPI
{
    public class Settings
    {
        public string ApiClientID { get; set; }
        public string ApiClientSecret { get; set; }
        public string ApiDomain { get; set; }
        public int ApiPageLimit { get; set; } = 100;
        public int ApiThreads { get; set; } = 4;
        public string Network { get; set; } = "default";
        public string Tower { get; set; } = "";
    }
}
