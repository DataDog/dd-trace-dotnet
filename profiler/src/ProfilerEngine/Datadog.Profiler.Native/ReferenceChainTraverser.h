// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"
#include "InlineVTCache.h"
#include "GCDescReader.h"
#include "GCHeapRangeSet.h"
#include "TypeReferenceTree.h"
#include "VisitedObjectSet.h"
#include "ReferenceChainTypes.h"
#include <chrono>
#include <string>
#include <vector>

// Forward declarations
class IFrameStore;

// Traversal engine for building type-level reference chains.
//
// IMPORTANT: GetClassFromObject/GetObjectSize2/GetArrayObjectInfo can only be called
// from within ICorProfilerCallback methods (i.e. during GC dump events).
// They cannot be called from another thread or after the GC ends.
// Therefore, traversal MUST happen inside the OnBulkRoot* event handlers.
//
// Reference enumeration uses the GCDesc (fast path, no cache) for all objects.
// Inline value type tree attribution uses InlineVTCache (slow path, rare).
class ReferenceChainTraverser
{
public:
    struct TraversalFrame
    {
        uintptr_t objectAddress;
        TypeTreeNode* treeNode;
        uint32_t depth;
        ClassID classID;
        SIZE_T objectSize;
    };

    ReferenceChainTraverser(
        ICorProfilerInfo12* pCorProfilerInfo,
        IFrameStore* pFrameStore,
        TypeReferenceTree& tree,
        InlineVTCache& inlineVTCache,
        size_t visitedSetInitialCapacity = 512,
        const GCHeapRangeSet* pHeapRanges = nullptr);

    // Traverse from a single root (called from OnBulkRoot* event handlers).
    // A fresh VisitedObjectSet is used per root for cycle detection within that root's graph.
    void TraverseFromSingleRoot(const RootInfo& root);

    void LogStats() const;

    size_t GetVisitedHighWatermark() const { return _visited.GetBucketCount(); }
    size_t GetVisitedPeakEntryCount() const { return _visited.GetPeakEntryCount(); }

    // Whether the GCDesc reader passed (or has not yet failed) its runtime
    // self-test. When false, GCDesc-based traversal is disabled for this
    // traverser; the class histogram (which does not use GCDesc) is unaffected.
    //
    // This is the permanent, layout-level signal. A memory access fault (see
    // GetFaultCount/WasAbortedByFaults) is a data-level event and never flips it.
    bool IsGCDescTrusted() const { return _gcDescTrusted; }

    // Number of memory access faults recovered from during this traverser's life
    // (i.e. this dump). A fault is data-local -- it says nothing about the GCDesc
    // layout being wrong -- so it is tracked separately from IsGCDescTrusted().
    uint32_t GetFaultCount() const { return _faultCount; }

    // True once memory access faults have exhausted the per-dump budget. When set,
    // traversal stops for the rest of this dump only; the next dump gets a fresh
    // traverser (and a fresh budget) unless the manager decides otherwise.
    bool WasAbortedByFaults() const { return _faultBudgetExhausted; }

#ifdef DD_TEST
    // Unit tests only: perform a guarded read of one byte from ptr using the same
    // SIGSEGV/SIGBUS (Linux) or SEH (Windows) machinery as TraverseFromSingleRoot.
    void Test_FaultReadUnderGuard(const volatile void* ptr);
#endif

private:
    // Prepare the per-root state and push the root frame onto _traversalStack.
    // Unguarded: does not read object graph memory beyond the root's MethodTable
    // (see SeedRootGuarded for the fault-protected entry point).
    void SeedRoot(const RootInfo& root);

    // Fault-guarded wrapper around SeedRoot. Returns false if a memory access
    // fault occurred while seeding (the root is then skipped entirely).
    bool SeedRootGuarded(const RootInfo& root);

    // Iterative object graph traversal using an explicit stack.
    // Uses GCDesc to enumerate reference fields directly from the MethodTable (fast path).
    // Consults InlineVTCache for inline VT tree attribution (slow path, rare).
    //
    // Drains _traversalStack until it is empty (or the fault budget is exhausted,
    // or the GCDesc self-test fails). Because each frame is popped BEFORE it is
    // scanned, this can be safely re-entered after a fault: the faulting frame is
    // already gone and every unscanned frame is still on the stack.
    void DrainTraversalStack();

    // Fault-guarded wrapper around DrainTraversalStack. On a memory access fault it
    // calls OnTraversalFault and returns; the caller re-enters to resume with the
    // frames that remain on _traversalStack.
    void DrainTraversalStackGuarded();

