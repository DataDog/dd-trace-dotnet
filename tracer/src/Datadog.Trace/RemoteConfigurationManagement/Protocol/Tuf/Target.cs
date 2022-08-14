// <copyright file="Target.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf
{
    internal class Target
    {
        [JsonProperty("custom")]
        public TargetCustom Custom { get; set; } = new();

        [JsonProperty("hashes")]
        public Dictionary<string, string> Hashes { get; set; } = new();

        [JsonProperty("length")]
        public int Length { get; set; }
    }
}
