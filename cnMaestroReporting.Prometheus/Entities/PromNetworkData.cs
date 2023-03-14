using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cnMaestroReporting.Prometheus.Entities
{
    public record PromNetworkData (PromApiResponse ApDl, PromApiResponse ApUl, PromApiResponse ApDlTp, PromApiResponse ApUlTp, PromApiResponse ApMPGain, PromApiResponse ApAvgGrp, PromApiResponse SMMaxCount, PromApiResponse SMDlMod);
}
