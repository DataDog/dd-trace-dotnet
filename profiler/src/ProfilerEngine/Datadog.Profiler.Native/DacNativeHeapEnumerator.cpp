// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "DacNativeHeapEnumerator.h"

#include "CommittedMemoryProbe.h"
#include "IRuntimeInfo.h"
#include "InProcessMemoryReader.h"
#include "Log.h"

#ifdef _WINDOWS
// Needed so CONTEXT is defined before the DAC headers reference T_CONTEXT (see the alias below).
#include <windows.h>
#endif

// On non-Windows, define TARGET_UNIX/HOST_UNIX before the DAC headers so the Dacp* struct layouts
// match the Linux runtime (and so dacprivate.h does not pull in the Windows-only <msodw.h>). This
// mirrors how coreclr itself compiles these headers.
#if !defined(_WINDOWS)
#ifndef TARGET_UNIX
#define TARGET_UNIX
#endif
#ifndef HOST_UNIX
#define HOST_UNIX
#endif
#endif

// sospriv.h (generated from sospriv.idl) refers to T_CONTEXT, but its in-file definition is disabled
// (#if 0): coreclr normally supplies it via crosscomp.h (#define T_CONTEXT CONTEXT). We only ever pass
// it as an opaque pointer in an interface slot we never call, so alias it to the platform CONTEXT to
// match coreclr. CONTEXT comes from <windows.h> (Windows) / the PAL (Unix).
#ifndef T_CONTEXT
#define T_CONTEXT CONTEXT
#endif

// DAC/coreclr-internal headers are isolated to this translation unit. dacprivate.h pulls in
// cor.h/clrdata.h/xclrdata.h/sospriv.h and the Dacp* structs + IsMiIL().
#include "dacprivate.h"

// Vendored ISOSDacInterface13 / ISOSMemoryEnum / SOSMemoryRegion (the vendored sospriv.h stops at
// 12). Must come after sospriv.h (above) for ISOSEnum / VISITHEAP / MIDL_INTERFACE.
#include "DacInterface13.h"

#include <functional>
#include <set>
#include <vector>

namespace
{
// ---- captureless VISITHEAP trampoline -------------------------------------------------------
// VISITHEAP is "void (*)(CLRDATA_ADDRESS, size_t, BOOL)" - a captureless function pointer with no
// context argument (mirroring SOS, which routes results through file-scope globals). We instead use
// a thread_local sink installed by an RAII scope for the duration of each Traverse* call.
struct TraverseSink
{
    std::vector<ClrNativeHeapInfo>* results = nullptr;
    NativeHeapKind kind = NativeHeapKind::Unknown;
    IMemoryReader* reader = nullptr;
};

thread_local TraverseSink* t_sink = nullptr;

void HeapVisitor(CLRDATA_ADDRESS blockData, size_t blockSize, BOOL blockIsCurrentBlock)
{
    TraverseSink* sink = t_sink;
    if (sink == nullptr || sink->results == nullptr)
    {
        return;
    }

    ClrNativeHeapInfo info;
    info.Address = static_cast<uintptr_t>(blockData);
    info.Size = static_cast<uint64_t>(blockSize); // reserved span of the block
    // The DAC only reports the reserved block size; recover the committed portion by probing pages
    // (the DAC target is this same process, so an in-process reader is exact).
    info.Committed = (sink->reader != nullptr)
                         ? eeheap::ProbeCommittedBytes(*sink->reader, info.Address, info.Size)
                         : info.Size;
    info.Kind = sink->kind;
    info.State = blockIsCurrentBlock ? NativeHeapState::Active : NativeHeapState::Reserved;
    sink->results->push_back(info);
}

class TraverseScope
{
public:
    TraverseScope(std::vector<ClrNativeHeapInfo>& results, NativeHeapKind kind, IMemoryReader& reader)
    {
        _sink.results = &results;
        _sink.kind = kind;
        _sink.reader = &reader;
        t_sink = &_sink;
    }

    ~TraverseScope()
    {
        t_sink = nullptr;
    }

