namespace cnMaestroReporting.Reporting.PPTX.Entities
{
    public record fullModInfo(string series, float Downlink, float Uplink) : ISeriesInfo
    {
        public string series { get; set; } = series;
    }
}

