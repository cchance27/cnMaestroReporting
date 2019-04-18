using cnMaestroReporting.cnMaestroAPI.cnDataType;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cnMaestroReporting.cnMaestroAPI
{
    public class Api
    {
        private readonly Manager _manager;

        public Api(Manager manager)
        {
            _manager = manager;
        }

        /// <summary>
        /// Returns a list of tasks that are fetching all of the networks
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public Task<IList<CnTower>> GetNetworksAsync(string filter = null) =>
            _manager.GetFullApiResultsAsync<CnTower>("/networks", filter);

        /// <summary>
        /// Returns a a list of tasks that are fetching all of the towers available on the network
        /// </summary>
        /// <param name="network"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public Task<IList<CnTower>> GetTowersAsync() {
            if (String.IsNullOrWhiteSpace(_manager.settings.Network))
                throw new ArgumentNullException("Network", "Network Setting is required for cnMaestro");

            return _manager.GetFullApiResultsAsync<CnTower>($"/networks/{_manager.settings.Network}/towers");
        }

        /// <summary>
        /// Returns a list of devices based on a filter
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public Task<IList<CnDevice>> GetMultipleDevicesAsync(string filter = null)
        {
            // TODO: Fix the hacky stuff and make it so that we can pass dictionary of filters instead of strings.
            // Hacky way of doing this but should work. Grabbing the tower to filter by from config and combining with any passed filters.
            if (String.IsNullOrWhiteSpace(filter) == false)
            {
                // We have a custom filter
                if (String.IsNullOrWhiteSpace(_manager.settings.Tower) == false)
                {
                    // We also have a towerFilter from config
                    filter = "tower=" + Uri.EscapeDataString(_manager.settings.Tower) + "&" + filter;
                }
            }
            else
            {
                // We don't have a custom filter
                if (String.IsNullOrWhiteSpace(_manager.settings.Tower) == false)
                {
                    // We also have a towerFilter from config
                    filter = "tower=" + Uri.EscapeDataString(_manager.settings.Tower);
                }
            }

            return _manager.GetFullApiResultsAsync<CnDevice>($"/devices", filter);
        }
            
        /// <summary>
        /// Return a single device by macaddress
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public async Task<CnDevice> GetSingleDeviceAsync(string macAddress, string filter = null)
        {
            // Return a single value
            var resp = await _manager.GetFullApiResultsAsync<CnDevice>($"/devices/{macAddress}", filter);
            return resp.SingleOrDefault<CnDevice>();
        }

        /// <summary>
        /// Return device current last reported statistics
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public async Task<CnStatistics> GetDeviceStatsAsync(string macAddress, string filter = null)
        {
            // Return a single value
            var resp = await _manager.GetFullApiResultsAsync<CnStatistics>($"/devices/{macAddress}/statistics", filter);
            return resp.SingleOrDefault<CnStatistics>();
        }

        /// <summary>
        /// Returns a list of devices statistics based on a filter
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public Task<IList<CnStatistics>> GetMultipleDevStatsAsync(string filter = null)
        {
            // Hacky way of doing this but should work. Grabbing the tower to filter by from config and combining with any passed filters.
            if (String.IsNullOrWhiteSpace(filter) == false)
            {
                // We have a custom filter
                if (String.IsNullOrWhiteSpace(_manager.settings.Tower) == false)
                {
                    // We also have a towerFilter from config
                    filter = "tower=" + Uri.EscapeDataString(_manager.settings.Tower) + "&" + filter;
                }
            }
            else
            {
                // We don't have a custom filter
                if (String.IsNullOrWhiteSpace(_manager.settings.Tower) == false)
                {
                    // We also have a towerFilter from config
                    filter = "tower=" + Uri.EscapeDataString(_manager.settings.Tower);
                }
            }

            return _manager.GetFullApiResultsAsync<CnStatistics>($"/devices/statistics", filter);
        }


        /// <summary>
        /// Return a list of performance from a device between 2 dates, it's returned as an array of days and hours.
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public Task<IList<CnPerformance>> GetDevicePerfAsync(string macAddress, DateTime startTime, DateTime endTime)
        {
            // Would be nice if we could maybe clean up the return to be a real date time list and not the weird way the API returns it.
            var startT = startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffK");
            var endT = endTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffK");
            var filter = $"start_time={startT}&stop_time={endT}";
            return _manager.GetFullApiResultsAsync<CnPerformance>($"/devices/{macAddress}/performance", filter);
        }
    }
}
