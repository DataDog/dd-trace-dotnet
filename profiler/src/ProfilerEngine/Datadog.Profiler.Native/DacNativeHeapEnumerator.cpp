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

#include <algorithm>
#include <cstring>
#include <functional>
#include <set>
#include <vector>

namespace
{
// heap_segment::flags bit for a read-only (frozen / non-GC) segment - gcinterface.dac.h:
// HEAP_SEGMENT_FLAGS_READONLY. ClrMD classifies such a segment as GCSegmentKind.Frozen.
constexpr size_t HEAP_SEGMENT_FLAGS_READONLY = 1;

// ClrMD's SanitizeSize: a negative or absurdly large block size means "no size info".
constexpr size_t MaxSaneBlockSize = 0x7fffffff;

constexpr int MaxSegmentIterations = 65536;

// Bundles the SOS interfaces + reader + runtime identity threaded through the enumeration.
struct DacContext
{
    ISOSDacInterface* sos = nullptr;
    ISOSDacInterface8* sos8 = nullptr;   // full generation table incl. POH (.NET 5+); null on .NET FX
    ISOSDacInterface13* sos13 = nullptr; // kind-aware traverse + per-LoaderAllocator heaps (.NET 8+)
    IMemoryReader* reader = nullptr;
    int versionMajor = 0;
    bool isCore = true;
};

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

    // SanitizeSize (ClrMD): treat an out-of-range block size as "unknown" (0) rather than trusting a
    // torn/garbage value read from a live target.
    const uint64_t size = blockSize > MaxSaneBlockSize ? 0 : static_cast<uint64_t>(blockSize);

