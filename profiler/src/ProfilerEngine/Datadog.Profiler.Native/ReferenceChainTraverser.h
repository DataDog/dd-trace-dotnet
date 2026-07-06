// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"
#include "InlineVTCache.h"
#include "GCDescReader.h"
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
        size_t visitedSetInitialCapacity = 512);

    // Traverse from a single root (called from OnBulkRoot* event handlers).
    // A fresh VisitedObjectSet is used per root for cycle detection within that root's graph.
    void TraverseFromSingleRoot(const RootInfo& root);

    void LogStats() const;

    size_t GetVisitedHighWatermark() const { return _visited.GetBucketCount(); }
    size_t GetVisitedPeakEntryCount() const { return _visited.GetPeakEntryCount(); }

    // Whether the GCDesc reader passed (or has not yet failed) its runtime
    // self-test. When false, GCDesc-based traversal is disabled for this
    // traverser; the class histogram (which does not use GCDesc) is unaffected.
    bool IsGCDescTrusted() const { return _gcDescTrusted; }

#ifdef DD_TEST
    // Unit tests only: perform a guarded read of one byte from ptr using the same
    // SIGSEGV/SIGBUS (Linux) or SEH (Windows) machinery as TraverseFromSingleRoot.
    void Test_FaultReadUnderGuard(const volatile void* ptr);
#endif

private:
    void TraverseFromSingleRootImpl(const RootInfo& root);

    // Iterative object graph traversal using an explicit stack.
    // Uses GCDesc to enumerate reference fields directly from the MethodTable (fast path).
    // Consults InlineVTCache for inline VT tree attribution (slow path, rare).
    void TraverseObjectGraph(uintptr_t objectAddress, TypeTreeNode* currentNode, uint32_t depth,
                             ClassID rootClassID, SIZE_T rootObjectSize);

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

    bool IsValidObjectAddress(uintptr_t address) const;
    std::string GetClassName(ClassID classID) const;

    void OnTraversalFault();

    ICorProfilerInfo12* _pCorProfilerInfo;
    IFrameStore* _pFrameStore;
    TypeReferenceTree& _tree;
    InlineVTCache& _inlineVTCache;

    // Per-root cycle detection.
    // Cleared between roots to avoid reallocating the bucket array.
    VisitedObjectSet _visited;

    // Used to keep track of all objects to visit when starting from a root.
    // Reused across roots to avoid repeated heap allocations.
    std::vector<TraversalFrame> _traversalStack;

    // Statistics
    uint64_t _objectsTraversed;
    uint64_t _rootsProcessed;
    uint64_t _rootCategoryCounts[RootCategoryCount] = {};
    std::chrono::nanoseconds _totalTraversalDuration{0};

    static constexpr size_t MinStackReserve = 64;
    size_t _traversalStackHighWatermark = MinStackReserve;

    // GCDesc reader self-test state. The self-test runs on the first few
    // scannable objects of a traversal and cross-checks the GCDesc/MethodTable
    // layout against profiling-API metadata. On a clear contradiction the reader
    // is disabled (_gcDescTrusted = false) for the rest of this traverser's life.
    static constexpr uint32_t MaxSelfTestObjects = 8;
    bool _gcDescTrusted = true;
    GCDesc::SelfTestResult _selfTest = GCDesc::SelfTestResult::Pending;
    uint32_t _selfTestObjectsChecked = 0;
};
