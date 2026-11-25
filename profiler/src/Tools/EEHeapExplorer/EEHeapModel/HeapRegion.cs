// <copyright file="HeapRegion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace EEHeapModel;

/// <summary>
/// A single CLR native-heap region, as emitted in eeheap.json by the native profiler.
/// </summary>
public sealed class HeapRegion
{
    /// <summary>
    /// Region base address, as a hex string (e.g. "0x7ffb1234000").
    /// </summary>
    public string AddressHex { get; init; } = "0x0";

    /// <summary>
    /// Reserved (virtual) span of the region in bytes (the JSON "size").
    /// </summary>
    public ulong Reserved { get; init; }

    /// <summary>
    /// Committed bytes within the region (the JSON "committed").
    /// </summary>
    public ulong Committed { get; init; }

    /// <summary>
    /// Heap kind (e.g. LoaderCodeHeap, GCHeapSegment, GCFreeRegion).
    /// </summary>
    public string Kind { get; init; } = "Unknown";

    /// <summary>
    /// High-level group this kind belongs to (the JSON "group"). Emitted by the native profiler;
    /// derived from <see cref="Kind"/> via <see cref="HeapKindGroup.ForKind"/> for older reports.
    /// </summary>
    public string Group { get; init; } = HeapKindGroup.Other;

    /// <summary>
    /// Region state (Active, Reserved, Inactive, RegionOfRegions, ...).
    /// </summary>
    public string State { get; init; } = "None";

    /// <summary>
    /// GC heap index for GC regions, or -1 when not GC-heap-specific.
    /// </summary>
    public int GcHeap { get; init; } = -1;

    /// <summary>
    /// Generation for per-generation managed-heap regions, or -1 otherwise.
    /// </summary>
    public int Generation { get; init; } = -1;
}