    ClrNativeHeapInfo info;
    info.Address = static_cast<uintptr_t>(blockData);
    info.Size = size; // reserved span of the block
    // The DAC only reports the reserved block size; recover the committed portion by probing pages
    // (the DAC target is this same process, so an in-process reader is exact).
    info.Committed = (sink->reader != nullptr)
                         ? eeheap::ProbeCommittedBytes(*sink->reader, info.Address, info.Size)
                         : info.Size;
    info.Kind = sink->kind;
    // ClrMD marks the current (still-being-allocated-into) block Active, all others Inactive.
    info.State = blockIsCurrentBlock ? NativeHeapState::Active : NativeHeapState::Inactive;
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

// Maps the names returned by ISOSDacInterface13::GetLoaderAllocatorHeapNames to our heap kinds
// (mirrors ClrMD DacNativeHeaps.GetNativeHeaps).
NativeHeapKind MapLoaderHeapName(const char* name)
{
    if (name == nullptr)
    {
        return NativeHeapKind::Unknown;
    }

    struct Entry
    {
        const char* name;
        NativeHeapKind kind;
    };
    static const Entry table[] = {
        {"LowFrequencyHeap", NativeHeapKind::LowFrequencyHeap},
        {"HighFrequencyHeap", NativeHeapKind::HighFrequencyHeap},
        {"StubHeap", NativeHeapKind::StubHeap},
        {"ExecutableHeap", NativeHeapKind::ExecutableHeap},
        {"FixupPrecodeHeap", NativeHeapKind::FixupPrecodeHeap},
        {"NewStubPrecodeHeap", NativeHeapKind::NewStubPrecodeHeap},
        {"IndcellHeap", NativeHeapKind::IndirectionCellHeap},
        {"LookupHeap", NativeHeapKind::LookupHeap},
        {"ResolveHeap", NativeHeapKind::ResolveHeap},
        {"DispatchHeap", NativeHeapKind::DispatchHeap},
        {"CacheEntryHeap", NativeHeapKind::CacheEntryHeap},
        {"VtableHeap", NativeHeapKind::VtableHeap},
    };

    for (const Entry& e : table)
    {
        if (std::strcmp(name, e.name) == 0)
        {
            return e.kind;
        }
    }
    return NativeHeapKind::Unknown;
}

// Free-region sub-kind from SOSMemoryRegion.ExtraData (ClrMD DacNativeHeaps.EnumerateGCFreeRegions).
NativeHeapKind MapFreeRegionExtraData(CLRDATA_ADDRESS extraData)
{
    switch (extraData)
    {
        case 1: return NativeHeapKind::GCFreeGlobalHugeRegion;
        case 2: return NativeHeapKind::GCFreeGlobalRegion;
        case 3: return NativeHeapKind::GCFreeRegion;
        case 4: return NativeHeapKind::GCFreeSohSegment;
        case 5: return NativeHeapKind::GCFreeUohSegment;
        default: return NativeHeapKind::GCFreeRegion;
    }
}

// When only the classic ISOSDacInterface is available the DAC cannot tell a normal LoaderHeap from a
// vtable-less ExplicitControlLoaderHeap (which it treats as a LoaderHeap), so the address must be
// nudged by one pointer depending on runtime version. Mirrors ClrMD DacNativeHeaps.FixupHeapAddress.
// (ClrMD's extra read-validation retry, for unknown single-file versions, is omitted: the profiler
// always knows its own runtime version, and the in-process target makes the guess deterministic.)
CLRDATA_ADDRESS AdjustLoaderHeapAddress(const DacContext& ctx, CLRDATA_ADDRESS loaderHeap, LoaderHeapKind kind)
{
    const CLRDATA_ADDRESS ptr = static_cast<CLRDATA_ADDRESS>(ctx.reader != nullptr ? ctx.reader->PointerSize() : static_cast<int>(sizeof(uintptr_t)));

    bool normalNeedsAdjustment = false;
    bool explicitDoesNotNeedAdjustment = false;
    if (ctx.isCore)
    {
        const int v = ctx.versionMajor;
        normalNeedsAdjustment = (v == 7) || (v == 8 && ctx.sos13 == nullptr) || (v == 0);
        explicitDoesNotNeedAdjustment = (v >= 11) || (v == 0);
    }

    if (normalNeedsAdjustment)
    {
        if (kind == LoaderHeapKindNormal)
        {
            loaderHeap += ptr;
        }
    }
    else
    {
        if (kind == LoaderHeapKindExplicitControl && !explicitDoesNotNeedAdjustment)
        {
            loaderHeap -= ptr;
        }
    }
    return loaderHeap;
}

// Walk one loader heap via the classic ISOSDacInterface::TraverseLoaderHeap with the version-based
// pointer fixup. Used for legacy AppDomain loader heaps and module thunk heaps (ClrMD's
// LegacyEnumerateLoaderAllocatorHeaps), which stay on the classic path even when ISOSDacInterface13
// exists but exposes no loader-allocator heap names.
void EnumerateLoaderHeapClassic(
    const DacContext& ctx,
    CLRDATA_ADDRESS loaderHeap,
    LoaderHeapKind kind,
    NativeHeapKind nativeKind,
    std::vector<ClrNativeHeapInfo>& results,
    std::set<CLRDATA_ADDRESS>& visited)
{
    if (loaderHeap == 0 || !visited.insert(loaderHeap).second)
    {
        return;
    }

    TraverseScope scope(results, nativeKind, *ctx.reader);
    ctx.sos->TraverseLoaderHeap(AdjustLoaderHeapAddress(ctx, loaderHeap, kind), &HeapVisitor);
}

// Walk one loader heap via the ISOSDacInterface13 kind-aware traverse (.NET 8+). The caller must have
// confirmed ctx.sos13 is non-null. Used by the modern per-LoaderAllocator path.
void EnumerateLoaderHeapSos13(
    const DacContext& ctx,
    CLRDATA_ADDRESS loaderHeap,
    LoaderHeapKind kind,
    NativeHeapKind nativeKind,
    std::vector<ClrNativeHeapInfo>& results,
    std::set<CLRDATA_ADDRESS>& visited)
{
    if (loaderHeap == 0 || !visited.insert(loaderHeap).second)
    {
        return;
    }

    TraverseScope scope(results, nativeKind, *ctx.reader);
    ctx.sos13->TraverseLoaderHeap(loaderHeap, kind, &HeapVisitor);
}

// Code heaps are ExplicitControlLoaderHeaps. The classic traverse reports all-zero blocks for them
// on modern runtimes, so prefer the ISOSDacInterface13 kind-aware traverse when available; otherwise
// fall back to the classic traverse with the explicit-control pointer fixup.
void EnumerateCodeHeapBlocks(
    const DacContext& ctx,
    CLRDATA_ADDRESS loaderHeap,
    std::vector<ClrNativeHeapInfo>& results,
    std::set<CLRDATA_ADDRESS>& visited)
{
    if (ctx.sos13 != nullptr)
    {
        EnumerateLoaderHeapSos13(ctx, loaderHeap, LoaderHeapKindExplicitControl, NativeHeapKind::LoaderCodeHeap, results, visited);
    }
    else
    {
        EnumerateLoaderHeapClassic(ctx, loaderHeap, LoaderHeapKindExplicitControl, NativeHeapKind::LoaderCodeHeap, results, visited);
    }
}

void WalkVcsHeap(
    const DacContext& ctx,
    CLRDATA_ADDRESS appDomain,
    VCSHeapType heapType,
    NativeHeapKind kind,
    std::vector<ClrNativeHeapInfo>& results)
{
    TraverseScope scope(results, kind, *ctx.reader);
    ctx.sos->TraverseVirtCallStubHeap(appDomain, heapType, &HeapVisitor);
}

// --- JIT code heaps (JitHeapInfo) ---
void EnumerateCodeHeaps(const DacContext& ctx, std::vector<ClrNativeHeapInfo>& results)
{
    unsigned int needed = 0;
    if (FAILED(ctx.sos->GetJitManagerList(0, nullptr, &needed)) || needed == 0)
    {
        return;
    }

    std::vector<DacpJitManagerInfo> managers(needed);
    if (FAILED(ctx.sos->GetJitManagerList(needed, managers.data(), &needed)))
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
        if (FAILED(ctx.sos->GetCodeHeapList(managers[i].managerAddr, 0, nullptr, &heapCount)) || heapCount == 0)
        {
            continue;
        }

        std::vector<DacpJitCodeHeapInfo> codeHeaps(heapCount);
        if (FAILED(ctx.sos->GetCodeHeapList(managers[i].managerAddr, heapCount, codeHeaps.data(), &heapCount)))
        {
            continue;
        }

        for (unsigned int h = 0; h < heapCount && h < codeHeaps.size(); h++)
        {
            const DacpJitCodeHeapInfo& heap = codeHeaps[h];
            if (heap.codeHeapType == CODEHEAP_LOADER)
            {
                EnumerateCodeHeapBlocks(ctx, heap.LoaderHeap, results, visited);
            }
            else if (heap.codeHeapType == CODEHEAP_HOST)
            {
                if (heap.HostData.currentAddr >= heap.HostData.baseAddr && heap.HostData.baseAddr != 0)
                {
                    ClrNativeHeapInfo info;
                    info.Address = static_cast<uintptr_t>(heap.HostData.baseAddr);
                    info.Size = static_cast<uint64_t>(heap.HostData.currentAddr - heap.HostData.baseAddr);
                    info.Committed = eeheap::ProbeCommittedBytes(*ctx.reader, info.Address, info.Size);
                    info.Kind = NativeHeapKind::HostCodeHeap;
                    info.State = NativeHeapState::Active;
                    results.push_back(info);
                }
            }
        }
    }
}

