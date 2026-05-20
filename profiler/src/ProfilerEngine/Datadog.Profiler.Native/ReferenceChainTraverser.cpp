// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ReferenceChainTraverser.h"
#include "Log.h"
#include "OpSysTools.h"

ReferenceChainTraverser::ReferenceChainTraverser(
    ICorProfilerInfo12* pCorProfilerInfo,
    IFrameStore* pFrameStore,
    TypeReferenceTree& tree,
    InlineVTCache& inlineVTCache,
    size_t visitedSetInitialCapacity)
    : _pCorProfilerInfo(pCorProfilerInfo),
      _pFrameStore(pFrameStore),
      _tree(tree),
      _inlineVTCache(inlineVTCache),
      _visited(visitedSetInitialCapacity),
      _objectsTraversed(0),
      _rootsProcessed(0)
{
}

void ReferenceChainTraverser::TraverseFromSingleRoot(const RootInfo& root)
{
    auto startTime = OpSysTools::GetHighPrecisionTimestamp();
    _rootCategoryCounts[static_cast<int>(root.category)]++;

    TypeTreeNode* rootNode = _tree.AddRoot(root.classID, root.category, root.objectSize, root.fieldName);

    _visited.Clear();

    TraverseObjectGraph(root.address, rootNode, 1, root.classID, root.objectSize);

    if (_traversalStack.capacity() > _traversalStackHighWatermark)
    {
        _traversalStackHighWatermark = _traversalStack.capacity();
    }

    _rootsProcessed++;
    _totalTraversalDuration += OpSysTools::GetHighPrecisionTimestamp() - startTime;
}

void ReferenceChainTraverser::LogStats() const
{
    auto durationMs = std::chrono::duration_cast<std::chrono::milliseconds>(_totalTraversalDuration).count();

    Log::Debug("Reference chain traversal completed in ", durationMs, "ms: ",
              _rootsProcessed, " roots, ",
              _objectsTraversed, " objects traversed, ",
              "stack high watermark: ", _traversalStackHighWatermark);

    Log::Debug("  VisitedObjectSet: ",
              _visited.Size(), " current / ",
              _visited.GetPeakEntryCount(), " peak entries, ",
              _visited.GetBucketCount(), " buckets, ",
              _visited.GetGrowCount(), " grows, ",
              _visited.GetMemorySize() / 1024, " KB total (",
              "addresses: ", _visited.GetAddressesMemorySize() / 1024, " KB, ",
              "entries: ", _visited.GetEntriesMemorySize() / 1024, " KB, ",
              "dirty: ", _visited.GetDirtyIndicesMemorySize() / 1024, " KB)");

    if constexpr (VisitedObjectSet::AreDetailedStatsEnabled())
    {
        size_t tryInsertCalls = _visited.GetTryInsertCalls();
        size_t tryInsertAverageProbesX100 = tryInsertCalls == 0 ? 0 : (_visited.GetTryInsertProbeCount() * 100) / tryInsertCalls;
        Log::Debug("  VisitedObjectSet TryInsert: ",
                  tryInsertCalls, " calls, ",
                  _visited.GetTryInsertInsertedCount(), " inserted, ",
                  _visited.GetTryInsertAlreadyPresentCount(), " already present, ",
                  _visited.GetTryInsertProbeCount(), " probes, avg ",
                  tryInsertAverageProbesX100 / 100, ".",
                  tryInsertAverageProbesX100 % 100, ", max ",
                  _visited.GetTryInsertMaxProbeCount());

        size_t markCalls = _visited.GetMarkVisitedAndStoreCalls();
        size_t markAverageProbesX100 = markCalls == 0 ? 0 : (_visited.GetMarkVisitedAndStoreProbeCount() * 100) / markCalls;
        Log::Debug("  VisitedObjectSet MarkVisitedAndStore: ",
                  markCalls, " calls, ",
                  _visited.GetMarkVisitedAndStoreInsertedCount(), " inserted, ",
                  _visited.GetMarkVisitedAndStoreAlreadyPresentCount(), " already present, ",
                  _visited.GetMarkVisitedAndStoreProbeCount(), " probes, avg ",
                  markAverageProbesX100 / 100, ".",
                  markAverageProbesX100 % 100, ", max ",
                  _visited.GetMarkVisitedAndStoreMaxProbeCount());
    }
    else
    {
        Log::Debug("  VisitedObjectSet detailed probe stats: disabled");
    }

    for (int i = 0; i < static_cast<int>(RootCategoryCount); i++)
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
    uint32_t depth,
    ClassID rootClassID,
    SIZE_T rootObjectSize)
{
    _traversalStack.clear();
    _traversalStack.reserve(_traversalStackHighWatermark);

    _visited.MarkVisitedAndStore(objectAddress, rootClassID);
    PushTraversalFrameIfScannable(objectAddress, currentNode, depth, rootClassID, rootObjectSize);

    while (!_traversalStack.empty())
    {
        auto frame = _traversalStack.back();
        _traversalStack.pop_back();

        if (frame.depth > MaxTreeDepth)
        {
            continue;
        }

        _objectsTraversed++;

        ClassID classID = frame.classID;
        SIZE_T objectSize = frame.objectSize;

        if (!GCDesc::ContainsGCPointers(classID))
        {
            continue;
        }

        ptrdiff_t seriesCount = GCDesc::GetSeriesCount(classID);
        if (seriesCount < 0)
        {
            EnqueueValueTypeArrayChildren(frame.objectAddress, classID, frame.treeNode, frame.depth);
            continue;
        }

        if (seriesCount == 0)
        {
            continue;
        }

        // FAST PATH: Read positive GCDesc series directly from the MethodTable.
        // This handles both regular objects and reference arrays, matching the GC scanner.
        // Check if this type has inline VTs (slow path needed for tree attribution).
        const InlineVTCache::InlineVTInfo* vtInfo = _inlineVTCache.GetInlineVTInfo(classID);

        if (vtInfo == nullptr)
        {
            // Common case: no inline VTs. All GCDesc refs belong to direct fields.
            GCDesc::EnumerateObjectRefs(classID, frame.objectAddress, objectSize,
                [&](const uintptr_t* /*slot*/, uintptr_t refAddr, ULONG /*offset*/)
                {
                    if (IsValidObjectAddress(refAddr))
                    {
                        ProcessDiscoveredRef(refAddr, frame.treeNode, frame.depth);
                    }
                });
        }
        else
        {
            // Rare case: type has inline VTs. Still enumerate refs from the parent
            // object's GCDesc, then use InlineVTCache only to attribute refs to
            // the deepest inline VT range that owns their offset.
            AddInlineValueTypeInstances(frame.treeNode, *vtInfo);
            GCDesc::EnumerateObjectRefs(classID, frame.objectAddress, objectSize,
                [&](const uintptr_t* /*slot*/, uintptr_t refAddr, ULONG offset)
                {
                    if (!IsValidObjectAddress(refAddr))
                    {
                        return;
                    }

                    InlineVTOwner owner = GetInlineValueTypeOwner(frame.treeNode, frame.depth, offset, *vtInfo);
                    ProcessDiscoveredRef(refAddr, owner.node, owner.depth);
                });
        }
    }
}

