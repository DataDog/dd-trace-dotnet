// <copyright file="MockClientGroupedStats.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using MessagePack;

namespace Datadog.Trace.TestHelpers.Stats;

[MessagePackObject]
public class MockClientGroupedStats
{
    [Key("Service")]
    public string Service { get; set; }

    [Key("Name")]
    public string Name { get; set; }

    [Key("Resource")]
    public string Resource { get; set; }

    [Key("HTTPStatusCode")]
    public int HttpStatusCode { get; set; }

    [Key("Type")]
    public string Type { get; set; }

    [Key("DBType")]
    public string DbType { get; set; }

    [Key("Hits")]
    public long Hits { get; set; }

    [Key("Errors")]
    public long Errors { get; set; }

    [Key("Duration")]
    public long Duration { get; set; }

    [Key("OkSummary")]
    public byte[] OkSummary { get; set; }

    [Key("ErrorSummary")]
    public byte[] ErrorSummary { get; set; }

    [Key("Synthetics")]
    public bool Synthetics { get; set; }

    [Key("TopLevelHits")]
    public long TopLevelHits { get; set; }
}
