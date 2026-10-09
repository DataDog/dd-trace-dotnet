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
    /// This kind's share of the report's total reserved bytes (0..1).
    /// </summary>
    public double ReservedFraction { get; init; }

    /// <summary>
    /// This kind's share of the report's total committed bytes (0..1).
    /// </summary>
    public double CommittedFraction { get; init; }

    /// <summary>
    /// High-level group this kind belongs to (see <see cref="HeapKindGroup"/>).
    /// </summary>
    public string Group { get; init; } = HeapKindGroup.Other;

    /// <summary>
    /// Total committed bytes of the whole group this kind belongs to (used to order groups by size).
    /// </summary>
    public ulong GroupCommittedTotal { get; init; }

    /// <summary>
    /// Aggregates a report into one <see cref="KindSummary"/> per kind, sorted by committed size
    /// descending ("sorted by size").
    /// </summary>
    public static IReadOnlyList<KindSummary> BuildFromReport(EEHeapReport report)
    {
        ulong totalReserved = report.TotalReserved;
        ulong totalCommitted = report.TotalCommitted;

        var perKind = report.Heaps
            .GroupBy(h => h.Kind)
            .Select(g =>
            {
                ulong reserved = g.Aggregate(0ul, (acc, h) => acc + h.Reserved);
                ulong committed = g.Aggregate(0ul, (acc, h) => acc + h.Committed);

                // All regions of a kind share the same group; take it from the region (populated from
                // the report's "group" field, or the local fallback mapping when it is absent).
                return (Kind: g.Key, Count: g.Count(), Reserved: reserved, Committed: committed, Group: g.First().Group);
            })
            .ToList();

        var groupCommitted = new Dictionary<string, ulong>();
        foreach (var k in perKind)
        {
            groupCommitted.TryGetValue(k.Group, out ulong sum);
            groupCommitted[k.Group] = sum + k.Committed;
        }

        return perKind
            .Select(k => new KindSummary
            {
                Kind = k.Kind,
                Count = k.Count,
                ReservedTotal = k.Reserved,
                CommittedTotal = k.Committed,
                ReservedFraction = totalReserved == 0 ? 0 : (double)k.Reserved / totalReserved,
                CommittedFraction = totalCommitted == 0 ? 0 : (double)k.Committed / totalCommitted,
                Group = k.Group,
                GroupCommittedTotal = groupCommitted[k.Group],
            })
            .OrderByDescending(s => s.CommittedTotal)
            .ToList();
    }
}
