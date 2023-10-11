// <copyright file="MockDataStreamsBacklog.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using MessagePack;

namespace Datadog.Trace.TestHelpers.DataStreamsMonitoring;

[MessagePackObject]
public class MockDataStreamsBacklog
{
    [Key(nameof(Tags))]
    public string[] Tags { get; set; }

    [Key(nameof(Value))]
    public long Value { get; set; }
}
