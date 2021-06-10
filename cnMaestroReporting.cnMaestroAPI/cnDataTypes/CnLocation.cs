namespace cnMaestroReporting.cnMaestroAPI.cnDataType
{
    public record CnLocation(string type, decimal[] coordinates) : ICnMaestroDataType;
}
