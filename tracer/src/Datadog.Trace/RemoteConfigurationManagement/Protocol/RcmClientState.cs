// <copyright file="RcmClientState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Protocol
{
    internal class RcmClientState
    {
        public RcmClientState(int rootVersion, int targetsVersion, List<RcmConfigState> configStates, bool hasError, string error, string backendClientState)
        {
            RootVersion = rootVersion;
            TargetsVersion = targetsVersion;
            ConfigStates = configStates;
            HasError = hasError;
            Error = error;
            BackendClientState = backendClientState;
        }

        [JsonProperty("root_version")]
        public int RootVersion { get; }

        [JsonProperty("targets_version")]
        public long TargetsVersion { get; }

        [JsonProperty("config_states")]
        public List<RcmConfigState> ConfigStates { get; }

        [JsonProperty("has_error")]
        public bool HasError { get; }

        [JsonProperty("error")]
        public string Error { get; }

        [JsonProperty("backend_client_state")]
        public string BackendClientState { get; }
    }
}
