// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ReferenceChainTraverser.h"
#include "Log.h"
#include "OpSysTools.h"

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
    TypeReferenceTree& tree,
    ClassLayoutCache& layoutCache)
    : _pCorProfilerInfo(pCorProfilerInfo),
      _pFrameStore(pFrameStore),
      _tree(tree),
      _layoutCache(layoutCache),
      _objectsTraversed(0),
      _rootsProcessed(0)
{
}

void ReferenceChainTraverser::TraverseFromSingleRoot(const RootInfo& root)
{
    auto startTime = OpSysTools::GetHighPrecisionTimestamp();
    _rootCategoryCounts[static_cast<int>(root.category)]++;

    // Add the root to the tree and get the tree node to navigate from
    TypeTreeNode* rootNode = _tree.AddRoot(root.classID, root.category, root.objectSize, root.fieldName);

    // Clear visited set for this root: detects cycles within this root's graph
    // while allowing the same object to be reached from different roots.
    // Reusing the member set avoids reallocating the bucket array for each root.
    _visited.Clear();

    // Traverse the object graph from this root iteratively.
    // rootNode is the tree position; children will be added under it.
    // Depth starts at 1 (the root itself).
    TraverseObjectGraph(root.address, rootNode, 1);

    _rootsProcessed++;
    _totalTraversalDuration += OpSysTools::GetHighPrecisionTimestamp() - startTime;
}

void ReferenceChainTraverser::LogStats() const
{
    auto durationMs = std::chrono::duration_cast<std::chrono::milliseconds>(_totalTraversalDuration).count();

    Log::Debug("Reference chain traversal completed in ", durationMs, "ms: ",
              _rootsProcessed, " roots, ",
              _objectsTraversed, " objects traversed");

    for (int i = 0; i <= static_cast<int>(RootCategory::Unknown); i++)
    {
        auto cat = static_cast<RootCategory>(i);
        if (_rootCategoryCounts[i] > 0)
        {
            Log::Debug("  ", RootCategoryToString(cat), " roots: ", _rootCategoryCounts[i]);
        }
    }
}

void ReferenceChainTraverser::TraverseObjectGraph(
    uintptr_t objectAddress,
    TypeTreeNode* currentNode,
    uint32_t depth)
{
    _traversalStack.clear();
    _traversalStack.push_back({objectAddress, currentNode, depth});

    while (!_traversalStack.empty())
    {
        auto frame = _traversalStack.back();
        _traversalStack.pop_back();

        if (_visited.IsVisited(frame.objectAddress))
        {
            continue;
        }

        if (frame.depth > MaxTreeDepth)
        {
            continue;
        }

        _visited.MarkVisited(frame.objectAddress);
        _objectsTraversed++;

        ClassID classID = 0;
        HRESULT hr = _pCorProfilerInfo->GetClassFromObject(frame.objectAddress, &classID);
        if (FAILED(hr) || classID == 0)
        {
            continue;
        }

        SIZE_T objectSize = 0;
        hr = _pCorProfilerInfo->GetObjectSize2(frame.objectAddress, &objectSize);
        if (FAILED(hr) || objectSize == 0)
        {
            continue;
        }

        const ClassLayoutCache::ClassLayoutData* layout = _layoutCache.GetLayout(classID);
        if (layout == nullptr)
        {
            continue;
        }

        if (layout->isArray)
        {
            EnqueueArrayChildren(frame.objectAddress, classID, *layout, frame.treeNode, frame.depth);
            continue;
        }

        for (const auto& field : layout->fields)
        {
            if (!field.isReferenceType)
            {
                continue;
            }

            uintptr_t fieldValue = ReadFieldReference(frame.objectAddress, field.offset, objectSize);
            if (fieldValue == 0 || !IsValidObjectAddress(fieldValue))
            {
                continue;
            }

            // Early visited check avoids unnecessary CLR calls and stack push.
            // The authoritative check is at the top of the loop after popping.
            if (_visited.IsVisited(fieldValue))
            {
                continue;
            }

            ClassID targetClassID = 0;
            hr = _pCorProfilerInfo->GetClassFromObject(fieldValue, &targetClassID);
            if (FAILED(hr) || targetClassID == 0)
            {
                continue;
            }

            SIZE_T targetSize = 0;
            _pCorProfilerInfo->GetObjectSize2(fieldValue, &targetSize);

            TypeTreeNode* childNode = frame.treeNode->GetOrCreateChild(targetClassID);
            childNode->AddInstance(targetSize);

            _traversalStack.push_back({fieldValue, childNode, frame.depth + 1});
        }
    }
}

