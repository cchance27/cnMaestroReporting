using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cnMaestroReporting.Domain
{
    public record DeviceInfo (ESN Mac, string IP, DeviceMode Mode);
    public enum DeviceMode
    {
        ap, 
        sm
    }
}
