// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "ClrNativeHeapInfo.h"
#include "DacNativeHeapEnumerator.h"

#ifdef _WINDOWS
// Needed so CONTEXT is defined before the DAC headers reference T_CONTEXT (see the alias below).
#include <windows.h>
#endif

// Mirror DacNativeHeapEnumerator.cpp: define TARGET_UNIX/HOST_UNIX before the DAC headers on
// non-Windows so the Dacp* struct layouts match and dacprivate.h does not pull in <msodw.h>.
#if !defined(_WINDOWS)
#ifndef TARGET_UNIX
#define TARGET_UNIX
#endif
#ifndef HOST_UNIX
#define HOST_UNIX
#endif
#endif

// sospriv.h refers to T_CONTEXT, but its in-file definition is disabled (#if 0); coreclr normally
// supplies it via crosscomp.h (#define T_CONTEXT CONTEXT). Alias it to the platform CONTEXT so the
// vtable layout matches (we never call the slot that uses it). Mirrors DacNativeHeapEnumerator.cpp.
#ifndef T_CONTEXT
#define T_CONTEXT CONTEXT
#endif

#include "dacprivate.h"

// Vendored ISOSDacInterface13 / ISOSMemoryEnum / SOSMemoryRegion (mirrors DacNativeHeapEnumerator.cpp).
#include "DacInterface13.h"

#include <algorithm>
#include <vector>

namespace
{
// Canned addresses used by the fake DAC.
constexpr CLRDATA_ADDRESS JitManagerAddr = 0x900;
constexpr CLRDATA_ADDRESS LoaderCodeHeapAddr = 0x1000;
constexpr CLRDATA_ADDRESS HostCodeHeapBase = 0x2000;
constexpr CLRDATA_ADDRESS HostCodeHeapCurrent = 0x2800; // size 0x800
constexpr CLRDATA_ADDRESS SystemDomain = 0x10;
constexpr CLRDATA_ADDRESS AppDomain = 0x30;
constexpr CLRDATA_ADDRESS AssemblyAddr = 0x40;
constexpr CLRDATA_ADDRESS ModuleAddr = 0x50;
constexpr CLRDATA_ADDRESS GcSegment = 0x5000;

// A heap-allocated ISOSMemoryEnum that hands back a canned set of SOSMemoryRegion entries. Created
// with new; the enumerator owns it and calls Release() exactly once (which deletes it).
class FakeMemoryEnum : public ISOSMemoryEnum
{
public:
    explicit FakeMemoryEnum(std::vector<SOSMemoryRegion> regions) :
        _regions(std::move(regions))
    {
    }

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID, void**) override { return E_NOINTERFACE; }
    ULONG STDMETHODCALLTYPE AddRef() override { return 1; }
    ULONG STDMETHODCALLTYPE Release() override
    {
        delete this;
        return 0;
    }