void ReferenceChainTraverser::EnqueueArrayChildren(
    uintptr_t arrayAddress,
    ClassID arrayClassID,
    const ClassLayoutCache::ClassLayoutData& layout,
    TypeTreeNode* currentNode,
    uint32_t depth)
{
    bool isReferenceTypeArray =
        layout.arrayElementType == ELEMENT_TYPE_CLASS ||
        layout.arrayElementType == ELEMENT_TYPE_STRING ||
        layout.arrayElementType == ELEMENT_TYPE_OBJECT ||
        layout.arrayElementType == ELEMENT_TYPE_SZARRAY ||
        layout.arrayElementType == ELEMENT_TYPE_ARRAY;

    bool isValueTypeArray = (layout.arrayElementType == ELEMENT_TYPE_VALUETYPE);

    if (!isReferenceTypeArray && !isValueTypeArray)
    {
        return;
    }

    const ClassLayoutCache::ClassLayoutData* elementLayout = nullptr;
    if (isValueTypeArray)
    {
        elementLayout = _layoutCache.GetLayout(layout.arrayElementClassID);
        if (elementLayout == nullptr || elementLayout->classSize == 0)
        {
            return;
        }

        bool hasReferenceFields = false;
        for (const auto& field : elementLayout->fields)
        {
            if (field.isReferenceType)
            {
                hasReferenceFields = true;
                break;
            }
        }

        if (!hasReferenceFields)
        {
            return;
        }
    }

    ULONG32 rank = layout.arrayRank;
    if (rank == 0)
    {
        return;
    }

    // Stack-allocate for rank 1 (99%+ of .NET arrays); heap-allocate only for multi-dimensional.
    ULONG32 dimSize1;
    int dimBound1;
    ULONG32* dimensionSizes;
    int* dimensionLowerBounds;
    std::vector<ULONG32> dimSizesVec;
    std::vector<int> dimBoundsVec;
    if (rank == 1)
    {
        dimensionSizes = &dimSize1;
        dimensionLowerBounds = &dimBound1;
    }
    else
    {
        dimSizesVec.resize(rank);
        dimBoundsVec.resize(rank);
        dimensionSizes = dimSizesVec.data();
        dimensionLowerBounds = dimBoundsVec.data();
    }

    BYTE* pData = nullptr;

    HRESULT hr = _pCorProfilerInfo->GetArrayObjectInfo(
        static_cast<ObjectID>(arrayAddress),
        rank,
        dimensionSizes,
        dimensionLowerBounds,
        &pData);

    if (FAILED(hr) || pData == nullptr)
    {
        return;
    }

    uint64_t totalElements = 1;
    for (ULONG32 d = 0; d < rank; d++)
    {
        totalElements *= dimensionSizes[d];
    }

    if (totalElements == 0)
    {
        return;
    }

    if (isValueTypeArray)
    {
        EnqueueValueTypeArrayChildren(pData, totalElements, *elementLayout, currentNode, depth);
        return;
    }

    // Reference type array: each element is a pointer-sized reference.
    uintptr_t* pElements = reinterpret_cast<uintptr_t*>(pData);

    for (uint64_t i = 0; i < totalElements; i++)
    {
        uintptr_t elementAddress = pElements[i];

        if (elementAddress == 0 || !IsValidObjectAddress(elementAddress))
        {
            continue;
        }

        if (_visited.IsVisited(elementAddress))
        {
            continue;
        }

        ClassID elementClassID = 0;
        hr = _pCorProfilerInfo->GetClassFromObject(elementAddress, &elementClassID);
        if (FAILED(hr) || elementClassID == 0)
        {
            continue;
        }

        SIZE_T elementSizeBytes = 0;
        _pCorProfilerInfo->GetObjectSize2(elementAddress, &elementSizeBytes);

        TypeTreeNode* childNode = currentNode->GetOrCreateChild(elementClassID);
        childNode->AddInstance(elementSizeBytes);

        _traversalStack.push_back({elementAddress, childNode, depth + 1});
    }
}

void ReferenceChainTraverser::EnqueueValueTypeArrayChildren(
    BYTE* pData,
    uint64_t totalElements,
    const ClassLayoutCache::ClassLayoutData& elementLayout,
    TypeTreeNode* currentNode,
    uint32_t depth)
{
    ULONG elementSize = elementLayout.classSize;

    for (uint64_t i = 0; i < totalElements; i++)
    {
        uintptr_t elementBase = reinterpret_cast<uintptr_t>(pData) + i * elementSize;

        for (const auto& field : elementLayout.fields)
        {
            if (!field.isReferenceType)
            {
                continue;
            }

            if (static_cast<SIZE_T>(field.offset) + sizeof(uintptr_t) > elementSize)
            {
                continue;
            }

            uintptr_t* pField = reinterpret_cast<uintptr_t*>(elementBase + field.offset);
            uintptr_t fieldValue = *pField;

            if (fieldValue == 0 || !IsValidObjectAddress(fieldValue))
            {
                continue;
            }

            if (_visited.IsVisited(fieldValue))
            {
                continue;
            }

            ClassID targetClassID = 0;
            HRESULT hr = _pCorProfilerInfo->GetClassFromObject(fieldValue, &targetClassID);
            if (FAILED(hr) || targetClassID == 0)
            {
                continue;
            }

            SIZE_T targetSize = 0;
            _pCorProfilerInfo->GetObjectSize2(fieldValue, &targetSize);

            TypeTreeNode* childNode = currentNode->GetOrCreateChild(targetClassID);
            childNode->AddInstance(targetSize);

            _traversalStack.push_back({fieldValue, childNode, depth + 1});
        }
    }
}

std::string ReferenceChainTraverser::GetClassName(ClassID classID) const
{
    std::string name;
    if (_pFrameStore != nullptr && _pFrameStore->GetTypeName(classID, name))
    {
        return name;
    }
    return "<classID=" + std::to_string(classID) + ">";
}

bool ReferenceChainTraverser::IsValidObjectAddress(uintptr_t address) const
{
    // Basic validation - check if address is in reasonable range
    if (address == 0 || address < 0x10000)
    {
        return false;  // Null or too small
    }

    // Check alignment (objects are typically pointer-aligned)
    if ((address % sizeof(void*)) != 0)
    {
        return false;
    }

    return true;
}

uintptr_t ReferenceChainTraverser::ReadFieldReference(uintptr_t objectAddress, ULONG fieldOffset, SIZE_T objectSize) const
{
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

    if (fieldOffset < MinFieldOffset)
    {
        fieldOffset = MinFieldOffset;
    }

    // Guard: field value (a pointer-sized reference) must fit within the object
    if (static_cast<SIZE_T>(fieldOffset) + sizeof(uintptr_t) > objectSize)
    {
        return 0;
    }

    // Read the reference stored at the field offset.
    uintptr_t* pField = reinterpret_cast<uintptr_t*>(objectAddress + fieldOffset);
    return *pField;
}
