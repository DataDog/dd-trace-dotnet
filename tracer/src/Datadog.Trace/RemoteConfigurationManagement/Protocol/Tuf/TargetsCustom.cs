// <copyright file="TargetsCustom.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf
{
    internal class TargetsCustom
    {
        [JsonProperty("opaque_backend_state")]
        public string OpaqueBackendState { get; set; }
    }
}
