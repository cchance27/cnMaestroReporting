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

        public Task<IList<CnTowers>> GetTowersTask(string network, string filter = null) => 
            _manager.GetFullApiResultsAsync<CnTowers>($"/networks/{network}/towers", filter);

        public Task<IList<CnTowers>> GetNetworksTask(string filter = null) => 
            _manager.GetFullApiResultsAsync<CnTowers>("/networks", filter);

        public Task<IList<CnDevice>> GetDeviceTask(string macAddress, string filter = null) =>
            _manager.GetFullApiResultsAsync<CnDevice>($"/devices/{macAddress}", filter);

        public Task<IList<CnStatistics>> GetDeviceStatsTask(string macAddress, string filter = null) =>
            _manager.GetFullApiResultsAsync<CnStatistics>($"/devices/{macAddress}/statistics", filter);

        public Task<IList<CnPerformance>> GetDevicePerfTask(string macAddress, DateTime startTime, DateTime endTime)
        {
            var startT = startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffK");
            var endT = endTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffK");
            return _manager.GetFullApiResultsAsync<CnPerformance>($"/devices/{macAddress}/performance?start_time={startT}&stop_time={endT}");
        }
    }
}