    HRESULT STDMETHODCALLTYPE Skip(unsigned int) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE Reset() override
    {
        _pos = 0;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetCount(unsigned int* pCount) override
    {
        if (pCount != nullptr)
        {
            *pCount = static_cast<unsigned int>(_regions.size());
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE Next(unsigned int count, SOSMemoryRegion regions[], unsigned int* pNeeded) override
    {
        unsigned int n = 0;
        while (_pos < _regions.size() && n < count)
        {
            regions[n++] = _regions[_pos++];
        }
        if (pNeeded != nullptr)
        {
            *pNeeded = n;
        }
        return S_OK;
    }

private:
    std::vector<SOSMemoryRegion> _regions;
    size_t _pos = 0;
};

// A fake ISOSDacInterface13 returning one region from each GC memory-region enumerator.
class FakeSosDac13 : public ISOSDacInterface13
{
public:
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID, void**) override { return E_NOINTERFACE; }
    ULONG STDMETHODCALLTYPE AddRef() override { return 1; }
    ULONG STDMETHODCALLTYPE Release() override { return 1; }

    HRESULT STDMETHODCALLTYPE TraverseLoaderHeap(CLRDATA_ADDRESS, LoaderHeapKind, VISITHEAP) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetDomainLoaderAllocator(CLRDATA_ADDRESS, CLRDATA_ADDRESS*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetLoaderAllocatorHeapNames(int, const char**, int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetLoaderAllocatorHeaps(CLRDATA_ADDRESS, int, CLRDATA_ADDRESS*, LoaderHeapKind*, int*) override { return E_NOTIMPL; }

    HRESULT STDMETHODCALLTYPE GetHandleTableMemoryRegions(ISOSMemoryEnum** ppEnum) override
    {
        *ppEnum = new FakeMemoryEnum({SOSMemoryRegion{0x9000, 0x1000, 0, 0}});
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetGCBookkeepingMemoryRegions(ISOSMemoryEnum** ppEnum) override
    {
        *ppEnum = new FakeMemoryEnum({SOSMemoryRegion{0xA000, 0x2000, 0, 0}});
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetGCFreeRegions(ISOSMemoryEnum** ppEnum) override
    {
        *ppEnum = new FakeMemoryEnum({SOSMemoryRegion{0x8000, 0x4000, 0, 1}});
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE LockedFlush() override { return S_OK; }
};

// A fake ISOSDacInterface returning canned heap data, so the DAC enumeration can be exercised
// without loading a real DAC. Only the methods used by EnumerateNativeHeapsFromSos return data;
// everything else returns E_NOTIMPL.
class FakeSosDacInterface : public ISOSDacInterface
{
public:
    bool FailCodeHeaps = false;
    bool FailGc = false;
    bool DedupMode = false; // system + shared domains share the same heaps; no app domains.
    bool SupportInterface13 = false; // when set, QI(ISOSDacInterface13) succeeds.
    FakeSosDac13 _dac13;

    // --- IUnknown ---
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override
    {
        if (SupportInterface13 && ppvObject != nullptr && IsEqualGUID(riid, __uuidof(ISOSDacInterface13)))
        {
            *ppvObject = static_cast<ISOSDacInterface13*>(&_dac13);
            return S_OK;
        }
        return E_NOINTERFACE;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return 1; }
    ULONG STDMETHODCALLTYPE Release() override { return 1; }

    // --- JIT code heaps ---
    HRESULT STDMETHODCALLTYPE GetJitManagerList(unsigned int count, struct DacpJitManagerInfo* managers, unsigned int* pNeeded) override
    {
        if (FailCodeHeaps)
        {
            return E_FAIL;
        }
        if (pNeeded != nullptr)
        {
            *pNeeded = 1;
        }
        if (managers != nullptr && count >= 1)
        {
            managers[0].managerAddr = JitManagerAddr;
            managers[0].codeType = 0; // miIL -> IsMiIL() true
            managers[0].ptrHeapList = 0;
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetCodeHeapList(CLRDATA_ADDRESS jitManager, unsigned int count, struct DacpJitCodeHeapInfo* codeHeaps, unsigned int* pNeeded) override
    {
        if (jitManager != JitManagerAddr)
        {
            return E_FAIL;
        }
        if (pNeeded != nullptr)
        {
            *pNeeded = 2;
        }
        if (codeHeaps != nullptr && count >= 2)
        {
            codeHeaps[0].codeHeapType = CODEHEAP_LOADER;
            codeHeaps[0].LoaderHeap = LoaderCodeHeapAddr;

            codeHeaps[1].codeHeapType = CODEHEAP_HOST;
            codeHeaps[1].HostData.baseAddr = HostCodeHeapBase;
            codeHeaps[1].HostData.currentAddr = HostCodeHeapCurrent;
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE TraverseLoaderHeap(CLRDATA_ADDRESS loaderHeapAddr, VISITHEAP pCallback) override
    {
        if (pCallback != nullptr && loaderHeapAddr != 0)
        {
            pCallback(loaderHeapAddr, 0x1000, TRUE);
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE TraverseVirtCallStubHeap(CLRDATA_ADDRESS pAppDomain, VCSHeapType heaptype, VISITHEAP pCallback) override
    {
        if (pCallback != nullptr && pAppDomain != 0)
        {
            pCallback(0x3000 + static_cast<CLRDATA_ADDRESS>(heaptype), 0x80, TRUE);
        }
        return S_OK;
    }

    // --- loader heaps ---
    HRESULT STDMETHODCALLTYPE GetAppDomainStoreData(struct DacpAppDomainStoreData* data) override
    {
        if (data == nullptr)
        {
            return E_POINTER;
        }
        data->systemDomain = SystemDomain;
        data->sharedDomain = DedupMode ? SystemDomain : 0;
        data->DomainCount = DedupMode ? 0 : 1;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetAppDomainList(unsigned int count, CLRDATA_ADDRESS values[], unsigned int* pNeeded) override
    {
        if (pNeeded != nullptr)
        {
            *pNeeded = 1;
        }
        if (values != nullptr && count >= 1)
        {
            values[0] = AppDomain;
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetAppDomainData(CLRDATA_ADDRESS addr, struct DacpAppDomainData* data) override
    {
        if (data == nullptr)
        {
            return E_POINTER;
        }
        if (addr == SystemDomain)
        {
            data->pLowFrequencyHeap = 0x110;
            data->pHighFrequencyHeap = 0x120;
            data->pStubHeap = 0x130;
            return S_OK;
        }
        if (addr == AppDomain)
        {
            data->pLowFrequencyHeap = 0x140;
            data->pHighFrequencyHeap = 0x150;
            data->pStubHeap = 0x160;
            return S_OK;
        }
        return E_FAIL;
    }

    HRESULT STDMETHODCALLTYPE GetAssemblyList(CLRDATA_ADDRESS appDomain, int count, CLRDATA_ADDRESS values[], int* pNeeded) override
    {
        if (appDomain != AppDomain)
        {
            return E_FAIL;
        }
        if (pNeeded != nullptr)
        {
            *pNeeded = 1;
        }
        if (values != nullptr && count >= 1)
        {
            values[0] = AssemblyAddr;
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetAssemblyModuleList(CLRDATA_ADDRESS assembly, unsigned int count, CLRDATA_ADDRESS modules[], unsigned int* pNeeded) override
    {
        if (assembly != AssemblyAddr)
        {
            return E_FAIL;
        }
        if (pNeeded != nullptr)
        {
            *pNeeded = 1;
        }
        if (modules != nullptr && count >= 1)
        {
            modules[0] = ModuleAddr;
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetModuleData(CLRDATA_ADDRESS moduleAddr, struct DacpModuleData* data) override
    {
        if (data == nullptr || moduleAddr != ModuleAddr)
        {
            return E_FAIL;
        }
        data->pThunkHeap = 0x170;
        return S_OK;
    }

    // --- GC ---
    HRESULT STDMETHODCALLTYPE GetGCHeapData(struct DacpGcHeapData* data) override
    {
        if (FailGc)
        {
            return E_FAIL;
        }
        if (data == nullptr)
        {
            return E_POINTER;
        }
        data->bServerMode = FALSE;
        data->bGcStructuresValid = TRUE;
        data->HeapCount = 1;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetGCHeapStaticData(struct DacpGcHeapDetails* data) override
    {
        if (data == nullptr)
        {
            return E_POINTER;
        }
        data->generation_table[0].start_segment = GcSegment;
        data->ephemeral_heap_segment = GcSegment;
        data->alloc_allocated = 0x5400;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetHeapSegmentData(CLRDATA_ADDRESS seg, struct DacpHeapSegmentData* data) override
    {
        if (data == nullptr || seg != GcSegment)
        {
            return E_FAIL;
        }
        data->segmentAddr = GcSegment;
        data->mem = 0x5000;
        data->allocated = 0x5400;
        data->committed = 0x5400;
        data->reserved = 0x6000;
        data->next = 0;
        return S_OK;
    }

    // ----------------------------------------------------------------------------------------
    // Everything below is unused by the enumeration and simply returns E_NOTIMPL.
    // ----------------------------------------------------------------------------------------
    HRESULT STDMETHODCALLTYPE GetThreadStoreData(struct DacpThreadStoreData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetAppDomainName(CLRDATA_ADDRESS, unsigned int, WCHAR*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetDomainFromContext(CLRDATA_ADDRESS, CLRDATA_ADDRESS*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetAssemblyData(CLRDATA_ADDRESS, CLRDATA_ADDRESS, struct DacpAssemblyData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetAssemblyName(CLRDATA_ADDRESS, unsigned int, WCHAR*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetModule(CLRDATA_ADDRESS, IXCLRDataModule**) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE TraverseModuleMap(ModuleMapType, CLRDATA_ADDRESS, MODULEMAPTRAVERSE, LPVOID) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetILForModule(CLRDATA_ADDRESS, DWORD, CLRDATA_ADDRESS*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetThreadData(CLRDATA_ADDRESS, struct DacpThreadData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetThreadFromThinlockID(UINT, CLRDATA_ADDRESS*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetStackLimits(CLRDATA_ADDRESS, CLRDATA_ADDRESS*, CLRDATA_ADDRESS*, CLRDATA_ADDRESS*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetMethodDescData(CLRDATA_ADDRESS, CLRDATA_ADDRESS, struct DacpMethodDescData*, ULONG, struct DacpReJitData*, ULONG*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetMethodDescPtrFromIP(CLRDATA_ADDRESS, CLRDATA_ADDRESS*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetMethodDescName(CLRDATA_ADDRESS, unsigned int, WCHAR*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetMethodDescPtrFromFrame(CLRDATA_ADDRESS, CLRDATA_ADDRESS*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetMethodDescFromToken(CLRDATA_ADDRESS, mdToken, CLRDATA_ADDRESS*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetMethodDescTransparencyData(CLRDATA_ADDRESS, struct DacpMethodDescTransparencyData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetCodeHeaderData(CLRDATA_ADDRESS, struct DacpCodeHeaderData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetJitHelperFunctionName(CLRDATA_ADDRESS, unsigned int, char*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetJumpThunkTarget(T_CONTEXT*, CLRDATA_ADDRESS*, CLRDATA_ADDRESS*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetThreadpoolData(struct DacpThreadpoolData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetWorkRequestData(CLRDATA_ADDRESS, struct DacpWorkRequestData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetHillClimbingLogEntry(CLRDATA_ADDRESS, struct DacpHillClimbingLogEntry*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetObjectData(CLRDATA_ADDRESS, struct DacpObjectData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetObjectStringData(CLRDATA_ADDRESS, unsigned int, WCHAR*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetObjectClassName(CLRDATA_ADDRESS, unsigned int, WCHAR*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetMethodTableName(CLRDATA_ADDRESS, unsigned int, WCHAR*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetMethodTableData(CLRDATA_ADDRESS, struct DacpMethodTableData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetMethodTableSlot(CLRDATA_ADDRESS, unsigned int, CLRDATA_ADDRESS*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetMethodTableFieldData(CLRDATA_ADDRESS, struct DacpMethodTableFieldData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetMethodTableTransparencyData(CLRDATA_ADDRESS, struct DacpMethodTableTransparencyData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetMethodTableForEEClass(CLRDATA_ADDRESS, CLRDATA_ADDRESS*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetFieldDescData(CLRDATA_ADDRESS, struct DacpFieldDescData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetFrameName(CLRDATA_ADDRESS, unsigned int, WCHAR*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetPEFileBase(CLRDATA_ADDRESS, CLRDATA_ADDRESS*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetPEFileName(CLRDATA_ADDRESS, unsigned int, WCHAR*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetGCHeapList(unsigned int, CLRDATA_ADDRESS[], unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetGCHeapDetails(CLRDATA_ADDRESS, struct DacpGcHeapDetails*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetOOMData(CLRDATA_ADDRESS, struct DacpOomData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetOOMStaticData(struct DacpOomData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetHeapAnalyzeData(CLRDATA_ADDRESS, struct DacpGcHeapAnalyzeData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetHeapAnalyzeStaticData(struct DacpGcHeapAnalyzeData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetDomainLocalModuleData(CLRDATA_ADDRESS, struct DacpDomainLocalModuleData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetDomainLocalModuleDataFromAppDomain(CLRDATA_ADDRESS, int, struct DacpDomainLocalModuleData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetDomainLocalModuleDataFromModule(CLRDATA_ADDRESS, struct DacpDomainLocalModuleData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetThreadLocalModuleData(CLRDATA_ADDRESS, unsigned int, struct DacpThreadLocalModuleData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetSyncBlockData(unsigned int, struct DacpSyncBlockData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetSyncBlockCleanupData(CLRDATA_ADDRESS, struct DacpSyncBlockCleanupData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetHandleEnum(ISOSHandleEnum**) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetHandleEnumForTypes(unsigned int[], unsigned int, ISOSHandleEnum**) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetHandleEnumForGC(unsigned int, ISOSHandleEnum**) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE TraverseEHInfo(CLRDATA_ADDRESS, DUMPEHINFO, LPVOID) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetNestedExceptionData(CLRDATA_ADDRESS, CLRDATA_ADDRESS*, CLRDATA_ADDRESS*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetStressLogAddress(CLRDATA_ADDRESS*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetUsefulGlobals(struct DacpUsefulGlobalsData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetClrWatsonBuckets(CLRDATA_ADDRESS, void*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetTLSIndex(ULONG*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetDacModuleHandle(HMODULE*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetRCWData(CLRDATA_ADDRESS, struct DacpRCWData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetRCWInterfaces(CLRDATA_ADDRESS, unsigned int, struct DacpCOMInterfacePointerData*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetCCWData(CLRDATA_ADDRESS, struct DacpCCWData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetCCWInterfaces(CLRDATA_ADDRESS, unsigned int, struct DacpCOMInterfacePointerData*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE TraverseRCWCleanupList(CLRDATA_ADDRESS, VISITRCWFORCLEANUP, LPVOID) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetStackReferences(DWORD, ISOSStackRefEnum**) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetRegisterName(int, unsigned int, WCHAR*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetThreadAllocData(CLRDATA_ADDRESS, struct DacpAllocData*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetHeapAllocData(unsigned int, struct DacpGenerationAllocData*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetFailedAssemblyList(CLRDATA_ADDRESS, int, CLRDATA_ADDRESS[], unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetPrivateBinPaths(CLRDATA_ADDRESS, int, WCHAR*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetAssemblyLocation(CLRDATA_ADDRESS, int, WCHAR*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetAppDomainConfigFile(CLRDATA_ADDRESS, int, WCHAR*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetApplicationBase(CLRDATA_ADDRESS, int, WCHAR*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetFailedAssemblyData(CLRDATA_ADDRESS, unsigned int*, HRESULT*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetFailedAssemblyLocation(CLRDATA_ADDRESS, unsigned int, WCHAR*, unsigned int*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetFailedAssemblyDisplayName(CLRDATA_ADDRESS, unsigned int, WCHAR*, unsigned int*) override { return E_NOTIMPL; }
};

int CountKind(const std::vector<ClrNativeHeapInfo>& heaps, NativeHeapKind kind)
{
    return static_cast<int>(std::count_if(heaps.begin(), heaps.end(),
                                          [kind](const ClrNativeHeapInfo& h) { return h.Kind == kind; }));
}

bool HasHeap(const std::vector<ClrNativeHeapInfo>& heaps, NativeHeapKind kind, uintptr_t address, uint64_t size)
{
    return std::any_of(heaps.begin(), heaps.end(), [&](const ClrNativeHeapInfo& h) {
        return h.Kind == kind && h.Address == address && h.Size == size;
    });
}
} // namespace

TEST(DacNativeHeapEnumeratorTest, EnumeratesAllSourcesInOrder)
{
    FakeSosDacInterface sos;
    std::vector<ClrNativeHeapInfo> heaps = dac::EnumerateNativeHeapsFromSos(&sos);

    ASSERT_FALSE(heaps.empty());

    // Code heaps come first; the loader code heap precedes the host code heap.
    EXPECT_EQ(heaps[0].Kind, NativeHeapKind::LoaderCodeHeap);
    EXPECT_EQ(heaps[0].Address, static_cast<uintptr_t>(LoaderCodeHeapAddr));
    EXPECT_TRUE(HasHeap(heaps, NativeHeapKind::HostCodeHeap, static_cast<uintptr_t>(HostCodeHeapBase), 0x800));

    // Loader heaps (system + app domain), in low -> high -> stub order, plus the module thunk heap.
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::LowFrequencyHeap), 2);
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::HighFrequencyHeap), 2);
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::StubHeap), 2);
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::ThunkHeap), 1);

    // The five VCS stub heaps, routed through the captureless VISITHEAP trampoline.
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::IndirectionCellHeap), 1);
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::LookupHeap), 1);
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::ResolveHeap), 1);
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::DispatchHeap), 1);
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::CacheEntryHeap), 1);

    // GC segment: ONE entry per segment carrying the reserved span (reserved - mem) as Size and the
    // committed span (committed - mem) as Committed, tagged with the heap index and generation.
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::GCHeapSegment), 1);
    for (const auto& h : heaps)
    {
        if (h.Kind == NativeHeapKind::GCHeapSegment)
        {
            EXPECT_EQ(h.Address, static_cast<uintptr_t>(0x5000));
            EXPECT_EQ(h.Size, 0x1000u);     // reserved (0x6000) - mem (0x5000)
            EXPECT_EQ(h.Committed, 0x400u); // committed (0x5400) - mem (0x5000)
            EXPECT_EQ(h.GCHeap, 0);
            EXPECT_EQ(h.Generation, 0);
        }
    }
}

TEST(DacNativeHeapEnumeratorTest, FailingSourceIsSkipped)
{
    FakeSosDacInterface sos;
    sos.FailGc = true;

    std::vector<ClrNativeHeapInfo> heaps = dac::EnumerateNativeHeapsFromSos(&sos);

    // GC failed, so no GC segments, but code and loader heaps are still enumerated.
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::GCHeapSegment), 0);
    EXPECT_GE(CountKind(heaps, NativeHeapKind::LoaderCodeHeap), 1);
    EXPECT_GE(CountKind(heaps, NativeHeapKind::LowFrequencyHeap), 1);
}

TEST(DacNativeHeapEnumeratorTest, NullInterfaceYieldsEmpty)
{
    EXPECT_TRUE(dac::EnumerateNativeHeapsFromSos(nullptr).empty());
}

TEST(DacNativeHeapEnumeratorTest, GcMemoryRegionsAbsentWithoutInterface13)
{
    FakeSosDacInterface sos; // SupportInterface13 == false

    std::vector<ClrNativeHeapInfo> heaps = dac::EnumerateNativeHeapsFromSos(&sos);

    EXPECT_EQ(CountKind(heaps, NativeHeapKind::GCFreeRegion), 0);
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::HandleTable), 0);
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::GCBookkeeping), 0);
}

TEST(DacNativeHeapEnumeratorTest, EnumeratesGcMemoryRegionsViaInterface13)
{
    FakeSosDacInterface sos;
    sos.SupportInterface13 = true; // .NET 8+: ISOSDacInterface13 is available.

    std::vector<ClrNativeHeapInfo> heaps = dac::EnumerateNativeHeapsFromSos(&sos);

    EXPECT_EQ(CountKind(heaps, NativeHeapKind::GCFreeRegion), 1);
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::HandleTable), 1);
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::GCBookkeeping), 1);

    EXPECT_TRUE(HasHeap(heaps, NativeHeapKind::GCFreeRegion, 0x8000, 0x4000));
    EXPECT_TRUE(HasHeap(heaps, NativeHeapKind::HandleTable, 0x9000, 0x1000));
    EXPECT_TRUE(HasHeap(heaps, NativeHeapKind::GCBookkeeping, 0xA000, 0x2000));

    // The memory enum reports committed bytes; Size == Committed for these regions, and the free
    // region carries its heap index.
    for (const auto& h : heaps)
    {
        if (h.Kind == NativeHeapKind::GCFreeRegion)
        {
            EXPECT_EQ(h.Committed, h.Size);
            EXPECT_EQ(h.GCHeap, 1);
        }
    }
}

TEST(DacNativeHeapEnumeratorTest, SharedLoaderHeapsAreDeduped)
{
    FakeSosDacInterface sos;
    sos.DedupMode = true; // system + shared domains expose the same heaps; no app domains.

    std::vector<ClrNativeHeapInfo> heaps = dac::EnumerateNativeHeapsFromSos(&sos);

    // The shared domain reuses the system domain's heaps, so each appears once (not twice).
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::LowFrequencyHeap), 1);
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::HighFrequencyHeap), 1);
    EXPECT_EQ(CountKind(heaps, NativeHeapKind::StubHeap), 1);
}
