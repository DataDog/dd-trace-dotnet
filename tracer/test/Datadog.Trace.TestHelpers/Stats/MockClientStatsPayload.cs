// <copyright file="MockClientStatsPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using MessagePack;

namespace Datadog.Trace.TestHelpers.Stats
{
    [MessagePackObject]
    public class MockClientStatsPayload
    {
        [Key("Hostname")]
        public string Hostname { get; set; }

        [Key("Env")]
        public string Env { get; set; }

        [Key("Version")]
        public string Version { get; set; }

        [Key("Lang")]
        public string Lang { get; set; }

        [Key("TracerVersion")]
        public string TracerVersion { get; set; }

        [Key("RuntimeID")]
        public string RuntimeId { get; set; }

        [Key("Sequence")]
        public long Sequence { get; set; }

        [Key("AgentAggregation")]
        public string AgentAggregation { get; set; }

        [Key("Service")]
        public string Service { get; set; }

        [Key("Stats")]
        public List<MockClientStatsBucket> Stats { get; set; }
    }
}
