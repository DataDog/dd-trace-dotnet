// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CdacTarget.h"
#include "ClrNativeHeapInfo.h"

#include <functional>
#include <set>
#include <string>
#include <vector>

namespace cdac
{
// GC data contract ("c1"): GC-owned native memory regions (free regions, handle-table segments,
// bookkeeping card tables), ported from dotnet/runtime's GC_1. Every global/type/field is Has*-guarded
// so older descriptors (which may not publish this data) degrade to an empty result.
class GCContract
{
public:
    using Sink = std::function<void(const ClrNativeHeapInfo&)>;

    explicit GCContract(Target& target) :
        _target(target)
    {
    }

    void GetGCFreeRegions(const Sink& sink);
    void GetHandleTableMemoryRegions(const Sink& sink);
    void GetGCBookkeepingMemoryRegions(const Sink& sink);

    // Per-generation allocated GC segments (parity with the DAC backend's AddGcSegments). Walks each
    // generation's StartSegment -> HeapSegment.Next chain, emitting one GCHeapSegment per segment
    // carrying reserved + committed spans, the heap index, and the generation. Deduped per heap.
    void GetGCHeapSegments(const Sink& sink);

private:
    // Classification of a free GC memory region (mirrors the runtime's free_region_kind).
    enum class FreeRegionKind
    {
        FreeUnknownRegion = 0,
        FreeGlobalHugeRegion = 1,
        FreeGlobalRegion = 2,
        FreeRegion = 3,
        FreeSohSegment = 4,
        FreeUohSegment = 5,
    };

    // Safety caps mirroring the native DAC, to bound traversals on corrupt memory.
    static constexpr int MaxHandleTableRegions = 8192;
    static constexpr int MaxBookkeepingRegions = 32;
    static constexpr int MaxSegmentListIterations = 65536;
    static constexpr int MaxFreeRegionKinds = 16;

    std::vector<std::string> GetGCIdentifiers();
    bool IsServer();
    bool IsWorkstation();
    uint32_t GetGCHeapCount();

    void AddFreeList(uintptr_t freeListAddr, FreeRegionKind kind, const Sink& sink, int heap = 0);
    void AddSegmentList(uintptr_t start, FreeRegionKind kind, const Sink& sink, int heap = 0);
    void AddCardTable(uintptr_t cardTableInfoAddr, const Sink& sink);
    void AddGenerationSegments(uintptr_t genTableBase, int genSize, uint32_t genCount, int heap,
                               std::set<uintptr_t>& visited, const Sink& sink);

    static NativeHeapKind MapFreeRegionKind(FreeRegionKind kind);

    Target& _target;
};
} // namespace cdac
