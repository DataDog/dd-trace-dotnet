// <copyright file="RcmConfigState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Protocol
{
    internal class RcmConfigState
    {
        public RcmConfigState(string id, int version, string product)
        {
            Id = id;
            Version = version;
            Product = product;
        }

        [JsonProperty("id")]
        public string Id { get; }

        [JsonProperty("version")]
        public int Version { get; }

        [JsonProperty("product")]
        public string Product { get; }
    }
}
