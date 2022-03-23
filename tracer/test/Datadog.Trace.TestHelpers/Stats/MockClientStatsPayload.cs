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
        [Key("hostname")]
        public string Hostname { get; set; }

        [Key("env")]
        public string Env { get; set; }

        [Key("version")]
        public string Version { get; set; }

        [Key("lang")]
        public string Lang { get; set; }

        [Key("tracerVersion")]
        public string TracerVersion { get; set; }

        [Key("runtimeID")]
        public string RuntimeId { get; set; }

        [Key("sequence")]
        public long Sequence { get; set; }

        [Key("agentAggregation")]
        public string AgentAggregation { get; set; }

        [Key("service")]
        public string Service { get; set; }

        [Key("stats")]
        public List<MockClientStatsBucket> Stats { get; set; }
    }
}