    TraverseScope(const TraverseScope&) = delete;
    TraverseScope& operator=(const TraverseScope&) = delete;

private:
    TraverseSink _sink;
};

constexpr int MaxSegmentIterations = 65536;

// Walk one loader heap (if non-null and not already visited), routing blocks to the sink.
void WalkLoaderHeap(
    ISOSDacInterface* sos,
    CLRDATA_ADDRESS loaderHeap,
    NativeHeapKind kind,
    std::vector<ClrNativeHeapInfo>& results,
    std::set<CLRDATA_ADDRESS>& visited,
    IMemoryReader& reader)
{
    if (loaderHeap == 0 || !visited.insert(loaderHeap).second)
    {
        return;
    }

    TraverseScope scope(results, kind, reader);
    sos->TraverseLoaderHeap(loaderHeap, &HeapVisitor);
}

void WalkVcsHeap(
    ISOSDacInterface* sos,
    CLRDATA_ADDRESS appDomain,
    VCSHeapType heapType,
    NativeHeapKind kind,
    std::vector<ClrNativeHeapInfo>& results,
    IMemoryReader& reader)
{
    TraverseScope scope(results, kind, reader);
    sos->TraverseVirtCallStubHeap(appDomain, heapType, &HeapVisitor);
}

// --- JIT code heaps (JitHeapInfo) ---
void EnumerateCodeHeaps(ISOSDacInterface* sos, std::vector<ClrNativeHeapInfo>& results, IMemoryReader& reader)
{
    unsigned int needed = 0;
    if (FAILED(sos->GetJitManagerList(0, nullptr, &needed)) || needed == 0)
    {
        return;
    }

    std::vector<DacpJitManagerInfo> managers(needed);
    if (FAILED(sos->GetJitManagerList(needed, managers.data(), &needed)))
    {
        return;
    }

    std::set<CLRDATA_ADDRESS> visited;
    for (unsigned int i = 0; i < needed && i < managers.size(); i++)
    {
        // Only real (IL/JIT) managers expose code heaps; native/unknown managers are ignored.
        if (!IsMiIL(managers[i].codeType))
        {
            continue;
        }

        unsigned int heapCount = 0;
        if (FAILED(sos->GetCodeHeapList(managers[i].managerAddr, 0, nullptr, &heapCount)) || heapCount == 0)
        {
            continue;
        }

        std::vector<DacpJitCodeHeapInfo> codeHeaps(heapCount);
        if (FAILED(sos->GetCodeHeapList(managers[i].managerAddr, heapCount, codeHeaps.data(), &heapCount)))
        {
            continue;
        }

        for (unsigned int h = 0; h < heapCount && h < codeHeaps.size(); h++)
        {
            const DacpJitCodeHeapInfo& heap = codeHeaps[h];
            if (heap.codeHeapType == CODEHEAP_LOADER)
            {
                WalkLoaderHeap(sos, heap.LoaderHeap, NativeHeapKind::LoaderCodeHeap, results, visited, reader);
            }
            else if (heap.codeHeapType == CODEHEAP_HOST)
            {
                if (heap.HostData.currentAddr >= heap.HostData.baseAddr && heap.HostData.baseAddr != 0)
                {
                    ClrNativeHeapInfo info;
                    info.Address = static_cast<uintptr_t>(heap.HostData.baseAddr);
                    info.Size = static_cast<uint64_t>(heap.HostData.currentAddr - heap.HostData.baseAddr);
                    info.Committed = eeheap::ProbeCommittedBytes(reader, info.Address, info.Size);
                    info.Kind = NativeHeapKind::HostCodeHeap;
                    info.State = NativeHeapState::Active;
                    results.push_back(info);
                }
            }
        }
    }
}

// Walk a single domain's three loader heaps in SOS order (low -> high -> stub).
void EnumerateDomainLoaderHeaps(
    ISOSDacInterface* sos,
    CLRDATA_ADDRESS domain,
    std::vector<ClrNativeHeapInfo>& results,
    std::set<CLRDATA_ADDRESS>& visited,
    IMemoryReader& reader)
{
    if (domain == 0)
    {
        return;
    }

    DacpAppDomainData data;
    if (FAILED(data.Request(sos, domain)))
    {
        return;
    }

    WalkLoaderHeap(sos, data.pLowFrequencyHeap, NativeHeapKind::LowFrequencyHeap, results, visited, reader);
    WalkLoaderHeap(sos, data.pHighFrequencyHeap, NativeHeapKind::HighFrequencyHeap, results, visited, reader);
    WalkLoaderHeap(sos, data.pStubHeap, NativeHeapKind::StubHeap, results, visited, reader);
}

// VCS (virtual call stub) heaps for an AppDomain (VSDHeapInfo).
void EnumerateVcsHeaps(ISOSDacInterface* sos, CLRDATA_ADDRESS appDomain, std::vector<ClrNativeHeapInfo>& results, IMemoryReader& reader)
{
    if (appDomain == 0)
    {
        return;
    }

    WalkVcsHeap(sos, appDomain, IndcellHeap, NativeHeapKind::IndirectionCellHeap, results, reader);
    WalkVcsHeap(sos, appDomain, LookupHeap, NativeHeapKind::LookupHeap, results, reader);
    WalkVcsHeap(sos, appDomain, ResolveHeap, NativeHeapKind::ResolveHeap, results, reader);
    WalkVcsHeap(sos, appDomain, DispatchHeap, NativeHeapKind::DispatchHeap, results, reader);
    WalkVcsHeap(sos, appDomain, CacheEntryHeap, NativeHeapKind::CacheEntryHeap, results, reader);
}

// Module thunk heaps for every assembly/module in an AppDomain (PrintModuleHeapInfo).
void EnumerateModuleThunkHeaps(
    ISOSDacInterface* sos,
    CLRDATA_ADDRESS appDomain,
    std::vector<ClrNativeHeapInfo>& results,
    std::set<CLRDATA_ADDRESS>& visited,
    IMemoryReader& reader)
{
    if (appDomain == 0)
    {
        return;
    }

    int assemblyCount = 0;
    if (FAILED(sos->GetAssemblyList(appDomain, 0, nullptr, &assemblyCount)) || assemblyCount <= 0)
    {
        return;
    }

    std::vector<CLRDATA_ADDRESS> assemblies(static_cast<size_t>(assemblyCount));
    if (FAILED(sos->GetAssemblyList(appDomain, assemblyCount, assemblies.data(), &assemblyCount)))
    {
        return;
    }

    for (int a = 0; a < assemblyCount && a < static_cast<int>(assemblies.size()); a++)
    {
        unsigned int moduleCount = 0;
        if (FAILED(sos->GetAssemblyModuleList(assemblies[a], 0, nullptr, &moduleCount)) || moduleCount == 0)
        {
            continue;
        }

        std::vector<CLRDATA_ADDRESS> modules(moduleCount);
        if (FAILED(sos->GetAssemblyModuleList(assemblies[a], moduleCount, modules.data(), &moduleCount)))
        {
            continue;
        }

        for (unsigned int m = 0; m < moduleCount && m < modules.size(); m++)
        {
            DacpModuleData moduleData;
            if (FAILED(moduleData.Request(sos, modules[m])))
            {
                continue;
            }

            WalkLoaderHeap(sos, moduleData.pThunkHeap, NativeHeapKind::ThunkHeap, results, visited, reader);
        }
    }
}

// --- Loader heaps + VCS + module thunk heaps (PrintDomainHeapInfo / VSDHeapInfo / PrintModuleHeapInfo) ---
void EnumerateLoaderHeaps(ISOSDacInterface* sos, std::vector<ClrNativeHeapInfo>& results, IMemoryReader& reader)
{
    DacpAppDomainStoreData stores;
    if (FAILED(stores.Request(sos)))
    {
        return;
    }

    std::set<CLRDATA_ADDRESS> visited;

    // System + shared domains first (they hold runtime-wide loader heaps).
    EnumerateDomainLoaderHeaps(sos, stores.systemDomain, results, visited, reader);
    EnumerateDomainLoaderHeaps(sos, stores.sharedDomain, results, visited, reader);

    std::vector<CLRDATA_ADDRESS> domains;
    if (stores.DomainCount > 0)
    {
        unsigned int needed = 0;
        if (SUCCEEDED(sos->GetAppDomainList(static_cast<unsigned int>(stores.DomainCount), nullptr, &needed)) && needed > 0)
        {
            domains.resize(needed);
            if (FAILED(sos->GetAppDomainList(needed, domains.data(), &needed)))
            {
                domains.clear();
            }
            else
            {
                domains.resize(needed);
            }
        }
    }

    for (CLRDATA_ADDRESS domain : domains)
    {
        EnumerateDomainLoaderHeaps(sos, domain, results, visited, reader);
        EnumerateVcsHeaps(sos, domain, results, reader);
        EnumerateModuleThunkHeaps(sos, domain, results, visited, reader);
    }
}

void AddGcSegments(
    ISOSDacInterface* sos,
    const DacpGcHeapDetails& details,
    int heapIndex,
    std::vector<ClrNativeHeapInfo>& results)
{
    std::set<CLRDATA_ADDRESS> visitedSegments;

    for (int gen = 0; gen < DAC_NUMBERGENERATIONS; gen++)
    {
        CLRDATA_ADDRESS seg = details.generation_table[gen].start_segment;
        int iterations = 0;
        while (seg != 0 && iterations++ < MaxSegmentIterations)
        {
            if (!visitedSegments.insert(seg).second)
            {
                // Already walked (segment-mode runtimes share segments across generations).
                break;
            }

            DacpHeapSegmentData segData;
            if (FAILED(segData.Request(sos, seg, details)))
            {
                break;
            }

            const uint64_t mem = static_cast<uint64_t>(segData.mem);
            const uint64_t committed = static_cast<uint64_t>(segData.committed);
            const uint64_t reserved = static_cast<uint64_t>(segData.reserved);

            // One entry per segment carrying both the reserved (virtual) and committed spans, tagged
            // with the heap index and the generation whose segment list first referenced it. The DAC
            // reports these directly, so no page probe is needed here.
            if (reserved > mem)
            {
                ClrNativeHeapInfo info;
                info.Address = static_cast<uintptr_t>(mem);
                info.Size = reserved - mem;
                info.Committed = committed > mem ? committed - mem : 0;
                info.Kind = NativeHeapKind::GCHeapSegment;
                info.State = NativeHeapState::Active;
                info.GCHeap = heapIndex;
                info.Generation = gen;
                results.push_back(info);
            }

            seg = segData.next;
        }
    }
}

// --- GC heap segments (GCHeapInfo) ---
void EnumerateGcRegions(ISOSDacInterface* sos, std::vector<ClrNativeHeapInfo>& results)
{
    DacpGcHeapData gcData;
    if (FAILED(gcData.Request(sos)))
    {
        return;
    }

    if (gcData.bServerMode)
    {
        unsigned int needed = 0;
        if (FAILED(sos->GetGCHeapList(0, nullptr, &needed)) || needed == 0)
        {
            return;
        }

        std::vector<CLRDATA_ADDRESS> heaps(needed);
        if (FAILED(sos->GetGCHeapList(needed, heaps.data(), &needed)))
        {
            return;
        }

        for (unsigned int i = 0; i < needed && i < heaps.size(); i++)
        {
            DacpGcHeapDetails details;
            if (FAILED(details.Request(sos, heaps[i])))
            {
                continue;
            }
            AddGcSegments(sos, details, static_cast<int>(i), results);
        }
    }
    else
    {
        DacpGcHeapDetails details;
        if (FAILED(details.Request(sos)))
        {
            return;
        }
        AddGcSegments(sos, details, 0, results);
    }
}

// Drains an ISOSMemoryEnum, emitting one entry per SOSMemoryRegion. The enum reports committed
// bytes; the reserved span is not exposed, so Size == Committed for these regions.
void DrainMemoryEnum(ISOSMemoryEnum* memEnum, NativeHeapKind kind, std::vector<ClrNativeHeapInfo>& results)
{
    if (memEnum == nullptr)
    {
        return;
    }

    constexpr unsigned int BatchSize = 256;
    SOSMemoryRegion regions[BatchSize];
    int guard = MaxSegmentIterations;
    unsigned int fetched = 0;

    do
    {
        fetched = 0;
        if (FAILED(memEnum->Next(BatchSize, regions, &fetched)))
        {
            break;
        }

        for (unsigned int i = 0; i < fetched && i < BatchSize; i++)
        {
            ClrNativeHeapInfo info;
            info.Address = static_cast<uintptr_t>(regions[i].Start);
            info.Size = static_cast<uint64_t>(regions[i].Size);
            info.Committed = info.Size;
            info.Kind = kind;
            info.State = NativeHeapState::Active;
            info.GCHeap = regions[i].Heap;
            results.push_back(info);
        }
    } while (fetched == BatchSize && guard-- > 0);
}

// --- GC free-regions / handle-table / bookkeeping (ISOSDacInterface13, .NET 8+) ---
// Parity with the cDAC backend. QI ISOSDacInterface13; if absent (.NET 5-7, .NET Framework) these
// sources are simply empty. The DAC's free-region enum does not subdivide into the cDAC's finer
// GCFreeGlobal*/GCFreeSoh/Uoh kinds; mapping all to GCFreeRegion is the accepted difference.
void EnumerateGcMemoryRegions(ISOSDacInterface* sos, std::vector<ClrNativeHeapInfo>& results)
{
    ISOSDacInterface13* sos13 = nullptr;
    if (FAILED(sos->QueryInterface(__uuidof(ISOSDacInterface13), reinterpret_cast<void**>(&sos13))) || sos13 == nullptr)
    {
        return;
    }

    ISOSMemoryEnum* memEnum = nullptr;
    if (SUCCEEDED(sos13->GetGCFreeRegions(&memEnum)) && memEnum != nullptr)
    {
        DrainMemoryEnum(memEnum, NativeHeapKind::GCFreeRegion, results);
        memEnum->Release();
    }

    memEnum = nullptr;
    if (SUCCEEDED(sos13->GetHandleTableMemoryRegions(&memEnum)) && memEnum != nullptr)
    {
        DrainMemoryEnum(memEnum, NativeHeapKind::HandleTable, results);
        memEnum->Release();
    }

    memEnum = nullptr;
    if (SUCCEEDED(sos13->GetGCBookkeepingMemoryRegions(&memEnum)) && memEnum != nullptr)
    {
        DrainMemoryEnum(memEnum, NativeHeapKind::GCBookkeeping, results);
        memEnum->Release();
    }

    sos13->Release();
}
} // namespace

