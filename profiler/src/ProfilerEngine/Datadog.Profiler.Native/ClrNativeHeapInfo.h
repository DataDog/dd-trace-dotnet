// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>

// Kind of a CLR native heap region. Mirrors Microsoft.Diagnostics.Runtime.NativeHeapKind (ClrMD)
// plus the heap kinds introduced with the .NET 11 cDAC Loader contract. Both the cDAC and the DAC
// backends emit these so the reporter/exporter/JSON wiring is shared.
enum class NativeHeapKind
{
    Unknown,
    IndirectionCellHeap,
    LookupHeap,
    ResolveHeap,
    DispatchHeap,
    CacheEntryHeap,
    VtableHeap,
    LoaderCodeHeap,
    HostCodeHeap,
    StubHeap,
    HighFrequencyHeap,
    LowFrequencyHeap,
    StaticsHeap,
    ExecutableHeap,
    FixupPrecodeHeap,
    NewStubPrecodeHeap,
    DynamicHelpersStubHeap,
    ThunkHeap,
    HandleTable,
    GCBookkeeping,
    GCHeapSegment,
    NonGCHeap,
    GCFreeRegion,
    GCFreeGlobalHugeRegion,
    GCFreeGlobalRegion,
    GCFreeSohSegment,
    GCFreeUohSegment,
};

// High-level group a native heap kind belongs to. These mirror the categories used by the CLR
// runtime and SOS !eeheap: JIT code, loader-allocator metadata heaps, the Virtual Stub Dispatch
// heaps, and the GC heap split into object storage, bookkeeping/handles, and free/reserve.
// NOTE: kept in sync with the EEHeapExplorer tool's HeapKindGroup
// (profiler/src/Tools/EEHeapExplorer/EEHeapModel/HeapKindGroup.cs). Update both together.
enum class NativeHeapGroup
{
    Code,
    Loader,
    VirtualStubDispatch,
    GCObjectHeap,
    GCBookkeepingAndHandles,
    GCFreeAndReserve,
    Other,
};

// Extra state for a native heap region (mirrors ClrMD's ClrNativeHeapState).
enum class NativeHeapState
{
    None,
    Active,
    Inactive,
    RegionOfRegions,
    Reserved,
};

// A region of native memory allocated by the CLR. Equivalent to ClrMD's ClrNativeHeapInfo,
// produced here entirely from either the cDAC contracts or the DAC (ISOSDacInterface).
struct ClrNativeHeapInfo
{
    uintptr_t Address = 0;
    uint64_t Size = 0; // reserved / virtual span of the region
    uint64_t Committed = 0; // committed bytes within [Address, Address + Size)
    NativeHeapKind Kind = NativeHeapKind::Unknown;
    NativeHeapState State = NativeHeapState::None;
    int GCHeap = -1; // -1 when not GC-heap-specific
    int Generation = -1; // -1 when not a per-generation managed-heap region
};

inline const char* ToString(NativeHeapKind kind)
{
    switch (kind)
    {
        case NativeHeapKind::IndirectionCellHeap: return "IndirectionCellHeap";
        case NativeHeapKind::LookupHeap: return "LookupHeap";
        case NativeHeapKind::ResolveHeap: return "ResolveHeap";
        case NativeHeapKind::DispatchHeap: return "DispatchHeap";
        case NativeHeapKind::CacheEntryHeap: return "CacheEntryHeap";
        case NativeHeapKind::VtableHeap: return "VtableHeap";
        case NativeHeapKind::LoaderCodeHeap: return "LoaderCodeHeap";
        case NativeHeapKind::HostCodeHeap: return "HostCodeHeap";
        case NativeHeapKind::StubHeap: return "StubHeap";
        case NativeHeapKind::HighFrequencyHeap: return "HighFrequencyHeap";
        case NativeHeapKind::LowFrequencyHeap: return "LowFrequencyHeap";
        case NativeHeapKind::StaticsHeap: return "StaticsHeap";
        case NativeHeapKind::ExecutableHeap: return "ExecutableHeap";
        case NativeHeapKind::FixupPrecodeHeap: return "FixupPrecodeHeap";
        case NativeHeapKind::NewStubPrecodeHeap: return "NewStubPrecodeHeap";
        case NativeHeapKind::DynamicHelpersStubHeap: return "DynamicHelpersStubHeap";
        case NativeHeapKind::ThunkHeap: return "ThunkHeap";
        case NativeHeapKind::HandleTable: return "HandleTable";
        case NativeHeapKind::GCBookkeeping: return "GCBookkeeping";
        case NativeHeapKind::GCHeapSegment: return "GCHeapSegment";
        case NativeHeapKind::NonGCHeap: return "NonGCHeap";
        case NativeHeapKind::GCFreeRegion: return "GCFreeRegion";
        case NativeHeapKind::GCFreeGlobalHugeRegion: return "GCFreeGlobalHugeRegion";
        case NativeHeapKind::GCFreeGlobalRegion: return "GCFreeGlobalRegion";
        case NativeHeapKind::GCFreeSohSegment: return "GCFreeSohSegment";
        case NativeHeapKind::GCFreeUohSegment: return "GCFreeUohSegment";
        default: return "Unknown";
    }
}

