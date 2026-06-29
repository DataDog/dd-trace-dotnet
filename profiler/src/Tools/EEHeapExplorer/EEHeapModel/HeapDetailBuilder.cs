// <copyright file="HeapDetailBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace EEHeapModel;

/// <summary>
/// Builds the detail rows shown in the bottom panel for a selected kind. Managed/GC regions that
/// carry a heap index or generation are grouped per (heap, generation); everything else is listed
/// region by region.
/// </summary>
public static class HeapDetailBuilder
{
    /// <summary>
    /// Builds the detail rows for the regions of a given kind in the report.
    /// </summary>
    public static IReadOnlyList<HeapDetailRow> BuildForKind(EEHeapReport report, string kind)
    {
        var regions = report.Heaps.Where(h => h.Kind == kind).ToList();
        if (regions.Count == 0)
        {
            return Array.Empty<HeapDetailRow>();
        }

        bool grouped = regions.Any(r => r.Generation >= 0 || r.GcHeap >= 0);
        return grouped ? BuildGrouped(regions) : BuildRegionList(regions);
    }

    /// <summary>
    /// Maps a generation index to its display name (0/1/2 -> Gen0/Gen1/Gen2, 3 -> LOH, 4 -> POH).
    /// </summary>
    public static string GenerationName(int generation)
    {
        return generation switch
        {
            0 => "Gen0",
            1 => "Gen1",
            2 => "Gen2",
            3 => "LOH",
            4 => "POH",
            _ => $"Gen{generation}",
        };
    }

    private static IReadOnlyList<HeapDetailRow> BuildGrouped(List<HeapRegion> regions)
    {
        return regions
            .GroupBy(r => (r.GcHeap, r.Generation))
            .Select(g => new HeapDetailRow
            {
                Label = BuildLabel(g.Key.GcHeap, g.Key.Generation),
                Count = g.Count(),
                Reserved = g.Aggregate(0ul, (acc, h) => acc + h.Reserved),
                Committed = g.Aggregate(0ul, (acc, h) => acc + h.Committed),
            })
            .OrderBy(r => r.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<HeapDetailRow> BuildRegionList(List<HeapRegion> regions)
    {
        return regions
            .OrderByDescending(r => r.Reserved)
            .Select(r => new HeapDetailRow
            {
                Label = r.AddressHex,
                State = r.State,
                Count = 1,
                Reserved = r.Reserved,
                Committed = r.Committed,
            })
            .ToList();
    }

    private static string BuildLabel(int gcHeap, int generation)
    {
        if (gcHeap >= 0 && generation >= 0)
        {
            return $"Heap {gcHeap} / {GenerationName(generation)}";
        }

        if (gcHeap >= 0)
        {
            return $"Heap {gcHeap}";
        }

        if (generation >= 0)
        {
            return GenerationName(generation);
        }

        return "(all)";
    }
}
