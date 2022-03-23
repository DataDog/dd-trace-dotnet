// <copyright file="MockClientStatsBucket.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using MessagePack;

namespace Datadog.Trace.TestHelpers.Stats;

[MessagePackObject]
public class MockClientStatsBucket
{
    [Key("start")]
    public long Start { get; set; }

    [Key("duration")]
    public long Duration { get; set; }

    [Key("stats")]
    public List<MockClientGroupedStats> Stats { get; set; }

    [Key("agentTimeShift")]
    public long AgentTimeShift { get; set; }
}
