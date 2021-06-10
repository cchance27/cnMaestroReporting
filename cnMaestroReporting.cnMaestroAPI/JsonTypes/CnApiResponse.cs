using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace cnMaestroReporting.cnMaestroAPI.JsonType
{
    public struct CnApiResponse<T>
    {
        [JsonPropertyName("paging")]
        public CnApiPaging paging;

        [JsonPropertyName("data")]
        public T[] data;
    }
}
