using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cnMaestroReporting.cnMaestroAPI.cnDataTypes
{
    public record cnAuthentication(string access_token, int expires_in, string token_type);
}
