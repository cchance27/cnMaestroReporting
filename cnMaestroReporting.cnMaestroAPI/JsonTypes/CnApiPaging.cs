namespace cnMaestroReporting.cnMaestroAPI.JsonType
{
    public record CnApiPaging (int total, int limit, int offset) : ICnMaestroDataType;
}
