// <copyright file="Signed.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf
{
    internal class Signed
    {
        [JsonProperty("targets")]
        public Dictionary<string, Target> Targets { get; set; } = new();

        [JsonProperty("version")]
        public long Version { get; set; }

        [JsonProperty("custom")]
        public TargetsCustom Custom { get; set; }
    }
}
