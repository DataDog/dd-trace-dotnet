// <copyright file="MockDataStreamsPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using MessagePack;

namespace Datadog.Trace.TestHelpers.DataStreamsMonitoring;

[MessagePackObject]
public class MockDataStreamsPayload
{
    [Key(nameof(Env))]
    public string Env { get; set; }

    // Service is the service of the application
    [Key(nameof(Service))]
    public string Service { get; set; }

    [Key(nameof(PrimaryTag))]
    public string PrimaryTag { get; set; }

    [Key(nameof(TracerVersion))]
    public string TracerVersion { get; set; }

    [Key(nameof(Lang))]
    public string Lang { get; set; }

    [Key(nameof(Stats))]
    public MockDataStreamsBucket[] Stats { get; set; }

    public static IList<MockDataStreamsStatsPoint> ToPoints(IImmutableList<MockDataStreamsPayload> payloads)
    {
        var points = new List<MockDataStreamsStatsPoint>();
        foreach (var payload in payloads)
        {
            foreach (var bucket in payload.Stats)
            {
                if (bucket.Stats != null)
                {
                    points.AddRange(bucket.Stats);
                }
            }
        }

        return points.OrderBy(s => s.Hash).ThenBy(s => s.TimestampType).ToList();
    }
}
