namespace cnMaestroReporting.Reporting.PPTX.Entities
{
    public record ulModInfo(string series, float Uplink) : ISeriesInfo
    {
        public string series { get; set; } = series;
    }
}

