// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CdacGCContract.h"

#include <algorithm>
#include <cctype>

namespace cdac
{
namespace
{
std::string Trim(const std::string& s)
{
    size_t start = 0;
    size_t end = s.size();
    while (start < end && std::isspace(static_cast<unsigned char>(s[start])))
    {
        start++;
    }
    while (end > start && std::isspace(static_cast<unsigned char>(s[end - 1])))
    {
        end--;
    }
    return s.substr(start, end - start);
}
} // namespace

std::vector<std::string> GCContract::GetGCIdentifiers()
{
    std::vector<std::string> result;
    if (!_target.HasGlobal("GCIdentifiers"))
    {
        return result;
    }

    std::string ids = _target.ReadGlobalString("GCIdentifiers");
    size_t pos = 0;
    while (pos <= ids.size())
    {
        size_t comma = ids.find(',', pos);
        std::string token = (comma == std::string::npos) ? ids.substr(pos) : ids.substr(pos, comma - pos);
        token = Trim(token);
        if (!token.empty())
        {
            result.push_back(token);
        }
        if (comma == std::string::npos)
        {
            break;
        }
        pos = comma + 1;
    }
    return result;
}

bool GCContract::IsServer()
{
    auto ids = GetGCIdentifiers();
    return std::find(ids.begin(), ids.end(), "server") != ids.end();
}

bool GCContract::IsWorkstation()
{
    auto ids = GetGCIdentifiers();
    return std::find(ids.begin(), ids.end(), "workstation") != ids.end();
}

uint32_t GCContract::GetGCHeapCount()
{
    auto ids = GetGCIdentifiers();
    if (std::find(ids.begin(), ids.end(), "workstation") != ids.end())
    {
        return 1; // WRK_HEAP_COUNT
    }
    if (std::find(ids.begin(), ids.end(), "server") != ids.end())
    {
        return static_cast<uint32_t>(_target.Read<int32_t>(_target.ReadGlobalPointer("NumHeaps")));
    }
    return 0;
}

NativeHeapKind GCContract::MapFreeRegionKind(FreeRegionKind kind)
{
    switch (kind)
    {
        case FreeRegionKind::FreeGlobalHugeRegion: return NativeHeapKind::GCFreeGlobalHugeRegion;
        case FreeRegionKind::FreeGlobalRegion: return NativeHeapKind::GCFreeGlobalRegion;
        case FreeRegionKind::FreeRegion: return NativeHeapKind::GCFreeRegion;
        case FreeRegionKind::FreeSohSegment: return NativeHeapKind::GCFreeSohSegment;
        case FreeRegionKind::FreeUohSegment: return NativeHeapKind::GCFreeUohSegment;
        default: return NativeHeapKind::GCFreeRegion;
    }
}

void GCContract::AddSegmentList(uintptr_t start, FreeRegionKind kind, const Sink& sink, int heap)
{
    int guard = MaxSegmentListIterations;
    uintptr_t curr = start;
    while (curr != 0)
    {
        uintptr_t mem = _target.ReadFieldPointer(curr, "HeapSegment", "Mem");
        uintptr_t committed = _target.ReadFieldPointer(curr, "HeapSegment", "Committed");
        uintptr_t next = _target.ReadFieldPointer(curr, "HeapSegment", "Next");

        if (mem != 0)
        {
            uint64_t size = committed > mem ? static_cast<uint64_t>(committed - mem) : 0;
            ClrNativeHeapInfo info;
            info.Address = mem;
            info.Size = size;
            info.Committed = size; // free/committed GC regions are committed by nature
            info.Kind = MapFreeRegionKind(kind);
            info.State = NativeHeapState::Inactive;
            info.GCHeap = heap;
            sink(info);
        }

        curr = next;
        if (curr == start)
        {
            break;
        }
        if (guard-- <= 0)
        {
            break;
        }
    }
}

void GCContract::AddFreeList(uintptr_t freeListAddr, FreeRegionKind kind, const Sink& sink, int heap)
{
    if (freeListAddr == 0 || !_target.HasType("RegionFreeList"))
    {
        return;
    }

    uintptr_t head = _target.ReadFieldPointer(freeListAddr, "RegionFreeList", "HeadFreeRegion");
    if (head != 0)
    {
        AddSegmentList(head, kind, sink, heap);
    }
}

void GCContract::GetGCFreeRegions(const Sink& sink)
{
    if (!_target.HasType("HeapSegment"))
    {
        return;
    }

    int countFreeRegionKinds = 0;
    uint64_t regionFreeListSize = 0;
    uint32_t cfr = 0;
    int rflSize = 0;
    if (_target.TryReadGlobal<uint32_t>("CountFreeRegionKinds", cfr) && _target.TryGetTypeSize("RegionFreeList", rflSize))
    {
        countFreeRegionKinds = std::min(static_cast<int>(cfr), MaxFreeRegionKinds);
        regionFreeListSize = static_cast<uint64_t>(rflSize);
    }

    uintptr_t globalHuge = 0;
    if (countFreeRegionKinds > 0 && _target.TryReadGlobalPointer("GlobalFreeHugeRegions", globalHuge) && globalHuge != 0)
    {
        AddFreeList(globalHuge, FreeRegionKind::FreeGlobalHugeRegion, sink);
    }

    uintptr_t globalDecommit = 0;
    if (countFreeRegionKinds > 0 && _target.TryReadGlobalPointer("GlobalRegionsToDecommit", globalDecommit) && globalDecommit != 0)
    {
        for (int i = 0; i < countFreeRegionKinds; i++)
        {
            AddFreeList(globalDecommit + static_cast<uintptr_t>(i) * static_cast<uintptr_t>(regionFreeListSize), FreeRegionKind::FreeGlobalRegion, sink);
        }
    }

    if (IsServer())
    {
        if (!_target.HasGlobal("Heaps"))
        {
            return;
        }

        uint32_t heapCount = GetGCHeapCount();
        uintptr_t heapTable = _target.ReadPointer(_target.ReadGlobalPointer("Heaps"));
        for (uint32_t i = 0; i < heapCount; i++)
        {
            uintptr_t heapAddr = _target.ReadPointer(heapTable + static_cast<uintptr_t>(i) * static_cast<uintptr_t>(_target.PointerSize()));
            if (heapAddr == 0)
            {
                continue;
            }

            if (countFreeRegionKinds > 0 && _target.HasField("GCHeap", "FreeRegions"))
            {
                uintptr_t freeRegionsBase = _target.FieldAddress(heapAddr, "GCHeap", "FreeRegions");
                for (int j = 0; j < countFreeRegionKinds; j++)
                {
                    AddFreeList(freeRegionsBase + static_cast<uintptr_t>(j) * static_cast<uintptr_t>(regionFreeListSize), FreeRegionKind::FreeRegion, sink, static_cast<int>(i));
                }
            }

            if (_target.HasField("GCHeap", "FreeableSohSegment"))
            {
                uintptr_t soh = _target.ReadFieldPointer(heapAddr, "GCHeap", "FreeableSohSegment");
                if (soh != 0)
                {
                    AddSegmentList(soh, FreeRegionKind::FreeSohSegment, sink, static_cast<int>(i));
                }
            }

            if (_target.HasField("GCHeap", "FreeableUohSegment"))
            {
                uintptr_t uoh = _target.ReadFieldPointer(heapAddr, "GCHeap", "FreeableUohSegment");
                if (uoh != 0)
                {
                    AddSegmentList(uoh, FreeRegionKind::FreeUohSegment, sink, static_cast<int>(i));
                }
            }
        }
    }
    else
    {
        uintptr_t freeRegions = 0;
        if (countFreeRegionKinds > 0 && _target.TryReadGlobalPointer("GCHeapFreeRegions", freeRegions) && freeRegions != 0)
        {
            for (int i = 0; i < countFreeRegionKinds; i++)
            {
                AddFreeList(freeRegions + static_cast<uintptr_t>(i) * static_cast<uintptr_t>(regionFreeListSize), FreeRegionKind::FreeRegion, sink);
            }
        }

        uintptr_t sohGlobal = 0;
        if (_target.TryReadGlobalPointer("GCHeapFreeableSohSegment", sohGlobal) && sohGlobal != 0)
        {
            uintptr_t seg = _target.ReadPointer(sohGlobal);
            if (seg != 0)
            {
                AddSegmentList(seg, FreeRegionKind::FreeSohSegment, sink);
            }
        }

        uintptr_t uohGlobal = 0;
        if (_target.TryReadGlobalPointer("GCHeapFreeableUohSegment", uohGlobal) && uohGlobal != 0)
        {
            uintptr_t seg = _target.ReadPointer(uohGlobal);
            if (seg != 0)
            {
                AddSegmentList(seg, FreeRegionKind::FreeUohSegment, sink);
            }
        }
    }
}

void GCContract::AddGenerationSegments(
    uintptr_t genTableBase, int genSize, uint32_t genCount, int heap,
    std::set<uintptr_t>& visited, const Sink& sink)
{
    if (genTableBase == 0 || genSize <= 0)
    {
        return;
    }

    for (uint32_t g = 0; g < genCount; g++)
    {
        uintptr_t genAddr = genTableBase + static_cast<uintptr_t>(g) * static_cast<uintptr_t>(genSize);
        uintptr_t seg = _target.ReadFieldPointer(genAddr, "Generation", "StartSegment");

        int guard = MaxSegmentListIterations;
        while (seg != 0 && guard-- > 0)
        {
            // A region can appear in more than one generation's list; attribute it to the first
            // generation that references it (matches the DAC backend and SOS/ClrMD).
            if (!visited.insert(seg).second)
            {
                break;
            }

            uintptr_t mem = _target.ReadFieldPointer(seg, "HeapSegment", "Mem");
            uintptr_t committed = _target.ReadFieldPointer(seg, "HeapSegment", "Committed");
            uintptr_t reserved = _target.ReadFieldPointer(seg, "HeapSegment", "Reserved");
            uintptr_t next = _target.ReadFieldPointer(seg, "HeapSegment", "Next");

            if (mem != 0 && reserved > mem)
            {
                ClrNativeHeapInfo info;
                info.Address = mem;
                info.Size = static_cast<uint64_t>(reserved - mem);
                info.Committed = committed > mem ? static_cast<uint64_t>(committed - mem) : 0;
                info.Kind = NativeHeapKind::GCHeapSegment;
                info.State = NativeHeapState::Active;
                info.GCHeap = heap;
                info.Generation = static_cast<int>(g);
                sink(info);
            }

            if (next == seg)
            {
                break;
            }
            seg = next;
        }
    }
}

void GCContract::GetGCHeapSegments(const Sink& sink)
{
    if (!_target.HasGlobal("TotalGenerationCount") || !_target.HasType("Generation") || !_target.HasType("HeapSegment"))
    {
        return;
    }

    int genSize = 0;
    if (!_target.TryGetTypeSize("Generation", genSize) || genSize <= 0)
    {
        return;
    }

    // TotalGenerationCount is a literal constant baked into the (already-validated) contract
    // descriptor - it is the runtime's own total_generation_count (max_generation + 3), not a value
    // read from live target memory - so it is authoritative and needs no arbitrary ceiling.
    uint32_t genCount = _target.ReadGlobal<uint32_t>("TotalGenerationCount");
    if (genCount == 0)
    {
        return;
    }

    if (IsServer())
    {
        // GenerationTable is an inline array of Generation structs inside each server gc_heap.
        if (!_target.HasGlobal("Heaps") || !_target.HasField("GCHeap", "GenerationTable"))
        {
            return;
        }

        uint32_t heapCount = GetGCHeapCount();
        uintptr_t heapTable = _target.ReadPointer(_target.ReadGlobalPointer("Heaps"));
        if (heapTable == 0)
        {
            return;
        }

        for (uint32_t i = 0; i < heapCount; i++)
        {
            uintptr_t heapAddr = _target.ReadPointer(heapTable + static_cast<uintptr_t>(i) * static_cast<uintptr_t>(_target.PointerSize()));
            if (heapAddr == 0)
            {
                continue;
            }

            uintptr_t genTableBase = _target.FieldAddress(heapAddr, "GCHeap", "GenerationTable");
            std::set<uintptr_t> visited;
            AddGenerationSegments(genTableBase, genSize, genCount, static_cast<int>(i), visited, sink);
        }
    }
    else
    {
        // Workstation: the single heap's generation table is exposed as a global.
        uintptr_t genTableBase = 0;
        if (!_target.TryReadGlobalPointer("GCHeapGenerationTable", genTableBase) || genTableBase == 0)
        {
            return;
        }

        std::set<uintptr_t> visited;
        AddGenerationSegments(genTableBase, genSize, genCount, 0, visited, sink);
    }
}

void GCContract::GetHandleTableMemoryRegions(const Sink& sink)
{
    if (!_target.HasGlobal("HandleTableMap") || !_target.HasGlobal("HandleSegmentSize") || !_target.HasGlobal("InitialHandleTableArraySize") || !_target.HasType("HandleTableMap") || !_target.HasType("HandleTableBucket") || !_target.HasType("HandleTable") || !_target.HasType("TableSegment"))
    {
        return;
    }

    uint64_t handleSegmentSize = _target.ReadGlobal<uint64_t>("HandleSegmentSize");
    uint32_t bucketArrayLength = _target.ReadGlobal<uint32_t>("InitialHandleTableArraySize");
    uint32_t tableCount = IsServer()
                              ? (_target.HasGlobal("TotalCpuCount") ? _target.Read<uint32_t>(_target.ReadGlobalPointer("TotalCpuCount")) : 0u)
                              : 1u;

    int maxRegions = MaxHandleTableRegions;
    uintptr_t handleTableMap = _target.ReadGlobalPointer("HandleTableMap");
    while (handleTableMap != 0 && maxRegions-- > 0)
    {
        uintptr_t bucketsPtr = _target.ReadFieldPointer(handleTableMap, "HandleTableMap", "BucketsPtr");
        for (uint32_t b = 0; bucketsPtr != 0 && b < bucketArrayLength; b++)
        {
            uintptr_t bucketPtr = _target.ReadPointer(bucketsPtr + static_cast<uintptr_t>(b) * static_cast<uintptr_t>(_target.PointerSize()));
            if (bucketPtr == 0)
            {
                continue;
            }

            uintptr_t table = _target.ReadFieldPointer(bucketPtr, "HandleTableBucket", "Table");
            if (table == 0)
            {
                continue;
            }

            for (uint32_t j = 0; j < tableCount; j++)
            {
                uintptr_t handleTablePtr = _target.ReadPointer(table + static_cast<uintptr_t>(j) * static_cast<uintptr_t>(_target.PointerSize()));
                if (handleTablePtr == 0)
                {
                    continue;
                }

                uintptr_t segmentPtr = _target.ReadFieldPointer(handleTablePtr, "HandleTable", "SegmentList");
                if (segmentPtr == 0)
                {
                    continue;
                }

                uintptr_t firstSegment = segmentPtr;
                int segGuard = MaxSegmentListIterations;
                do
                {
                    ClrNativeHeapInfo info;
                    info.Address = segmentPtr;
                    info.Size = handleSegmentSize;
                    info.Committed = handleSegmentSize;
                    info.Kind = NativeHeapKind::HandleTable;
                    info.State = NativeHeapState::Active;
                    info.GCHeap = static_cast<int>(j);
                    sink(info);

                    segmentPtr = _target.ReadFieldPointer(segmentPtr, "TableSegment", "NextSegment");
                } while (segmentPtr != 0 && segmentPtr != firstSegment && --segGuard > 0);
            }
        }

        handleTableMap = _target.ReadFieldPointer(handleTableMap, "HandleTableMap", "Next");
    }
}

void GCContract::AddCardTable(uintptr_t cardTableInfoAddr, const Sink& sink)
{
    uint32_t recount = _target.ReadField<uint32_t>(cardTableInfoAddr, "CardTableInfo", "Recount");
    uintptr_t size = _target.ReadFieldPointer(cardTableInfoAddr, "CardTableInfo", "Size");
    if (recount != 0 && size != 0)
    {
        ClrNativeHeapInfo info;
        info.Address = cardTableInfoAddr;
        info.Size = static_cast<uint64_t>(size);
        info.Committed = static_cast<uint64_t>(size);
        info.Kind = NativeHeapKind::GCBookkeeping;
        info.State = NativeHeapState::RegionOfRegions;
        sink(info);
    }
}

void GCContract::GetGCBookkeepingMemoryRegions(const Sink& sink)
{
    if (!_target.HasGlobal("BookkeepingStart") || !_target.HasGlobal("CardTableInfoSize") || !_target.HasType("CardTableInfo"))
    {
        return;
    }

    uintptr_t bookkeepingStartGlobal = _target.ReadGlobalPointer("BookkeepingStart");
    if (bookkeepingStartGlobal == 0)
    {
        return;
    }

    uintptr_t bookkeepingStart = _target.ReadPointer(bookkeepingStartGlobal);
    if (bookkeepingStart == 0)
    {
        return;
    }

    uint32_t cardTableInfoSize = _target.ReadGlobal<uint32_t>("CardTableInfoSize");

    AddCardTable(bookkeepingStart, sink);

    uintptr_t next = _target.ReadFieldPointer(bookkeepingStart, "CardTableInfo", "NextCardTable");
    uintptr_t firstNext = next;
    int maxRegions = MaxBookkeepingRegions;

    // `next > cardTableInfoSize` guards against underflow when subtracting (matches native DAC).
    while (next != 0 && next > cardTableInfoSize && maxRegions-- > 0)
    {
        uintptr_t ctAddr = next - cardTableInfoSize;
        AddCardTable(ctAddr, sink);

        next = _target.ReadFieldPointer(ctAddr, "CardTableInfo", "NextCardTable");
        if (next == firstNext)
        {
            break;
        }
    }
}
} // namespace cdac
