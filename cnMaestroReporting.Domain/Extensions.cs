using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cnMaestroReporting.Domain
{
    public static class Extensions
    {
        public static string lookupApNameByIp(this IDictionary<ESN, AccessPointRadioInfo> _apInfo, string ipaddress)
        {
            var ApRi = _apInfo.Values.Where(v => v.IP == ipaddress).FirstOrDefault()?.Name;
            return ApRi ?? ipaddress;
        }

        public static string lookupApNameByEsn(this IDictionary<ESN, AccessPointRadioInfo> _apInfo, string esn)
        {
            var ApRi = _apInfo.Values.Where(v => v.Esn.Contains(esn, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault()?.Name;
            return ApRi;
        }


        public static string lookupApModelByIp(this IDictionary<ESN, AccessPointRadioInfo> _apInfo, string ipaddress)
        {
            var x = _apInfo.Values.Where(v => v.IP == ipaddress).FirstOrDefault()?.Hardware;
            return x;
        }

    }
}
