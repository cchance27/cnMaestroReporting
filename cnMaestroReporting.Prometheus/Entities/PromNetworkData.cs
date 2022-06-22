using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cnMaestroReporting.Prometheus.Entities
{
    public record PromNetworkData (PromApiResponse ApDl, PromApiResponse ApUl, PromApiResponse ApDlTp, PromApiResponse ApUlTp, PromApiResponse ApDl7Days, PromApiResponse ApUl7Days, PromApiResponse ApDl24Hours, PromApiResponse ApUl24Hours, PromApiResponse ApMPGain, PromApiResponse ApAvgGrp);
}