// --- Modern per-LoaderAllocator heap enumeration (ISOSDacInterface13, .NET 8+) ---
// Surfaces every loader-allocator heap (low/high/stub/executable/precode/VCS/vtable) the way ClrMD
// does, so heaps the legacy AppDomain walk misses (FixupPrecodeHeap, NewStubPrecodeHeap, ...) are
// reported. Names map to kinds positionally with the heaps returned by GetLoaderAllocatorHeaps.
std::vector<NativeHeapKind> GetLoaderAllocatorHeapKinds(ISOSDacInterface13* sos13)
{
    std::vector<NativeHeapKind> kinds;
    if (sos13 == nullptr)
    {
        return kinds;
    }

    int needed = 0;
    if (FAILED(sos13->GetLoaderAllocatorHeapNames(0, nullptr, &needed)) || needed <= 0)
    {
        return kinds;
    }

    std::vector<const char*> names(static_cast<size_t>(needed), nullptr);
    if (FAILED(sos13->GetLoaderAllocatorHeapNames(needed, names.data(), &needed)))
    {
        return kinds;
    }

    kinds.reserve(names.size());
    for (const char* n : names)
    {
        kinds.push_back(MapLoaderHeapName(n));
    }
    return kinds;
}

// Returns true if the modern path was taken for this domain (so the caller skips the legacy walk).
bool EnumerateDomainHeapsModern(
    const DacContext& ctx,
    CLRDATA_ADDRESS domain,
    const std::vector<NativeHeapKind>& names,
    std::vector<ClrNativeHeapInfo>& results,
    std::set<CLRDATA_ADDRESS>& visited)
{
    if (ctx.sos13 == nullptr || names.empty() || domain == 0)
    {
        return false;
    }

    CLRDATA_ADDRESS loaderAllocator = 0;
    if (FAILED(ctx.sos13->GetDomainLoaderAllocator(domain, &loaderAllocator)) || loaderAllocator == 0)
    {
        return false;
    }

    // Dedup shared loader allocators (the system and shared domains share the global one).
    if (!visited.insert(loaderAllocator).second)
    {
        return true;
    }

    int needed = 0;
    if (FAILED(ctx.sos13->GetLoaderAllocatorHeaps(loaderAllocator, 0, nullptr, nullptr, &needed)) || needed <= 0)
    {
        return true;
    }

    std::vector<CLRDATA_ADDRESS> addrs(static_cast<size_t>(needed), 0);
    std::vector<LoaderHeapKind> kinds(static_cast<size_t>(needed), LoaderHeapKindNormal);
    if (FAILED(ctx.sos13->GetLoaderAllocatorHeaps(loaderAllocator, needed, addrs.data(), kinds.data(), &needed)))
    {
        return true;
    }

    const int count = std::min({needed, static_cast<int>(addrs.size()), static_cast<int>(names.size())});
    for (int i = 0; i < count; i++)
    {
        EnumerateLoaderHeapSos13(ctx, addrs[i], kinds[i], names[i], results, visited);
    }
    return true;
}

