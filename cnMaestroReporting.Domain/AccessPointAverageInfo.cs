namespace cnMaestroReporting.Domain
{
    public struct AccessPointAverageInfo
    {
        public string ApName { get; set; }
        public string Tower { get; set; }
        public int SMs { get; set; }
        public int AvgSmDistanceM { get; set; }
        public int MaxSmDistanceM { get; set; }
        public int AvgApPl { get; set; }
        public int WorstApPl { get; set; }
        public int AvgSmPl { get; set; }
        public int WorstSmPl { get; set; }
        public int AvgSmSnrH { get; set; }
        public int AvgSmSnrV { get; set; }
        public int WorstSmSnr { get; set; }
        public int AvgApSnrH { get; set; }
        public int AvgApSnrV { get; set; }
        public int WorstApSnr { get; set; }
    }
}