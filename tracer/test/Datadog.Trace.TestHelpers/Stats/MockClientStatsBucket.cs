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
    [Key("Start")]
    public long Start { get; set; }

    [Key("Duration")]
    public long Duration { get; set; }

    [Key("Stats")]
    public List<MockClientGroupedStats> Stats { get; set; }

    [Key("AgentTimeShift")]
    public long AgentTimeShift { get; set; }
}
