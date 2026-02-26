// <copyright file="RcmClientState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Protocol
{
    internal sealed class RcmClientState
    {
        [JsonProperty("root_version")]
        public long RootVersion { get; set; }

        [JsonProperty("targets_version")]
        public long TargetsVersion { get; set; }

        [JsonProperty("config_states")]
        public List<RcmConfigState> ConfigStates { get; set; } = [];

        [JsonProperty("has_error")]
        public bool HasError { get; set; }

        [JsonProperty("error")]
        public string? Error { get; set; }

        [JsonProperty("backend_client_state")]
        public string? BackendClientState { get; set; }
    }
}
