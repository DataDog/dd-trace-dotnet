// <copyright file="MockCIVisibilityTestModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.TestHelpers.Ci
{
    public class MockCIVisibilityTestModule
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("resource")]
        public string Resource { get; set; }

        [JsonProperty("service")]
        public string Service { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("start")]
        public ulong Start { get; set; }

        [JsonProperty("duration")]
        public ulong Duration { get; set; }

        [JsonProperty("test_module_id")]
        public ulong TestModuleId { get; set; }

        [JsonProperty("test_session_id")]
        public ulong TestSessionId { get; set; }

        [JsonProperty("error")]
        public int Error { get; set; }

        [JsonProperty("meta")]
        public Dictionary<string, string> Meta { get; set; }

        [JsonProperty("metrics")]
        public Dictionary<string, double> Metrics { get; set; }
    }
}
