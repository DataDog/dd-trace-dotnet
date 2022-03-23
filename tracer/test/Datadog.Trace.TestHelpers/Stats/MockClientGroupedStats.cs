// <copyright file="MockClientGroupedStats.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using MessagePack;

namespace Datadog.Trace.TestHelpers.Stats;

[MessagePackObject]
public class MockClientGroupedStats
{
    [Key("service")]
    public string Service { get; set; }

    [Key("name")]
    public string Name { get; set; }

    [Key("resource")]
    public string Resource { get; set; }

    [Key("HTTP_status_code")]
    public int HttpStatusCode { get; set; }

    [Key("type")]
    public string Type { get; set; }

    [Key("DB_type")]
    public string DbType { get; set; }

    [Key("hits")]
    public long Hits { get; set; }

    [Key("errors")]
    public long Errors { get; set; }

    [Key("duration")]
    public long Duration { get; set; }

    [Key("okSummary")]
    public byte[] OkSummary { get; set; }

    [Key("errorSummary")]
    public byte[] ErrorSummary { get; set; }

    [Key("synthetics")]
    public bool Synthetics { get; set; }

    [Key("topLevelHits")]
    public long TopLevelhits { get; set; }
}
