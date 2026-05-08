// <copyright file="RcmClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Protocol
{
    internal sealed class RcmClient
    {
        public RcmClient(string id, RcmClientState state)
        {
            Id = id;
            State = state;
            // Client Tracer is actually required by the API, but we don't have one initially
            ClientTracer = null!;
        }

        [JsonProperty("state")]
        public RcmClientState State { get; }

        [JsonProperty("id")]
        public string Id { get; }

        [JsonProperty("products")]
        public ICollection<string> Products { get; set; } = [];

        [JsonProperty("is_tracer")]
        public bool IsTracer => true;

        [JsonProperty("client_tracer")]
        public RcmClientTracer ClientTracer { get; set; }

        [JsonProperty("capabilities")]
        public byte[] Capabilities { get; set; } = [];
    }
}
