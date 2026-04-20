// <copyright file="MockCIVisibilityProtocol.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.TestHelpers.Ci
{
    public class MockCIVisibilityProtocol
    {
        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("metadata")]
        public Dictionary<string, Dictionary<string, object>> Metadata { get; set; }

        [JsonProperty("events")]
        public MockCIVisibilityEvent[] Events { get; set; }
    }
}