namespace dac
{
std::vector<ClrNativeHeapInfo> EnumerateNativeHeapsFromSos(ISOSDacInterface* sos)
{
    std::vector<ClrNativeHeapInfo> results;
    if (sos == nullptr)
    {
        return results;
    }

    // Each source is wrapped so a single failing/throwing call degrades to "skip this source".
    auto safe = [](const std::function<void()>& work) {
        try
        {
            work();
        }
        catch (...)
        {
        }
    };

    // The DAC target is this same process, so committed bytes for loader/code blocks (which the DAC
    // only exposes as a reserved block size) can be recovered with an exact in-process page probe.
    InProcessMemoryReader reader;

    safe([&] { EnumerateCodeHeaps(sos, results, reader); });
    safe([&] { EnumerateLoaderHeaps(sos, results, reader); });
    safe([&] { EnumerateGcRegions(sos, results); });

    // Parity with the cDAC backend: GC free-regions / handle-table / bookkeeping via
    // ISOSDacInterface13 (.NET 8+). Absent on .NET 5-7 and .NET Framework -> these sources are empty.
    safe([&] { EnumerateGcMemoryRegions(sos, results); });

    return results;
}
} // namespace dac

DacNativeHeapEnumerator::DacNativeHeapEnumerator(IRuntimeInfo* pRuntimeInfo)
{
    _available = _dac.TryLoad(pRuntimeInfo);
}

bool DacNativeHeapEnumerator::IsAvailable() const
{
    return _available;
}

std::vector<ClrNativeHeapInfo> DacNativeHeapEnumerator::EnumerateAll()
{
    if (!_available)
    {
        return {};
    }

    // Invalidate the DAC's cache so the snapshot reflects current target memory (best-effort).
    _dac.Flush();

    return dac::EnumerateNativeHeapsFromSos(_dac.GetSos());
}
