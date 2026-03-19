// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"
#include "ClassLayoutCache.h"
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
class ReferenceChainTraverser
{
public:
    ReferenceChainTraverser(
        ICorProfilerInfo12* pCorProfilerInfo,
        IFrameStore* pFrameStore,
        TypeReferenceTree& tree);

    // Traverse from a single root (called from OnBulkRoot* event handlers).
    // A fresh VisitedObjectSet is used per root for cycle detection within that root's graph.
    // This allows the same object to be reached from different roots, capturing all type-level paths.
    void TraverseFromSingleRoot(const RootInfo& root);

    // Log traversal statistics
    void LogStats() const;

private:
    struct TraversalFrame
    {
        uintptr_t objectAddress;
        TypeTreeNode* treeNode;
        uint32_t depth;
    };

    // Iterative object graph traversal using an explicit stack.
    // Seeds the stack with the initial frame and processes until empty.
    void TraverseObjectGraph(uintptr_t objectAddress, TypeTreeNode* currentNode, uint32_t depth);

    // Enqueue array element children onto _traversalStack.
    // Handles reference type arrays, value type arrays with reference fields,
    // jagged arrays, and multi-dimensional arrays.
    void EnqueueArrayChildren(
        uintptr_t arrayAddress,
        ClassID arrayClassID,
        const ClassLayoutCache::ClassLayoutData& layout,
        TypeTreeNode* currentNode,
        uint32_t depth);

    // Enqueue reference fields from inline value type array elements onto _traversalStack.
    void EnqueueValueTypeArrayChildren(
        BYTE* pData,
        uint64_t totalElements,
        const ClassLayoutCache::ClassLayoutData& elementLayout,
        TypeTreeNode* currentNode,
        uint32_t depth);

    bool IsValidObjectAddress(uintptr_t address) const;
    std::string GetClassName(ClassID classID) const;

    // Read a reference field from an object.
    // objectAddress: ObjectID (points to the MethodTable pointer at offset 0)
    // fieldOffset: byte offset from GetClassLayout (relative to objectAddress, includes MethodTable*)
    // objectSize: total object size from GetObjectSize2 (for bounds checking)
    // Returns the ObjectID stored in the field, or 0 if the read is invalid.
    uintptr_t ReadFieldReference(uintptr_t objectAddress, ULONG fieldOffset, SIZE_T objectSize) const;

    ICorProfilerInfo12* _pCorProfilerInfo;
    IFrameStore* _pFrameStore;
    TypeReferenceTree& _tree;

    // Shared across root traversals (class layouts don't change between roots)
    ClassLayoutCache _layoutCache;

    // Reused across roots: cleared between roots to avoid reallocating the bucket array.
    VisitedObjectSet _visited;

    // Reused across roots to avoid repeated heap allocations.
    std::vector<TraversalFrame> _traversalStack;

    // Statistics
    uint64_t _objectsTraversed;
    uint64_t _rootsProcessed;
    uint64_t _rootCategoryCounts[8] = {};
    std::chrono::nanoseconds _totalTraversalDuration{0};
};
