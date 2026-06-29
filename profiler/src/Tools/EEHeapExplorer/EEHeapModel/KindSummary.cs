// <copyright file="KindSummary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace EEHeapModel;

/// <summary>
/// Per-kind aggregation of native-heap regions (the top panel of the viewer).
/// </summary>
public sealed class KindSummary
{
    public string Kind { get; init; } = "Unknown";

    public int Count { get; init; }

    public ulong ReservedTotal { get; init; }

    public ulong CommittedTotal { get; init; }

    /// <summary>
    /// Aggregates a report into one <see cref="KindSummary"/> per kind, sorted by reserved size
    /// descending ("sorted by size").
    /// </summary>
    public static IReadOnlyList<KindSummary> BuildFromReport(EEHeapReport report)
    {
        var summaries = report.Heaps
            .GroupBy(h => h.Kind)
            .Select(g => new KindSummary
            {
                Kind = g.Key,
                Count = g.Count(),
                ReservedTotal = g.Aggregate(0ul, (acc, h) => acc + h.Reserved),
                CommittedTotal = g.Aggregate(0ul, (acc, h) => acc + h.Committed),
            })
            .OrderByDescending(s => s.ReservedTotal)
            .ToList();

        return summaries;
    }
}