inline const char* ToString(NativeHeapState state)
{
    switch (state)
    {
        case NativeHeapState::Active: return "Active";
        case NativeHeapState::Inactive: return "Inactive";
        case NativeHeapState::RegionOfRegions: return "RegionOfRegions";
        case NativeHeapState::Reserved: return "Reserved";
        default: return "None";
    }
}

// Maps a native heap kind to its high-level group. The group is fully determined by the kind, so it
// is derived on demand rather than stored on each region. Mirrors HeapKindGroup.ForKind in the
// EEHeapExplorer tool - keep both in sync.
inline NativeHeapGroup GroupOf(NativeHeapKind kind)
{
    switch (kind)
    {
        case NativeHeapKind::LoaderCodeHeap:
        case NativeHeapKind::HostCodeHeap:
            return NativeHeapGroup::Code;

        case NativeHeapKind::StubHeap:
        case NativeHeapKind::HighFrequencyHeap:
        case NativeHeapKind::LowFrequencyHeap:
        case NativeHeapKind::StaticsHeap:
        case NativeHeapKind::ExecutableHeap:
        case NativeHeapKind::FixupPrecodeHeap:
        case NativeHeapKind::NewStubPrecodeHeap:
        case NativeHeapKind::DynamicHelpersStubHeap:
        case NativeHeapKind::ThunkHeap:
            return NativeHeapGroup::Loader;

        case NativeHeapKind::IndirectionCellHeap:
        case NativeHeapKind::LookupHeap:
        case NativeHeapKind::ResolveHeap:
        case NativeHeapKind::DispatchHeap:
        case NativeHeapKind::CacheEntryHeap:
        case NativeHeapKind::VtableHeap:
            return NativeHeapGroup::VirtualStubDispatch;

        case NativeHeapKind::GCHeapSegment:
        case NativeHeapKind::NonGCHeap:
            return NativeHeapGroup::GCObjectHeap;

        case NativeHeapKind::HandleTable:
        case NativeHeapKind::GCBookkeeping:
            return NativeHeapGroup::GCBookkeepingAndHandles;

        case NativeHeapKind::GCFreeRegion:
        case NativeHeapKind::GCFreeGlobalHugeRegion:
        case NativeHeapKind::GCFreeGlobalRegion:
        case NativeHeapKind::GCFreeSohSegment:
        case NativeHeapKind::GCFreeUohSegment:
            return NativeHeapGroup::GCFreeAndReserve;

        default:
            return NativeHeapGroup::Other;
    }
}

inline const char* ToString(NativeHeapGroup group)
{
    switch (group)
    {
        case NativeHeapGroup::Code: return "Code";
        case NativeHeapGroup::Loader: return "Loader";
        case NativeHeapGroup::VirtualStubDispatch: return "Virtual Stub Dispatch";
        case NativeHeapGroup::GCObjectHeap: return "GC Object Heap";
        case NativeHeapGroup::GCBookkeepingAndHandles: return "GC Bookkeeping & Handles";
        case NativeHeapGroup::GCFreeAndReserve: return "GC Free / Reserve";
        default: return "Other";
    }
}