// Walk a single domain's three loader heaps in SOS order (low -> high -> stub).
void EnumerateDomainLoaderHeaps(
    const DacContext& ctx,
    CLRDATA_ADDRESS domain,
    std::vector<ClrNativeHeapInfo>& results,
    std::set<CLRDATA_ADDRESS>& visited)
{
    if (domain == 0)
    {
        return;
    }

    DacpAppDomainData data;
    if (FAILED(data.Request(ctx.sos, domain)))
    {
        return;
    }

    EnumerateLoaderHeapClassic(ctx, data.pLowFrequencyHeap, LoaderHeapKindNormal, NativeHeapKind::LowFrequencyHeap, results, visited);
    EnumerateLoaderHeapClassic(ctx, data.pHighFrequencyHeap, LoaderHeapKindNormal, NativeHeapKind::HighFrequencyHeap, results, visited);
    EnumerateLoaderHeapClassic(ctx, data.pStubHeap, LoaderHeapKindNormal, NativeHeapKind::StubHeap, results, visited);
}

// VCS (virtual call stub) heaps for an AppDomain (VSDHeapInfo).
void EnumerateVcsHeaps(const DacContext& ctx, CLRDATA_ADDRESS appDomain, std::vector<ClrNativeHeapInfo>& results)
{
    if (appDomain == 0)
    {
        return;
    }

    WalkVcsHeap(ctx, appDomain, IndcellHeap, NativeHeapKind::IndirectionCellHeap, results);
    WalkVcsHeap(ctx, appDomain, LookupHeap, NativeHeapKind::LookupHeap, results);
    WalkVcsHeap(ctx, appDomain, ResolveHeap, NativeHeapKind::ResolveHeap, results);
    WalkVcsHeap(ctx, appDomain, DispatchHeap, NativeHeapKind::DispatchHeap, results);
    WalkVcsHeap(ctx, appDomain, CacheEntryHeap, NativeHeapKind::CacheEntryHeap, results);
}

