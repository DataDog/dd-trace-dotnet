// <copyright file="EEHeapReport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace EEHeapModel;

/// <summary>
/// A parsed eeheap.json report: the producing backend plus the list of native-heap regions.
/// </summary>
public sealed class EEHeapReport
{
    /// <summary>
    /// Producing backend ("cdac" or "dac").
    /// </summary>
    public string Source { get; init; } = "unknown";

    /// <summary>
    /// All native-heap regions in the report.
    /// </summary>
    public IReadOnlyList<HeapRegion> Heaps { get; init; } = Array.Empty<HeapRegion>();

    /// <summary>
    /// Total reserved (virtual) bytes across all regions.
    /// </summary>
    public ulong TotalReserved
    {
        get
        {
            ulong total = 0;
            foreach (var heap in Heaps)
            {
                total += heap.Reserved;
            }

            return total;
        }
    }

    /// <summary>
    /// Total committed bytes across all regions.
    /// </summary>
    public ulong TotalCommitted
    {
        get
        {
            ulong total = 0;
            foreach (var heap in Heaps)
            {
                total += heap.Committed;
            }

            return total;
        }
    }
}
