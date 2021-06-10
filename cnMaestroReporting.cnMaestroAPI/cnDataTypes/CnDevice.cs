
using System.Collections.ObjectModel;

namespace cnMaestroReporting.cnMaestroAPI.cnDataType
{
    public record CnDevice(string ap_group, string country, string description, CnDeviceConfig config, string hardware_version, string inactive_software_version, string ip, CnLocation location, string mac, string managed_account, string msn, string name, string network, string product, string last_reboot_reason, string maximum_range, string registration_date, string site, string site_id, string software_version, string status, string status_time, string tower, string type): ICnMaestroDataType;
    public record CnDeviceConfig(string sync_reason, bool sync_status, ReadOnlyDictionary<string, string> variables, int version) : ICnMaestroDataType;
}