// Module thunk heaps for every assembly/module in an AppDomain (PrintModuleHeapInfo).
void EnumerateModuleThunkHeaps(
    const DacContext& ctx,
    CLRDATA_ADDRESS appDomain,
    std::vector<ClrNativeHeapInfo>& results,
    std::set<CLRDATA_ADDRESS>& visited)
{
    if (appDomain == 0)
    {
        return;
    }

    int assemblyCount = 0;
    if (FAILED(ctx.sos->GetAssemblyList(appDomain, 0, nullptr, &assemblyCount)) || assemblyCount <= 0)
    {
        return;
    }

    std::vector<CLRDATA_ADDRESS> assemblies(static_cast<size_t>(assemblyCount));
    if (FAILED(ctx.sos->GetAssemblyList(appDomain, assemblyCount, assemblies.data(), &assemblyCount)))
    {
        return;
    }

    for (int a = 0; a < assemblyCount && a < static_cast<int>(assemblies.size()); a++)
    {
        unsigned int moduleCount = 0;
        if (FAILED(ctx.sos->GetAssemblyModuleList(assemblies[a], 0, nullptr, &moduleCount)) || moduleCount == 0)
        {
            continue;
        }

        std::vector<CLRDATA_ADDRESS> modules(moduleCount);
        if (FAILED(ctx.sos->GetAssemblyModuleList(assemblies[a], moduleCount, modules.data(), &moduleCount)))
        {
            continue;
        }

        for (unsigned int m = 0; m < moduleCount && m < modules.size(); m++)
        {
            DacpModuleData moduleData;
            if (FAILED(moduleData.Request(ctx.sos, modules[m])))
            {
                continue;
            }

            // Module thunk heaps are normal LoaderHeaps and are not part of the loader-allocator heap
            // list, so they are walked here in both the modern and legacy paths.
            EnumerateLoaderHeapClassic(ctx, moduleData.pThunkHeap, LoaderHeapKindNormal, NativeHeapKind::ThunkHeap, results, visited);
        }
    }
}

