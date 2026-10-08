// <copyright file="MockProbeSnapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Diagnostics;
using MessagePack; // use nuget MessagePack to deserialize

namespace Datadog.Trace.TestHelpers
{
    [MessagePackObject]
    [DebuggerDisplay("{ToString(),nq}")]
    public class MockProbeSnapshot
    {
        [Key("name")]
        public string Name { get; set; }

        [Key("service")]
        public string Service { get; set; }

        [Key("type")]
        public string Type { get; set; }

        [Key("duration")]
        public long Duration { get; set; }

        [Key("meta")]
        public Dictionary<string, string> Tags { get; set; }

        [Key("metrics")]
        public Dictionary<string, double> Metrics { get; set; }

        public override string ToString()
        {
            return $"{nameof(Name)}: {Name}, {nameof(Service)}: {Service}";
        }
    }
}
