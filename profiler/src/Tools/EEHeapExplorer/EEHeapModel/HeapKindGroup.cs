// <copyright file="HeapKindGroup.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace EEHeapModel;

/// <summary>
/// Groups the individual native-heap <c>kind</c>s into the high-level categories used by the CLR
/// runtime and SOS <c>!eeheap</c>: JIT code, loader-allocator metadata heaps, the Virtual Stub
/// Dispatch heaps, and the GC heap split into object storage, bookkeeping/handles, and free/reserve.
/// </summary>
public static class HeapKindGroup
{
    public const string Code = "Code";
    public const string Loader = "Loader";
    public const string VirtualStubDispatch = "Virtual Stub Dispatch";
    public const string GCObjectHeap = "GC Object Heap";
    public const string GCBookkeepingAndHandles = "GC Bookkeeping & Handles";
    public const string GCFreeAndReserve = "GC Free / Reserve";
    public const string Other = "Other";

    /// <summary>
    /// Maps a native-heap kind (as emitted in eeheap.json) to its high-level group.
    /// </summary>
    public static string ForKind(string kind) => kind switch
    {
        "LoaderCodeHeap" or "HostCodeHeap" => Code,

        "LowFrequencyHeap" or "HighFrequencyHeap" or "StaticsHeap" or "StubHeap"
            or "ExecutableHeap" or "FixupPrecodeHeap" or "NewStubPrecodeHeap"
            or "DynamicHelpersStubHeap" or "ThunkHeap" => Loader,

        "IndirectionCellHeap" or "LookupHeap" or "ResolveHeap" or "DispatchHeap"
            or "CacheEntryHeap" or "VtableHeap" => VirtualStubDispatch,

        "GCHeapSegment" or "NonGCHeap" => GCObjectHeap,

        "GCBookkeeping" or "HandleTable" => GCBookkeepingAndHandles,

        "GCFreeRegion" or "GCFreeGlobalHugeRegion" or "GCFreeGlobalRegion"
            or "GCFreeSohSegment" or "GCFreeUohSegment" => GCFreeAndReserve,

        _ => Other,
    };
}