// --- Loader heaps + VCS + module thunk heaps (PrintDomainHeapInfo / VSDHeapInfo / PrintModuleHeapInfo) ---
void EnumerateLoaderHeaps(const DacContext& ctx, std::vector<ClrNativeHeapInfo>& results)
{
    DacpAppDomainStoreData stores;
    if (FAILED(stores.Request(ctx.sos)))
    {
        return;
    }

    std::set<CLRDATA_ADDRESS> visited;

    // .NET 8+: prefer ISOSDacInterface13's per-LoaderAllocator enumeration, which reports every heap
    // kind (incl. FixupPrecodeHeap / NewStubPrecodeHeap / VtableHeap). The names are version-stable
    // and shared across loader allocators, so fetch them once.
    const std::vector<NativeHeapKind> names = GetLoaderAllocatorHeapKinds(ctx.sos13);

    // For a domain: try the modern path first; otherwise fall back to the classic low/high/stub walk.
    // VCS stub heaps are part of the loader-allocator heap list in the modern path, but a separate
    // source in the legacy path.
    auto walkDomain = [&](CLRDATA_ADDRESS domain, bool includeVcs) {
        if (domain == 0)
        {
            return;
        }
        if (EnumerateDomainHeapsModern(ctx, domain, names, results, visited))
        {
            return;
        }
        EnumerateDomainLoaderHeaps(ctx, domain, results, visited);
        if (includeVcs)
        {
            EnumerateVcsHeaps(ctx, domain, results);
        }
    };

    // System + shared domains first (they hold runtime-wide loader heaps).
    walkDomain(stores.systemDomain, false);
    walkDomain(stores.sharedDomain, false);

    std::vector<CLRDATA_ADDRESS> domains;
    if (stores.DomainCount > 0)
    {
        unsigned int needed = 0;
        if (SUCCEEDED(ctx.sos->GetAppDomainList(static_cast<unsigned int>(stores.DomainCount), nullptr, &needed)) && needed > 0)
        {
            domains.resize(needed);
            if (FAILED(ctx.sos->GetAppDomainList(needed, domains.data(), &needed)))
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
        walkDomain(domain, true);
        EnumerateModuleThunkHeaps(ctx, domain, results, visited);
    }
}

void AddGcSegments(
    const DacContext& ctx,
    const DacpGcHeapDetails& details,
    CLRDATA_ADDRESS heapAddr,
    int heapIndex,
    bool server,
    std::vector<ClrNativeHeapInfo>& results)
{
    // Prefer ISOSDacInterface8's full generation table (gen0/1/2 + LOH + POH); the legacy
    // DacpGcHeapDetails.generation_table is frozen at DAC_NUMBERGENERATIONS (4) and omits the Pinned
    // Object Heap (generation 4). Falls back to the capped table on .NET Framework (no sos8).
    std::vector<DacpGenerationData> genTable;
    unsigned int genCount = 0;
    if (ctx.sos8 != nullptr)
    {
        unsigned int n = 0;
        if (SUCCEEDED(ctx.sos8->GetNumberGenerations(&n)) && n > 0 && n < 64)
        {
            genTable.resize(n);
            unsigned int got = 0;
            const HRESULT hr = server
                                   ? ctx.sos8->GetGenerationTableSvr(heapAddr, n, genTable.data(), &got)
                                   : ctx.sos8->GetGenerationTable(n, genTable.data(), &got);
            if (SUCCEEDED(hr) && got > 0)
            {
                genCount = std::min(got, n);
            }
        }
    }

    const DacpGenerationData* gens = genTable.data();
    if (genCount == 0)
    {
        gens = details.generation_table;
        genCount = DAC_NUMBERGENERATIONS;
    }

    std::set<CLRDATA_ADDRESS> visitedSegments;

    for (unsigned int gen = 0; gen < genCount; gen++)
    {
        CLRDATA_ADDRESS seg = gens[gen].start_segment;
        int iterations = 0;
        while (seg != 0 && iterations++ < MaxSegmentIterations)
        {
            if (!visitedSegments.insert(seg).second)
            {
                // Already walked (segment-mode runtimes share segments across generations).
                break;
            }

            DacpHeapSegmentData segData;
            if (FAILED(segData.Request(ctx.sos, seg, details)))
            {
                break;
            }

            const uint64_t mem = static_cast<uint64_t>(segData.mem);
            const uint64_t committed = static_cast<uint64_t>(segData.committed);
            const uint64_t reserved = static_cast<uint64_t>(segData.reserved);
            const bool readOnly = (segData.flags & HEAP_SEGMENT_FLAGS_READONLY) != 0;

            // One entry per segment carrying both the reserved (virtual) and committed spans. The DAC
            // reports these directly, so no page probe is needed here. A read-only segment is the
            // frozen / non-GC heap (ClrMD GCSegmentKind.Frozen); everything else is a GC segment
            // tagged with the generation whose list first referenced it.
            if (reserved > mem)
            {
                ClrNativeHeapInfo info;
                info.Address = static_cast<uintptr_t>(mem);
                info.Size = reserved - mem;
                info.Committed = committed > mem ? committed - mem : 0;
                if (readOnly)
                {
                    info.Kind = NativeHeapKind::NonGCHeap;
                    info.Generation = -1;
                }
                else
                {
                    info.Kind = NativeHeapKind::GCHeapSegment;
                    info.Generation = static_cast<int>(gen);
                }
                info.State = NativeHeapState::Active;
                info.GCHeap = heapIndex;
                results.push_back(info);
            }

            seg = segData.next;
        }
    }
}

// --- GC heap segments (GCHeapInfo) ---
void EnumerateGcRegions(const DacContext& ctx, std::vector<ClrNativeHeapInfo>& results)
{
    DacpGcHeapData gcData;
    if (FAILED(gcData.Request(ctx.sos)))
    {
        return;
    }

    if (gcData.bServerMode)
    {
        unsigned int needed = 0;
        if (FAILED(ctx.sos->GetGCHeapList(0, nullptr, &needed)) || needed == 0)
        {
            return;
        }

        std::vector<CLRDATA_ADDRESS> heaps(needed);
        if (FAILED(ctx.sos->GetGCHeapList(needed, heaps.data(), &needed)))
        {
            return;
        }

        for (unsigned int i = 0; i < needed && i < heaps.size(); i++)
        {
            DacpGcHeapDetails details;
            if (FAILED(details.Request(ctx.sos, heaps[i])))
            {
                continue;
            }
            AddGcSegments(ctx, details, heaps[i], static_cast<int>(i), /*server*/ true, results);
        }
    }
    else
    {
        DacpGcHeapDetails details;
        if (FAILED(details.Request(ctx.sos)))
        {
            return;
        }
        AddGcSegments(ctx, details, /*heapAddr*/ 0, 0, /*server*/ false, results);
    }
}

// Drains an ISOSMemoryEnum, emitting one entry per SOSMemoryRegion. The enum reports a region size;
// for most kinds the reserved span is not exposed separately, so Size == Committed. Free regions are
// sub-classified by ExtraData; the heap index is attached only when meaningful (not for bookkeeping,
// which is a region-of-regions). When queryCommitted is set, the region's Size is treated as the
// *reserved* span and the committed bytes are derived from the OS region map (gap-aware) - used for
// the GC bookkeeping/card-table block, whose committed runs are scattered with reserved gaps.
void DrainMemoryEnum(
    ISOSMemoryEnum* memEnum,
    NativeHeapKind fixedKind,
    bool useExtraDataSubkind,
    NativeHeapState state,
    bool attachHeap,
    std::vector<ClrNativeHeapInfo>& results,
    bool queryCommitted = false)
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
            if (queryCommitted)
            {
                uint64_t committed = eeheap::QueryCommittedBytes(info.Address, info.Size);
                info.Committed = committed != 0 ? committed : info.Size;
            }
            else
            {
                info.Committed = info.Size;
            }
            info.Kind = useExtraDataSubkind ? MapFreeRegionExtraData(regions[i].ExtraData) : fixedKind;
            info.State = state;
            if (attachHeap)
            {
                info.GCHeap = regions[i].Heap;
            }
            results.push_back(info);
        }
    } while (fetched == BatchSize && guard-- > 0);
}

