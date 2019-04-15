﻿using Newtonsoft.Json;

namespace cnMaestro.cnDataType
{
    public struct CnNetwork: ICnMaestroDataType
    {
        [JsonProperty("id")]
        public readonly string Id;

        [JsonProperty("managed_account")]
        public readonly string ManagedAccount;

        [JsonProperty("name")]
        public readonly string Name;
    }
}