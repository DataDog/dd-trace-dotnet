// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ReferenceChainTraverser.h"
#include "Log.h"
#include "OpSysTools.h"
#include <algorithm>
#include <chrono>

// CLR object layout:
//   ObjectID + 0              : MethodTable* (the "reference points to the MethodTable")
//   ObjectID + sizeof(void*)  : first instance field
//   ...
//
// ICorProfilerInfo::GetClassLayout returns field offsets that are relative to the ObjectID
// and already include the MethodTable pointer size (sizeof(void*)).
// So the minimum valid field offset is sizeof(void*).
static constexpr ULONG MinFieldOffset = sizeof(void*);

ReferenceChainTraverser::ReferenceChainTraverser(
    ICorProfilerInfo12* pCorProfilerInfo,
    IFrameStore* pFrameStore,
    TypeReferenceTree& tree)
    : _pCorProfilerInfo(pCorProfilerInfo),
      _pFrameStore(pFrameStore),
      _tree(tree),
      _layoutCache(pCorProfilerInfo, pFrameStore),
      _objectsTraversed(0),
      _rootsProcessed(0)
{
}

void ReferenceChainTraverser::TraverseFromSingleRoot(const RootInfo& root) {
    // Add the root to the tree and get the tree node to navigate from
    TypeTreeNode* rootNode = _tree.AddRoot(root.classID, root.category, root.objectSize);

    // Fresh visited set for this root: detects cycles within this root's graph
    // while allowing the same object to be reached from different roots
    VisitedObjectSet visited;

    // Traverse the object graph from this root.
    // rootNode is the tree position; children will be added under it.
    // Depth starts at 1 (the root itself).
    TraverseObject(root.address, visited, rootNode, 1);

    _rootsProcessed++;
}

void ReferenceChainTraverser::LogStats() const {
    Log::Debug("Reference chain traversal completed: ",
               _rootsProcessed, " roots, ",
               _objectsTraversed, " objects traversed");
}

void ReferenceChainTraverser::TraverseObject(
    uintptr_t objectAddress,
    VisitedObjectSet& visited,
    TypeTreeNode* currentNode,
    uint32_t depth) {

    // Check if already visited in this root's traversal (cycle detection)
    if (visited.IsVisited(objectAddress)) {
        return;
    }

    // Depth limit to prevent pathological cases (e.g., million-element linked lists)
    if (depth > MaxTreeDepth) {
        return;
    }

    // Mark as visited
    visited.MarkVisited(objectAddress);
    _objectsTraversed++;

    // Get object's ClassID
    ClassID classID = 0;
    HRESULT hr = _pCorProfilerInfo->GetClassFromObject(objectAddress, &classID);
    if (FAILED(hr) || classID == 0) {
        return;  // Invalid object
    }

    // Get object size using GetObjectSize2 (supports objects > 4GB)
    SIZE_T objectSize = 0;
    hr = _pCorProfilerInfo->GetObjectSize2(objectAddress, &objectSize);
    if (FAILED(hr) || objectSize == 0) {
        return;
    }

    // Get class layout
    const ClassLayoutCache::ClassLayoutData* layout = _layoutCache.GetLayout(classID);
    if (layout == nullptr) {
        return;
    }

    // Handle arrays
    if (layout->isArray) {
        TraverseArray(objectAddress, classID, *layout, visited, currentNode, depth);
        return;
    }

    // Traverse reference fields
    for (const auto& field : layout->fields) {
        if (!field.isReferenceType) {
            continue;  // Skip non-reference fields
        }

        // Read the field value (object reference) with bounds checking.
        uintptr_t fieldValue = ReadFieldReference(objectAddress, field.offset, objectSize);

        if (fieldValue == 0) {
            continue;  // Null reference or invalid read
        }

        // Verify it's a valid object address
        if (!IsValidObjectAddress(fieldValue)) {
            continue;
        }

        // If this object was already visited in the current root traversal,
        // it's a back-reference (cycle). Skip it.
        if (visited.IsVisited(fieldValue)) {
            continue;
        }

        // Get the target object's type
        ClassID targetClassID = 0;
        hr = _pCorProfilerInfo->GetClassFromObject(fieldValue, &targetClassID);
        if (FAILED(hr) || targetClassID == 0) {
            continue;
        }

        // Get target size
        SIZE_T targetSize = 0;
        _pCorProfilerInfo->GetObjectSize2(fieldValue, &targetSize);

        // Navigate the tree: get or create a child node for this target type
        // under the CURRENT tree position. This preserves the full path context.
        TypeTreeNode* childNode = currentNode->GetOrCreateChild(targetClassID);
        childNode->AddInstance(targetSize);

        // Recursively traverse the referenced object, passing the child node
        // as the new tree position.
        TraverseObject(fieldValue, visited, childNode, depth + 1);
    }
}