// --- GC free-regions / handle-table / bookkeeping (ISOSDacInterface13, .NET 8+) ---
// Parity with the cDAC backend. If ISOSDacInterface13 is absent (.NET 5-7, .NET Framework) these
// sources are simply empty.
void EnumerateGcMemoryRegions(const DacContext& ctx, std::vector<ClrNativeHeapInfo>& results)
{
    if (ctx.sos13 == nullptr)
    {
        return;
    }

    ISOSMemoryEnum* memEnum = nullptr;
    if (SUCCEEDED(ctx.sos13->GetGCFreeRegions(&memEnum)) && memEnum != nullptr)
    {
        // Free regions are sub-classified by ExtraData and are inactive (not allocated into).
        DrainMemoryEnum(memEnum, NativeHeapKind::GCFreeRegion, /*useExtraDataSubkind*/ true, NativeHeapState::Inactive, /*attachHeap*/ true, results);
        memEnum->Release();
    }

    memEnum = nullptr;
    if (SUCCEEDED(ctx.sos13->GetHandleTableMemoryRegions(&memEnum)) && memEnum != nullptr)
    {
        DrainMemoryEnum(memEnum, NativeHeapKind::HandleTable, /*useExtraDataSubkind*/ false, NativeHeapState::Active, /*attachHeap*/ true, results);
        memEnum->Release();
    }

    memEnum = nullptr;
    if (SUCCEEDED(ctx.sos13->GetGCBookkeepingMemoryRegions(&memEnum)) && memEnum != nullptr)
    {
        // Bookkeeping is a region-of-regions covering all heaps, so no single heap index applies. The
        // enum reports the reserved card-table size; commit is scattered, so derive it from the OS map.
        DrainMemoryEnum(memEnum, NativeHeapKind::GCBookkeeping, /*useExtraDataSubkind*/ false, NativeHeapState::RegionOfRegions, /*attachHeap*/ false, results, /*queryCommitted*/ true);
        memEnum->Release();
    }
}
} // namespace

