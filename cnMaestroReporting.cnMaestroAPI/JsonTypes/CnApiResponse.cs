namespace cnMaestroReporting.cnMaestroAPI.JsonType
{
    public record CnApiResponse<T> (CnApiPaging paging, T[] data);
}