void ReferenceChainTraverser::TraverseArray(
    uintptr_t arrayAddress,
    ClassID arrayClassID,
    const ClassLayoutCache::ClassLayoutData& layout,
    VisitedObjectSet& visited,
    TypeTreeNode* currentNode,
    uint32_t depth) {

    // Check if array element type is a reference type
    if (layout.arrayElementType != ELEMENT_TYPE_CLASS &&
        layout.arrayElementType != ELEMENT_TYPE_STRING &&
        layout.arrayElementType != ELEMENT_TYPE_OBJECT &&
        layout.arrayElementType != ELEMENT_TYPE_SZARRAY &&
        layout.arrayElementType != ELEMENT_TYPE_ARRAY) {
        return;  // Array of value types, no references to follow
    }

    ULONG32 rank = layout.arrayRank;
    if (rank == 0) {
        return;  // Invalid rank
    }

    // Use GetArrayObjectInfo to safely get dimension sizes and a pointer to the data.
    std::vector<ULONG32> dimensionSizes(rank);
    std::vector<int> dimensionLowerBounds(rank);
    BYTE* pData = nullptr;

    HRESULT hr = _pCorProfilerInfo->GetArrayObjectInfo(
        static_cast<ObjectID>(arrayAddress),
        rank,
        dimensionSizes.data(),
        dimensionLowerBounds.data(),
        &pData);

    if (FAILED(hr) || pData == nullptr) {
        return;
    }

    // Compute total number of elements (product of all dimension sizes)
    uint64_t totalElements = 1;
    for (ULONG32 d = 0; d < rank; d++) {
        totalElements *= dimensionSizes[d];
    }

    if (totalElements == 0) {
        return;  // Empty array
    }

    // pData points to the first element.
    // For reference type arrays, each element is a pointer-sized reference.
    uintptr_t* pElements = reinterpret_cast<uintptr_t*>(pData);

    for (uint64_t i = 0; i < totalElements; i++) {
        uintptr_t elementAddress = pElements[i];

        if (elementAddress == 0) {
            continue;  // Null element
        }

        // Verify valid object
        if (!IsValidObjectAddress(elementAddress)) {
            continue;
        }

        // Skip already-visited objects (cycle / shared reference).
        if (visited.IsVisited(elementAddress)) {
            continue;
        }

        // Get element type
        ClassID elementClassID = 0;
        hr = _pCorProfilerInfo->GetClassFromObject(elementAddress, &elementClassID);
        if (FAILED(hr) || elementClassID == 0) {
            continue;
        }

        // Get element size
        SIZE_T elementSizeBytes = 0;
        _pCorProfilerInfo->GetObjectSize2(elementAddress, &elementSizeBytes);

        // Navigate the tree: get or create child node under current position
        TypeTreeNode* childNode = currentNode->GetOrCreateChild(elementClassID);
        childNode->AddInstance(elementSizeBytes);

        // Recursively traverse the element
        TraverseObject(elementAddress, visited, childNode, depth + 1);
    }
}

bool ReferenceChainTraverser::IsValidObjectAddress(uintptr_t address) const {
    // Basic validation - check if address is in reasonable range
    if (address == 0 || address < 0x10000) {
        return false;  // Null or too small
    }

    // Check alignment (objects are typically pointer-aligned)
    if ((address % sizeof(void*)) != 0) {
        return false;
    }

    return true;
}

uintptr_t ReferenceChainTraverser::ReadFieldReference(uintptr_t objectAddress, ULONG fieldOffset, SIZE_T objectSize) const {
    // CLR object layout:
    //   objectAddress + 0              = MethodTable* (NOT a field!)
    //   objectAddress + sizeof(void*)  = first instance field
    //
    // GetClassLayout should return offsets relative to objectAddress, including
    // the MethodTable pointer size (sizeof(void*)). However, in practice,
    // offset 0 has been observed during debugging (e.g., for boxed value types
    // where GetClassLayout may return offsets relative to the value type's field
    // area rather than the ObjectID). Reading at offset 0 would dereference the
    // MethodTable pointer as an ObjectID, causing a crash.
    //
    // When offset is 0, adjust to MinFieldOffset (= sizeof(void*)) to skip past
    // the MethodTable pointer and reach the actual first field.

    if (fieldOffset < MinFieldOffset) {
        Log::Debug("ReadFieldReference: adjusting field offset from ", fieldOffset,
                   " to MinFieldOffset=", MinFieldOffset);
        fieldOffset = MinFieldOffset;
    }

    // Guard: field value (a pointer-sized reference) must fit within the object
    if (static_cast<SIZE_T>(fieldOffset) + sizeof(uintptr_t) > objectSize) {
        Log::Debug("ReadFieldReference: field at offset ", fieldOffset,
                   " + ", sizeof(uintptr_t), " exceeds object size ", objectSize);
        return 0;
    }

    // Read the reference stored at the field offset.
    uintptr_t* pField = reinterpret_cast<uintptr_t*>(objectAddress + fieldOffset);
    return *pField;
}