namespace dac
{
std::vector<ClrNativeHeapInfo> EnumerateNativeHeapsFromSos(ISOSDacInterface* sos, int versionMajor, bool isCore)
{
    std::vector<ClrNativeHeapInfo> results;
    if (sos == nullptr)
    {
        return results;
    }

    // The DAC target is this same process, so committed bytes for loader/code blocks (which the DAC
    // only exposes as a reserved block size) can be recovered with an exact in-process page probe.
    InProcessMemoryReader reader;

    DacContext ctx;
    ctx.sos = sos;
    ctx.reader = &reader;
    ctx.versionMajor = versionMajor;
    ctx.isCore = isCore;

    // ISOSDacInterface8 (.NET 5+): full generation table incl. POH. ISOSDacInterface13 (.NET 8+):
    // kind-aware loader-heap traverse, per-LoaderAllocator heaps, and the GC memory-region enums.
    if (FAILED(sos->QueryInterface(__uuidof(ISOSDacInterface8), reinterpret_cast<void**>(&ctx.sos8))))
    {
        ctx.sos8 = nullptr;
    }
    if (FAILED(sos->QueryInterface(__uuidof(ISOSDacInterface13), reinterpret_cast<void**>(&ctx.sos13))))
    {
        ctx.sos13 = nullptr;
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

    safe([&] { EnumerateCodeHeaps(ctx, results); });
    safe([&] { EnumerateLoaderHeaps(ctx, results); });
    safe([&] { EnumerateGcRegions(ctx, results); });
    safe([&] { EnumerateGcMemoryRegions(ctx, results); });

    if (ctx.sos8 != nullptr)
    {
        ctx.sos8->Release();
    }
    if (ctx.sos13 != nullptr)
    {
        ctx.sos13->Release();
    }

    return results;
}
} // namespace dac

DacNativeHeapEnumerator::DacNativeHeapEnumerator(IRuntimeInfo* pRuntimeInfo)
{
    if (pRuntimeInfo != nullptr)
    {
        _versionMajor = static_cast<int>(pRuntimeInfo->GetMajorVersion());
        _isCore = !pRuntimeInfo->IsDotnetFramework();
    }
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

    return dac::EnumerateNativeHeapsFromSos(_dac.GetSos(), _versionMajor, _isCore);
}
