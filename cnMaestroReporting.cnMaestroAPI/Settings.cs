using System;

namespace cnMaestroReporting.cnMaestroAPI
{
    public class Settings
    {
        private string _network = "default";
        public string ApiClientID { get; set; } = "";
        public string ApiClientSecret { get; set; } = "";
        public string ApiDomain { get; set; } = "";
        public int ApiPageLimit { get; set; } = 100;
        public int ApiThreads { get; set; } = 4;
        public string Network
        {
            get {
                if (String.IsNullOrWhiteSpace(_network))
                    throw new ArgumentNullException("Network", "Network Setting is required for cnMaestro");

                return _network;
            }
            set
            {
                _network = value;
            }
        }
        public string Tower { get; set; } = "";
    }
}
