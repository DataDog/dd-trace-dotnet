// <copyright file="HeapDetailRow.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace EEHeapModel;

/// <summary>
/// One row of the detail panel for a selected kind. Either a per-(heap, generation) aggregate for
/// the managed/GC heap, or a single region listing otherwise.
/// </summary>
public sealed class HeapDetailRow
{
    /// <summary>
    /// Human-readable label: "Heap N / GenX" (grouped) or the region address (ungrouped).
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Region state, when listing individual regions; null for grouped rows.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Number of regions aggregated in this row (1 for ungrouped rows).
    /// </summary>
    public int Count { get; init; }

    public ulong Reserved { get; init; }

    public ulong Committed { get; init; }

    /// <summary>
    /// This row's share of the selected kind's total reserved bytes (0..1).
    /// </summary>
    public double ReservedFraction { get; init; }

    /// <summary>
    /// This row's share of the selected kind's total committed bytes (0..1).
    /// </summary>
    public double CommittedFraction { get; init; }
}
