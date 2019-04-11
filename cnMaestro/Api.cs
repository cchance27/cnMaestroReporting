using cnMaestro.cnDataType;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace cnMaestro
{
    public class Api
    {
        private readonly Manager _manager;

        public Api(Manager manager)
        {
            _manager = manager;
        }

        public Task<IEnumerable<CnTowers>> GetTowersTask(string network) => 
            _manager.GetFullApiResultsAsync<CnTowers>($"/networks/{network}/towers");

        public Task<IEnumerable<CnTowers>> GetNetworksTask() => 
            _manager.GetFullApiResultsAsync<CnTowers>("/networks");

        public Task<IEnumerable<CnDevice>> GetDeviceTask(string macAddress) =>
            _manager.GetFullApiResultsAsync<CnDevice>($"/devices/{macAddress}");

        public Task<IEnumerable<CnDevice>> GetFilteredDevicesTask(string filter) =>
            _manager.GetFullApiResultsAsync<CnDevice>($"/devices", filter);

    }
}
