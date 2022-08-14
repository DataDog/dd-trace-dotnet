// <copyright file="RcmClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Protocol
{
    internal class RcmClient
    {
        public RcmClient(string id, List<string> products, RcmClientTracer clientTracer, RcmClientState state)
        {
            Id = id;
            Products = products;
            IsTracer = true;
            ClientTracer = clientTracer;
            State = state;
        }

        [JsonProperty("state")]
        public RcmClientState State { get; }

        [JsonProperty("id")]
        public string Id { get; }

        [JsonProperty("products")]
        public List<string> Products { get; }

        [JsonProperty("is_tracer")]
        public bool IsTracer { get; }

        [JsonProperty("client_tracer")]
        public RcmClientTracer ClientTracer { get; }
    }
}