    // Enqueue reference fields from inline value type array elements via GCDesc negative series.
    void EnqueueValueTypeArrayChildren(
        uintptr_t arrayAddress,
        ClassID arrayClassID,
        TypeTreeNode* currentNode,
        uint32_t depth);

    struct InlineVTOwner
    {
        TypeTreeNode* node;
        uint32_t depth;
    };

    // Materialize inline VT nodes once per containing object so instance counts
    // do not depend on how many reference slots the value type contains.
    void AddInlineValueTypeInstances(TypeTreeNode* currentNode, const InlineVTCache::InlineVTInfo& vtInfo);

    // Map a parent-object GCDesc ref offset to the deepest inline VT node that owns it.
    // Reference discovery remains driven by the parent object's GCDesc, matching CoreCLR's
    // scanner; InlineVTCache only supplies tree attribution ranges.
    InlineVTOwner GetInlineValueTypeOwner(
        TypeTreeNode* currentNode,
        uint32_t depth,
        ULONG refOffset,
        const InlineVTCache::InlineVTInfo& vtInfo,
        ULONG baseOffset = 0);

    // Process a discovered reference: insert into visited set, resolve class/size, build tree.
    // Returns true if the reference was newly inserted and pushed onto the stack.
    bool ProcessDiscoveredRef(uintptr_t refAddress, TypeTreeNode* parentNode, uint32_t depth);

    void PushTraversalFrameIfScannable(
        uintptr_t objectAddress,
        TypeTreeNode* treeNode,
        uint32_t depth,
        ClassID classID,
        SIZE_T objectSize);

    bool IsValidObjectAddress(uintptr_t address);
    std::string GetClassName(ClassID classID) const;

    void OnTraversalFault();

    ICorProfilerInfo12* _pCorProfilerInfo;
    IFrameStore* _pFrameStore;
    TypeReferenceTree& _tree;
    InlineVTCache& _inlineVTCache;

    // Coarse GC-heap plausibility filter (nullable). When null or empty, every
    // candidate address is accepted, matching the pre-filter behaviour.
    const GCHeapRangeSet* _pHeapRanges;

    // Per-root cycle detection.
    // Cleared between roots to avoid reallocating the bucket array.
    VisitedObjectSet _visited;

    // Used to keep track of all objects to visit when starting from a root.
    // Reused across roots to avoid repeated heap allocations.
    std::vector<TraversalFrame> _traversalStack;

    // Reusable scratch for multi-dimensional value-type array dimensions.
    // Kept as members (not locals) so no vector destructor runs on the guarded
    // traversal path, where a fault unwinds via siglongjmp / SEH without running
    // C++ destructors.
    std::vector<ULONG32> _dimSizesScratch;
    std::vector<int> _dimBoundsScratch;

    // Statistics
    uint64_t _objectsTraversed;
    uint64_t _rootsProcessed;
    uint64_t _rootCategoryCounts[RootCategoryCount] = {};
    std::chrono::nanoseconds _totalTraversalDuration{0};

    // Count of candidate references rejected by the GC heap-range filter
    // (see IsValidObjectAddress). Diagnostic only.
    uint64_t _refsRejectedByRangeFilter = 0;

    static constexpr size_t MinStackReserve = 64;
    size_t _traversalStackHighWatermark = MinStackReserve;

    // ---- Permanent, layout-level state ----
    // GCDesc reader self-test state. The self-test runs on the first few
    // scannable objects of a traversal and cross-checks the GCDesc/MethodTable
    // layout against profiling-API metadata. On a clear contradiction the reader
    // is disabled (_gcDescTrusted = false) for the rest of this traverser's life.
    static constexpr uint32_t MaxSelfTestObjects = 8;
    bool _gcDescTrusted = true;
    GCDesc::SelfTestResult _selfTest = GCDesc::SelfTestResult::Pending;
    uint32_t _selfTestObjectsChecked = 0;

    // ---- Per-dump, data-level state (reset with the traverser each dump) ----
    // Beyond this many recovered faults the heap is unstable enough (or our reads
    // wrong enough) that continuing is not worth the signal-handler round trips
    // taken while the runtime is suspended. This never disables the reader itself.
    static constexpr uint32_t MaxFaultsPerDump = 16;
    uint32_t _faultCount = 0;
    bool _faultBudgetExhausted = false;
};
