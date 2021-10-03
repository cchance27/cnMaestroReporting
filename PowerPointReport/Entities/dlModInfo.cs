namespace cnMaestroReporting.Reporting.PPTX.Entities
{
    public record dlModInfo(string series, float Downlink) : ISeriesInfo
    {
        public string series { get; set; } = series;
    }
}