void ReferenceChainTraverser::EnqueueValueTypeArrayChildren(
    uintptr_t arrayAddress,
    ClassID arrayClassID,
    TypeTreeNode* currentNode,
    uint32_t depth)
{
    CorElementType elementType;
    ClassID elementClassID;
    ULONG rank = 0;
    HRESULT hr = _pCorProfilerInfo->IsArrayClass(arrayClassID, &elementType, &elementClassID, &rank);
    if (hr != S_OK || rank == 0 || elementType != ELEMENT_TYPE_VALUETYPE)
    {
        return;
    }

    if (elementClassID == 0 || !GCDesc::ContainsGCPointers(elementClassID))
    {
        return;
    }

    // Stack-allocate for rank 1; heap-allocate only for multi-dimensional.
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
    hr = _pCorProfilerInfo->GetArrayObjectInfo(
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

    GCDesc::EnumerateVTArrayRefs(arrayClassID, arrayAddress, totalElements,
        [&](const uintptr_t* /*slot*/, uintptr_t refAddr, ULONG /*offset*/)
        {
            if (refAddr == 0 || !IsValidObjectAddress(refAddr))
            {
                return;
            }

            VisitedObjectSet::VisitedEntry* slot = nullptr;
            if (_visited.TryInsert(refAddr, slot) == VisitedObjectSet::InsertResult::Inserted)
            {
                ClassID targetClassID = 0;
                HRESULT hr = _pCorProfilerInfo->GetClassFromObject(refAddr, &targetClassID);
                if (FAILED(hr) || targetClassID == 0)
                {
                    return;
                }

                SIZE_T targetSize = 0;
                hr = _pCorProfilerInfo->GetObjectSize2(refAddr, &targetSize);
                if (FAILED(hr) || targetSize == 0)
                {
                    return;
                }

                slot->classID = targetClassID;

                TypeTreeNode* childNode = currentNode->GetOrCreateChild(targetClassID);
                childNode->AddInstance(targetSize);
                PushTraversalFrameIfScannable(refAddr, childNode, depth + 1, targetClassID, targetSize);
            }
            else if (slot->classID != 0)
            {
                SIZE_T revisitSize = 0;
                _pCorProfilerInfo->GetObjectSize2(refAddr, &revisitSize);
                TypeTreeNode* childNode = currentNode->GetOrCreateChild(slot->classID);
                childNode->AddInstance(revisitSize);
            }
        });
}

void ReferenceChainTraverser::AddInlineValueTypeInstances(TypeTreeNode* currentNode, const InlineVTCache::InlineVTInfo& vtInfo)
{
    for (const auto& field : vtInfo.fields)
    {
        ClassID vtClassID = field.second;
        TypeTreeNode* vtNode = currentNode->GetOrCreateChild(vtClassID);
        vtNode->AddInstance(0);

        const InlineVTCache::InlineVTInfo* nestedInfo = _inlineVTCache.GetInlineVTInfo(vtClassID);
        if (nestedInfo != nullptr)
        {
            AddInlineValueTypeInstances(vtNode, *nestedInfo);
        }
    }
}

ReferenceChainTraverser::InlineVTOwner ReferenceChainTraverser::GetInlineValueTypeOwner(
    TypeTreeNode* currentNode,
    uint32_t depth,
    ULONG refOffset,
    const InlineVTCache::InlineVTInfo& vtInfo,
    ULONG baseOffset)
{
    for (const auto& [vtOffset, vtClassID] : vtInfo.fields)
    {
        ULONG vtStart = baseOffset + vtOffset;
        ULONG fieldCount = 0;
        ULONG vtSize = 0;
        HRESULT hr = _pCorProfilerInfo->GetClassLayout(vtClassID, nullptr, 0, &fieldCount, &vtSize);
        if (FAILED(hr) || vtSize == 0)
        {
            continue;
        }

        if (refOffset < vtStart || refOffset >= vtStart + vtSize)
        {
            continue;
        }

        TypeTreeNode* vtNode = currentNode->GetOrCreateChild(vtClassID);

        const InlineVTCache::InlineVTInfo* nestedInfo = _inlineVTCache.GetInlineVTInfo(vtClassID);
        if (nestedInfo != nullptr)
        {
            return GetInlineValueTypeOwner(vtNode, depth + 1, refOffset, *nestedInfo, vtStart);
        }

        return {vtNode, depth + 1};
    }

    return {currentNode, depth};
}

bool ReferenceChainTraverser::ProcessDiscoveredRef(uintptr_t refAddress, TypeTreeNode* parentNode, uint32_t depth)
{
    VisitedObjectSet::VisitedEntry* slot = nullptr;
    if (_visited.TryInsert(refAddress, slot) == VisitedObjectSet::InsertResult::Inserted)
    {
        ClassID targetClassID = 0;
        HRESULT hr = _pCorProfilerInfo->GetClassFromObject(refAddress, &targetClassID);
        if (FAILED(hr) || targetClassID == 0)
        {
            return false;
        }

        SIZE_T targetSize = 0;
        hr = _pCorProfilerInfo->GetObjectSize2(refAddress, &targetSize);
        if (FAILED(hr) || targetSize == 0)
        {
            return false;
        }

        slot->classID = targetClassID;

        TypeTreeNode* childNode = parentNode->GetOrCreateChild(targetClassID);
        childNode->AddInstance(targetSize);
        PushTraversalFrameIfScannable(refAddress, childNode, depth + 1, targetClassID, targetSize);
        return true;
    }

    if (slot->classID != 0)
    {
        SIZE_T revisitSize = 0;
        _pCorProfilerInfo->GetObjectSize2(refAddress, &revisitSize);
        TypeTreeNode* childNode = parentNode->GetOrCreateChild(slot->classID);
        childNode->AddInstance(revisitSize);
    }

    return false;
}

void ReferenceChainTraverser::PushTraversalFrameIfScannable(
    uintptr_t objectAddress,
    TypeTreeNode* treeNode,
    uint32_t depth,
    ClassID classID,
    SIZE_T objectSize)
{
    if (GCDesc::ContainsGCPointers(classID))
    {
        _traversalStack.push_back({objectAddress, treeNode, depth, classID, objectSize});
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
    if (address == 0 || address < 0x10000)
    {
        return false;
    }

    if ((address % sizeof(void*)) != 0)
    {
        return false;
    }

    return true;
}
