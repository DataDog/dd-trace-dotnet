// <copyright file="MockDataStreamsBucket.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using MessagePack;

namespace Datadog.Trace.TestHelpers.DataStreamsMonitoring;

[MessagePackObject]
public class MockDataStreamsBucket
{
    [Key(nameof(Start))]
    public ulong Start { get; set; }

    [Key(nameof(Duration))]
    public ulong Duration { get; set; }

    [Key(nameof(Stats))]
    public MockDataStreamsStatsPoint[] Stats { get; set; }

    [Key(nameof(Backlogs))]
    public MockDataStreamsBacklog[] Backlogs { get; set; }
}
