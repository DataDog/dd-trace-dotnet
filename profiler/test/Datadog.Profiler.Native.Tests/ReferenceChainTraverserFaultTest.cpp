// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "InlineVTCache.h"
#include "MockProfilerInfo.h"
#include "ReferenceChainTraverser.h"
#include "ReferenceChainTypes.h"
#include "TypeReferenceTree.h"
#include "IFrameStore.h"
#include "GCDescReader.h"

#include <cstdint>
#include <cstring>

#ifdef LINUX
#include <sys/mman.h>
#include <unistd.h>
#endif

#ifdef _WINDOWS
#include <Windows.h>
#endif

namespace
{
class NullFrameStore : public IFrameStore
{
public:
    std::pair<bool, FrameInfoView> GetFrame(uintptr_t) override
    {
        return {false, {"", "", "", 0}};
    }
    bool GetTypeName(ClassID, std::string&) override
    {
        return false;
    }
    bool GetTypeName(ClassID, std::string_view&) override
    {
        return false;
    }
    size_t GetMemorySize() const override
    {
        return 0;
    }
    void LogMemoryBreakdown() const override
    {
    }
};

// IsArrayClass S_OK short-circuits GCDesc self-test and inline-VT metadata (returns Pending / no VT info).
class TraversalFaultMockProfiler : public MockProfilerInfo
{
public:
    HRESULT STDMETHODCALLTYPE IsArrayClass(ClassID /*classId*/, CorElementType* pBaseElemType, ClassID* pBaseClassId, ULONG* pRank) override
    {
        if (pBaseElemType != nullptr)
        {
            *pBaseElemType = ELEMENT_TYPE_CLASS;
        }
        if (pBaseClassId != nullptr)
        {
            *pBaseClassId = 0;
        }
        if (pRank != nullptr)
        {
            *pRank = 1;
        }
        return S_OK;
    }
};

#if defined(_WINDOWS)
void* MapInaccessiblePage()
{
    SYSTEM_INFO si{};
    GetSystemInfo(&si);
    const size_t pageSize = si.dwPageSize != 0 ? static_cast<size_t>(si.dwPageSize) : 4096;
    return VirtualAlloc(nullptr, pageSize, MEM_COMMIT | MEM_RESERVE, PAGE_NOACCESS);
}

void UnmapPage(void* p)
{
    if (p != nullptr)
    {
        VirtualFree(p, 0, MEM_RELEASE);
    }
}

// MSVC C2712: __try/__except cannot appear in functions that require C++ object unwinding (e.g. gtest TestBody).
bool SehCatchesNullWrite()
{
    __try
    {
        volatile int* p = nullptr;
        *p = 1;
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        return true;
    }
    return false;
}
#elif defined(LINUX)
void* MapInaccessiblePage()
{
    const long pageSize = sysconf(_SC_PAGESIZE);
    void* p = mmap(nullptr, static_cast<size_t>(pageSize), PROT_NONE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
    return p == MAP_FAILED ? nullptr : p;
}

void UnmapPage(void* p)
{
    if (p != nullptr)
    {
        const long pageSize = sysconf(_SC_PAGESIZE);
        munmap(p, static_cast<size_t>(pageSize));
    }
}
#endif

// Build a fake MethodTable + GCDesc in buffer so EnumerateObjectRefs reads one pointer slot at object offset 0.
ClassID BuildFakeMethodTableWithOneRefSeries(std::uint8_t* storage, size_t storageSize)
{
    std::memset(storage, 0, storageSize);
    constexpr size_t mtOffset = 2048;
    auto* mtBase = reinterpret_cast<std::uint8_t*>(storage + mtOffset);
    auto* flags = reinterpret_cast<std::uint32_t*>(mtBase);
    *flags = GCDesc::Flag_ContainsPointers;

    auto* mtAsPtr = reinterpret_cast<ptrdiff_t*>(mtBase);
    // series count immediately before MethodTable (mt[-1])
    mtAsPtr[-1] = 1;

    // series[-1] relative to series pointer (series == mtAsPtr - 1, same address as &mtAsPtr[-1])
    auto* seriesBase = reinterpret_cast<GCDesc::GCDescSeries*>(mtAsPtr - 1);
    GCDesc::GCDescSeries& s = seriesBase[-1];
    // rangeSize = encodedSize + objectSize = 8 for one ref with objectSize 64 => encodedSize = -56
    s.encodedSize = -56;
    s.offset = 0;

    return reinterpret_cast<ClassID>(mtBase);
}
} // namespace

#ifdef _WINDOWS
TEST(ReferenceChainTraverserFaultTest, SehGuardCatchesAccessViolation)
{
    ASSERT_TRUE(SehCatchesNullWrite());
}
#endif

#if defined(DD_TEST)

TEST(ReferenceChainTraverserFaultTest, TestFaultReadUnderGuardDisablesGCDescTraversal)
{
    void* badPage = MapInaccessiblePage();
    ASSERT_NE(badPage, nullptr);

    TraversalFaultMockProfiler profiler;
    ICorProfilerInfo12* pInfo = reinterpret_cast<ICorProfilerInfo12*>(static_cast<ICorProfilerInfo4*>(&profiler));
    NullFrameStore frameStore;
    TypeReferenceTree tree;
    InlineVTCache vtCache(pInfo, &frameStore);
    ReferenceChainTraverser traverser(pInfo, &frameStore, tree, vtCache, 16);

    ASSERT_TRUE(traverser.IsGCDescTrusted());
    traverser.Test_FaultReadUnderGuard(badPage);
    ASSERT_FALSE(traverser.IsGCDescTrusted());

    traverser.Test_FaultReadUnderGuard(badPage);

    UnmapPage(badPage);
}

TEST(ReferenceChainTraverserFaultTest, TraverseFromSingleRootFaultDisablesFurtherTraversal)
{
    void* badPage = MapInaccessiblePage();
    ASSERT_NE(badPage, nullptr);

    alignas(64) std::uint8_t mtStorage[4096]{};
    ClassID fakeClass = BuildFakeMethodTableWithOneRefSeries(mtStorage, sizeof(mtStorage));

    TraversalFaultMockProfiler profiler;
    ICorProfilerInfo12* pInfo = reinterpret_cast<ICorProfilerInfo12*>(static_cast<ICorProfilerInfo4*>(&profiler));
    NullFrameStore frameStore;
    TypeReferenceTree tree;
    InlineVTCache vtCache(pInfo, &frameStore);
    ReferenceChainTraverser traverser(pInfo, &frameStore, tree, vtCache, 16);

    RootInfo root(reinterpret_cast<uintptr_t>(badPage), RootCategory::Stack, fakeClass, 64);
    ASSERT_TRUE(traverser.IsGCDescTrusted());
    traverser.TraverseFromSingleRoot(root);
    ASSERT_FALSE(traverser.IsGCDescTrusted());

    traverser.TraverseFromSingleRoot(root);

    UnmapPage(badPage);
}

#endif
