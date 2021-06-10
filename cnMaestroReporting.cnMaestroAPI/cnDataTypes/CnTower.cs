namespace cnMaestroReporting.cnMaestroAPI.cnDataType
{
    public record CnTower(string id, string managed_account, string name, CnLocation location, string network) : ICnMaestroDataType;
}
