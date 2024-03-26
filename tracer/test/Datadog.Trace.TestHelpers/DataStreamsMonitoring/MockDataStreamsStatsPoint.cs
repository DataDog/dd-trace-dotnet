// <copyright file="MockDataStreamsStatsPoint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using MessagePack;

namespace Datadog.Trace.TestHelpers.DataStreamsMonitoring;

[MessagePackObject]
public class MockDataStreamsStatsPoint
{
    [Key(nameof(EdgeTags))]
    public string[] EdgeTags { get; set; }

    [Key(nameof(Hash))]
    public ulong Hash { get; set; }

    [Key(nameof(ParentHash))]
    public ulong ParentHash { get; set; }

    [Key(nameof(PathwayLatency))]
    public byte[] PathwayLatency { get; set; }

    [Key(nameof(EdgeLatency))]
    public byte[] EdgeLatency { get; set; }

    [Key(nameof(PayloadSize))]
    public byte[] PayloadSize { get; set; }

    [Key(nameof(TimestampType))]
    public string TimestampType { get; set; }
}
