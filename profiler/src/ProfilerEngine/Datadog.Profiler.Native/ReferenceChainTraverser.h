// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"
#include "ClassLayoutCache.h"
#include "TypeReferenceTree.h"
#include "VisitedObjectSet.h"
#include "ReferenceChainTypes.h"
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
    // Traverse an object and its referenced children.
    // currentNode: the position in the type tree where this object's children will be added.
    // depth: current tree depth for limiting pathological cases.
    void TraverseObject(
        uintptr_t objectAddress,
        VisitedObjectSet& visited,
        TypeTreeNode* currentNode,
        uint32_t depth);

    // Traverse array elements using GetArrayObjectInfo.
    // Handles all array types: single-dimension (SZArray), jagged (arrays of arrays),
    // and multi-dimensional (matrices).
    void TraverseArray(
        uintptr_t arrayAddress,
        ClassID arrayClassID,
        const ClassLayoutCache::ClassLayoutData& layout,
        VisitedObjectSet& visited,
        TypeTreeNode* currentNode,
        uint32_t depth);

    bool IsValidObjectAddress(uintptr_t address) const;

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

    // Statistics
    uint64_t _objectsTraversed;
    uint64_t _rootsProcessed;
};
