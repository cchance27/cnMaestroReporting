namespace cnMaestroReporting.Prometheus
{
    public record PromResult(PromMetric metric, string[] value);
}
