namespace cnMaestroReporting.Reporting.PPTX.Entities
{
    public record freqCountInfo(string series, int fq3Ghz, int fq5Ghz) : ISeriesInfo
    {
        public string series { get; set; } = series;
    }
}

