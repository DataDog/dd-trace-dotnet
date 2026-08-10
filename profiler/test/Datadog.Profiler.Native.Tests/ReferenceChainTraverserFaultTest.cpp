// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "InlineVTCache.h"
#include "MockProfilerInfo.h"
#include "ReferenceChainTraverser.h"
#include "ReferenceChainTypes.h"
#include "TypeReferenceTree.h"
#include "VisitedObjectSet.h"
#include "IFrameStore.h"
#include "GCDescReader.h"

#include <cstdint>
#include <cstring>
#include <new>
#include <stdexcept>
#include <unordered_map>
#include <utility>

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

// Resolves GetClassFromObject/GetObjectSize2 from an explicit address -> (classID, size)
// map so a multi-object graph can be driven through the traverser. IsArrayClass returns
// S_OK to short-circuit the self-test and inline-VT metadata.
class GraphMockProfiler : public MockProfilerInfo
{
public:
    void AddObject(uintptr_t address, ClassID classID, SIZE_T size)
    {
        _objects[address] = {classID, size};
    }

    HRESULT STDMETHODCALLTYPE GetClassFromObject(ObjectID objectId, ClassID* pClassId) override
    {
        auto it = _objects.find(static_cast<uintptr_t>(objectId));
        if (it == _objects.end())
        {
            return E_FAIL;
        }
        if (pClassId != nullptr)
        {
            *pClassId = it->second.first;
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetObjectSize2(ObjectID objectId, SIZE_T* pcSize) override
    {
        auto it = _objects.find(static_cast<uintptr_t>(objectId));
        if (it == _objects.end())
        {
            return E_FAIL;
        }
        if (pcSize != nullptr)
        {
            *pcSize = it->second.second;
        }
        return S_OK;
    }

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

private:
    std::unordered_map<uintptr_t, std::pair<ClassID, SIZE_T>> _objects;
};

// Throws from a profiling API call made deep inside the guarded traversal. Stands in for
// anything that is not a memory access fault: std::bad_alloc from our own containers, or
// a CLR exception surfacing through the profiling API.
class ThrowingMockProfiler : public GraphMockProfiler
{
public:
    HRESULT STDMETHODCALLTYPE GetClassFromObject(ObjectID /*objectId*/, ClassID* /*pClassId*/) override
    {
        throw std::bad_alloc();
    }
};

// Drives the GCDesc self-test to a Failed verdict: not an array (IsArrayClass != S_OK),
// the MethodTable flag says "contains pointers", yet the metadata reports zero fields.
// That contradiction is what ValidateAgainstMetadata treats as a layout mismatch.
class SelfTestFailMockProfiler : public MockProfilerInfo
{
public:
    HRESULT STDMETHODCALLTYPE IsArrayClass(ClassID, CorElementType*, ClassID*, ULONG*) override
    {
        return S_FALSE;
    }

    HRESULT STDMETHODCALLTYPE GetClassLayout(ClassID, COR_FIELD_OFFSET[], ULONG, ULONG* pcFieldOffset, ULONG* pulClassSize) override
    {
        if (pcFieldOffset != nullptr)
        {
            *pcFieldOffset = 0; // zero fields contradicts the ContainsPointers flag
        }
        if (pulClassSize != nullptr)
        {
            *pulClassSize = 64;
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

// Build a fake MethodTable + GCDesc in storage describing one positive series of
// `refCount` consecutive reference slots starting at object offset 0.
ClassID BuildFakeMethodTableWithRefs(std::uint8_t* storage, size_t storageSize, ptrdiff_t refCount, SIZE_T objectSize)
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
    // rangeSize = encodedSize + objectSize = refCount * sizeof(void*)
    s.encodedSize = refCount * static_cast<ptrdiff_t>(sizeof(void*)) - static_cast<ptrdiff_t>(objectSize);
    s.offset = 0;

    return reinterpret_cast<ClassID>(mtBase);
}

// Single-ref-slot fake MethodTable (kept for the existing single-fault tests).
ClassID BuildFakeMethodTableWithOneRefSeries(std::uint8_t* storage, size_t storageSize)
{
    // objectSize 64 with one ref => encodedSize = 8 - 64 = -56 (matches the original helper).
    return BuildFakeMethodTableWithRefs(storage, storageSize, 1, 64);
}

// Fake MethodTable whose ContainsPointers flag is clear: such a type is never scanned.
ClassID BuildFakeMethodTableNoPointers(std::uint8_t* storage, size_t storageSize)
{
    std::memset(storage, 0, storageSize);
    constexpr size_t mtOffset = 2048;
    auto* mtBase = reinterpret_cast<std::uint8_t*>(storage + mtOffset);
    auto* flags = reinterpret_cast<std::uint32_t*>(mtBase);
    *flags = 0;
    return reinterpret_cast<ClassID>(mtBase);
}
} // namespace

#ifdef _WINDOWS
TEST(ReferenceChainTraverserFaultTest, SehGuardCatchesAccessViolation)
{
    ASSERT_TRUE(SehCatchesNullWrite());
}
#endif

// Pure VisitedObjectSet behaviour: after a possibly-interrupted insert is flagged,
// the next Clear() must wipe the whole table so no stale "visited" address leaks.
TEST(ReferenceChainTraverserFaultTest, VisitedSetIsFullyClearedAfterFault)
{
    VisitedObjectSet visited(16);
    visited.MarkVisited(0x123000);
    ASSERT_TRUE(visited.IsVisited(0x123000));

    visited.MarkPossiblyInconsistent();
    visited.Clear();

    ASSERT_FALSE(visited.IsVisited(0x123000));
    ASSERT_EQ(visited.Size(), 0u);

    // The set must remain fully usable after a full wipe.
    visited.MarkVisited(0x456000);
    ASSERT_TRUE(visited.IsVisited(0x456000));
    ASSERT_FALSE(visited.IsVisited(0x123000));
}

namespace
{
// root -> child through a single reference slot at offset 0. Returns the root's RootInfo;
// rootObj/childObj are owned by the caller.
struct TwoObjectGraph
{
    alignas(64) std::uint8_t rootMt[4096]{};
    alignas(64) std::uint8_t childMt[4096]{};
    alignas(8) std::uint8_t rootObj[64]{};
    alignas(8) std::uint8_t childObj[64]{};

    ClassID rootClass = 0;
    ClassID childClass = 0;

    void Build(GraphMockProfiler& profiler)
    {
        rootClass = BuildFakeMethodTableWithRefs(rootMt, sizeof(rootMt), 1, 64);
        childClass = BuildFakeMethodTableNoPointers(childMt, sizeof(childMt));

        *reinterpret_cast<uintptr_t*>(rootObj) = reinterpret_cast<uintptr_t>(childObj);
        profiler.AddObject(reinterpret_cast<uintptr_t>(childObj), childClass, 64);
    }

    RootInfo GetRoot() const
    {
        return RootInfo(reinterpret_cast<uintptr_t>(rootObj), RootCategory::Stack, rootClass, 64);
    }
};
} // namespace

// An exception thrown deep inside the guarded traversal must not escape into the EventPipe
// callback that drives the dump, must not be miscounted as a memory access fault, and must
// stop this dump's traversal without distrusting the GCDesc layout model.
TEST(ReferenceChainTraverserFaultTest, ExceptionEscapingTheGuardAbortsTheDumpWithoutCountingAFault)
{
    ThrowingMockProfiler profiler;
    TwoObjectGraph graph;
    graph.Build(profiler);

    ICorProfilerInfo12* pInfo = reinterpret_cast<ICorProfilerInfo12*>(static_cast<ICorProfilerInfo4*>(&profiler));
    NullFrameStore frameStore;
    TypeReferenceTree tree;
    InlineVTCache vtCache(pInfo, &frameStore, nullptr);
    ReferenceChainTraverser traverser(pInfo, &frameStore, tree, vtCache, 16);

    EXPECT_NO_THROW(traverser.TraverseFromSingleRoot(graph.GetRoot()));

    EXPECT_TRUE(traverser.WasAbortedByFaults());
    EXPECT_EQ(traverser.GetFaultCount(), 0u);
    EXPECT_TRUE(traverser.IsGCDescTrusted());
}

#if defined(DD_TEST)

// The guard only recovers from memory access faults, so a C++ exception must pass straight
// through it. On Windows that means the SEH filter no longer swallows exception code
// 0xE06D7363; on Linux it means the "in guarded traversal" flag is cleared while unwinding,
// without which the next fault on this thread would siglongjmp into a dead frame.
TEST(ReferenceChainTraverserFaultTest, ExceptionUnderGuardPropagatesAndLeavesTheGuardUsable)
{
    void* badPage = MapInaccessiblePage();
    ASSERT_NE(badPage, nullptr);

    TraversalFaultMockProfiler profiler;
    ICorProfilerInfo12* pInfo = reinterpret_cast<ICorProfilerInfo12*>(static_cast<ICorProfilerInfo4*>(&profiler));
    NullFrameStore frameStore;
    TypeReferenceTree tree;
    InlineVTCache vtCache(pInfo, &frameStore, nullptr);
    ReferenceChainTraverser traverser(pInfo, &frameStore, tree, vtCache, 16);

    EXPECT_THROW(traverser.Test_ThrowUnderGuard(), std::runtime_error);

    // The exception was not mistaken for an unreadable address.
    EXPECT_EQ(traverser.GetFaultCount(), 0u);

    // A real fault afterwards is still recovered rather than crashing the process,
    // which is only true if the guard left no state behind.
    traverser.Test_FaultReadUnderGuard(badPage);
    EXPECT_EQ(traverser.GetFaultCount(), 1u);
    EXPECT_TRUE(traverser.IsGCDescTrusted());

    UnmapPage(badPage);
}

TEST(ReferenceChainTraverserFaultTest, TestFaultReadUnderGuardIncrementsFaultCountButKeepsGCDescTrusted)
{
    void* badPage = MapInaccessiblePage();
    ASSERT_NE(badPage, nullptr);

    TraversalFaultMockProfiler profiler;
    ICorProfilerInfo12* pInfo = reinterpret_cast<ICorProfilerInfo12*>(static_cast<ICorProfilerInfo4*>(&profiler));
    NullFrameStore frameStore;
    TypeReferenceTree tree;
    InlineVTCache vtCache(pInfo, &frameStore, nullptr);
    ReferenceChainTraverser traverser(pInfo, &frameStore, tree, vtCache, 16);

    ASSERT_TRUE(traverser.IsGCDescTrusted());
    ASSERT_EQ(traverser.GetFaultCount(), 0u);

    traverser.Test_FaultReadUnderGuard(badPage);

    // A memory access fault is data-level: it is counted but never distrusts GCDesc.
    ASSERT_EQ(traverser.GetFaultCount(), 1u);
    ASSERT_TRUE(traverser.IsGCDescTrusted());
    ASSERT_FALSE(traverser.WasAbortedByFaults());

    UnmapPage(badPage);
}

TEST(ReferenceChainTraverserFaultTest, TraverseFromSingleRootFaultKeepsGCDescTrusted)
{
    void* badPage = MapInaccessiblePage();
    ASSERT_NE(badPage, nullptr);

    alignas(64) std::uint8_t mtStorage[4096]{};
    ClassID fakeClass = BuildFakeMethodTableWithOneRefSeries(mtStorage, sizeof(mtStorage));

    TraversalFaultMockProfiler profiler;
    ICorProfilerInfo12* pInfo = reinterpret_cast<ICorProfilerInfo12*>(static_cast<ICorProfilerInfo4*>(&profiler));
    NullFrameStore frameStore;
    TypeReferenceTree tree;
    InlineVTCache vtCache(pInfo, &frameStore, nullptr);
    ReferenceChainTraverser traverser(pInfo, &frameStore, tree, vtCache, 16);

    RootInfo root(reinterpret_cast<uintptr_t>(badPage), RootCategory::Stack, fakeClass, 64);
    ASSERT_TRUE(traverser.IsGCDescTrusted());

    traverser.TraverseFromSingleRoot(root);

    // The root object itself is unreadable, so scanning it faults once. That fault
    // must NOT permanently disable GCDesc traversal.
    ASSERT_EQ(traverser.GetFaultCount(), 1u);
    ASSERT_TRUE(traverser.IsGCDescTrusted());
    ASSERT_FALSE(traverser.WasAbortedByFaults());

    // A later, readable root is still processed (the traverser was not disabled).
    traverser.TraverseFromSingleRoot(root);

    UnmapPage(badPage);
}

TEST(ReferenceChainTraverserFaultTest, TraversalResumesAfterFault)
{
    void* badPage = MapInaccessiblePage();
    ASSERT_NE(badPage, nullptr);

    alignas(64) std::uint8_t rootMt[4096]{};
    alignas(64) std::uint8_t childMt[4096]{};
    alignas(64) std::uint8_t badMt[4096]{};
    alignas(64) std::uint8_t grandChildMt[4096]{};

    // root has two ref slots (offset 0 -> childA, offset 8 -> B on the bad page);
    // childA has one ref slot (offset 0 -> grandChild); B faults when scanned;
    // grandChild has no pointers (leaf).
    ClassID rootClass = BuildFakeMethodTableWithRefs(rootMt, sizeof(rootMt), 2, 64);
    ClassID childClass = BuildFakeMethodTableWithRefs(childMt, sizeof(childMt), 1, 64);
    ClassID badClass = BuildFakeMethodTableWithRefs(badMt, sizeof(badMt), 1, 64);
    ClassID grandChildClass = BuildFakeMethodTableNoPointers(grandChildMt, sizeof(grandChildMt));

    alignas(8) std::uint8_t rootObj[64]{};
    alignas(8) std::uint8_t childObj[64]{};
    alignas(8) std::uint8_t grandChildObj[16]{};

    uintptr_t childAddr = reinterpret_cast<uintptr_t>(childObj);
    uintptr_t grandChildAddr = reinterpret_cast<uintptr_t>(grandChildObj);
    uintptr_t badAddr = reinterpret_cast<uintptr_t>(badPage);

    *reinterpret_cast<uintptr_t*>(rootObj + 0) = childAddr;
    *reinterpret_cast<uintptr_t*>(rootObj + sizeof(void*)) = badAddr;
    *reinterpret_cast<uintptr_t*>(childObj + 0) = grandChildAddr;

    GraphMockProfiler profiler;
    profiler.AddObject(childAddr, childClass, 64);
    profiler.AddObject(badAddr, badClass, 64);
    profiler.AddObject(grandChildAddr, grandChildClass, 32);

    ICorProfilerInfo12* pInfo = reinterpret_cast<ICorProfilerInfo12*>(static_cast<ICorProfilerInfo4*>(&profiler));
    NullFrameStore frameStore;
    TypeReferenceTree tree;
    InlineVTCache vtCache(pInfo, &frameStore, nullptr);
    ReferenceChainTraverser traverser(pInfo, &frameStore, tree, vtCache, 16);

    RootInfo root(reinterpret_cast<uintptr_t>(rootObj), RootCategory::Stack, rootClass, 64);
    traverser.TraverseFromSingleRoot(root);

    // Exactly one object (B) faulted; the traverser stays trusted and not aborted.
    ASSERT_EQ(traverser.GetFaultCount(), 1u);
    ASSERT_TRUE(traverser.IsGCDescTrusted());
    ASSERT_FALSE(traverser.WasAbortedByFaults());

    // The sibling branch (root -> childA -> grandChild) was traversed AFTER the fault,
    // which proves traversal resumed instead of abandoning the whole root.
    RootKey key{rootClass, RootCategory::Stack};
    auto it = tree._roots.find(key);
    ASSERT_NE(it, tree._roots.end());

    const TypeTreeNode& rootNode = it->second->node;
    const TypeTreeNode* childNode = rootNode.GetChild(childClass);
    ASSERT_NE(childNode, nullptr);
    ASSERT_NE(childNode->GetChild(grandChildClass), nullptr);

    UnmapPage(badPage);
}

TEST(ReferenceChainTraverserFaultTest, FaultBudgetStopsDumpWithoutDistrustingGCDesc)
{
    void* badPage = MapInaccessiblePage();
    ASSERT_NE(badPage, nullptr);

    TraversalFaultMockProfiler profiler;
    ICorProfilerInfo12* pInfo = reinterpret_cast<ICorProfilerInfo12*>(static_cast<ICorProfilerInfo4*>(&profiler));
    NullFrameStore frameStore;
    TypeReferenceTree tree;
    InlineVTCache vtCache(pInfo, &frameStore, nullptr);
    ReferenceChainTraverser traverser(pInfo, &frameStore, tree, vtCache, 16);

    // Drive enough faults to exhaust the per-dump budget (MaxFaultsPerDump == 16).
    for (int i = 0; i < 16; i++)
    {
        traverser.Test_FaultReadUnderGuard(badPage);
    }

    ASSERT_TRUE(traverser.WasAbortedByFaults());
    ASSERT_EQ(traverser.GetFaultCount(), 16u);
    // Even at the budget, the layout self-test signal is untouched.
    ASSERT_TRUE(traverser.IsGCDescTrusted());

    // Once the budget is exhausted, further guarded reads are skipped (no more faults).
    traverser.Test_FaultReadUnderGuard(badPage);
    ASSERT_EQ(traverser.GetFaultCount(), 16u);

    UnmapPage(badPage);
}

TEST(ReferenceChainTraverserFaultTest, SelfTestFailureStillDisablesPermanently)
{
    alignas(64) std::uint8_t mtStorage[4096]{};
    ClassID fakeClass = BuildFakeMethodTableWithOneRefSeries(mtStorage, sizeof(mtStorage));

    alignas(8) std::uint8_t rootObj[64]{};

    SelfTestFailMockProfiler profiler;
    ICorProfilerInfo12* pInfo = reinterpret_cast<ICorProfilerInfo12*>(static_cast<ICorProfilerInfo4*>(&profiler));
    NullFrameStore frameStore;
    TypeReferenceTree tree;
    InlineVTCache vtCache(pInfo, &frameStore, nullptr);
    ReferenceChainTraverser traverser(pInfo, &frameStore, tree, vtCache, 16);

    ASSERT_TRUE(traverser.IsGCDescTrusted());

    // The object is readable (no memory fault), but the GCDesc self-test detects a
    // layout contradiction and disables the reader -- the permanent path, unchanged.
    RootInfo root(reinterpret_cast<uintptr_t>(rootObj), RootCategory::Stack, fakeClass, 64);
    traverser.TraverseFromSingleRoot(root);

    ASSERT_FALSE(traverser.IsGCDescTrusted());
    ASSERT_EQ(traverser.GetFaultCount(), 0u);
    ASSERT_FALSE(traverser.WasAbortedByFaults());
}

#endif
