using Newtonsoft.Json;
using System.Collections.Generic;

namespace cnMaestroReporting.cnMaestroAPI.JsonType
{
    public struct CnApiResponse<T>
    {
        [JsonProperty("paging")]
        public CnApiPaging paging;

        [JsonProperty("data")]
        public T[] data;
    }
}
