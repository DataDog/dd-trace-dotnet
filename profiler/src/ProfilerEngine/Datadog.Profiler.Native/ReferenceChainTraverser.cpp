// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ReferenceChainTraverser.h"
#include "IFrameStore.h"
#include "Log.h"
#include "MemoryFaultGuard.h"
#include "OpSysTools.h"

#include <cstdint>
#include <stdexcept>
#include <utility>

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

template <typename TBody>
bool ReferenceChainTraverser::RunGuarded(TBody&& body)
{
    MemoryFaultGuard::RunResult result = MemoryFaultGuard::Run(std::forward<TBody>(body));
    if (result == MemoryFaultGuard::RunResult::Completed)
    {
        return true;
    }

    if (result == MemoryFaultGuard::RunResult::Faulted)
    {
        OnTraversalFault();
    }
    else
    {
        OnFaultGuardUnavailable();
    }

    return false;
}

void ReferenceChainTraverser::TraverseFromSingleRoot(const RootInfo& root)
{
    // The guards below only recover from memory access faults. Anything else -- a
    // std::bad_alloc from the tree or the visited set, a CLR exception surfacing
    // through one of the profiling API calls -- would otherwise escape into the
    // EventPipe callback driving the dump. Stop this dump's traversal instead.
    try
    {
        TraverseFromSingleRootCore(root);
    }
    catch (...)
    {
        OnTraversalAborted();
    }

    // Reported from here rather than from inside the guard: see LogPendingSelfTestFailure.
    LogPendingSelfTestFailure();
}

void ReferenceChainTraverser::TraverseFromSingleRootCore(const RootInfo& root)
{
    // If the GCDesc reader failed its self-test, skip all GCDesc-based traversal
    // (permanent). If faults have exhausted the per-dump budget, skip the rest of
    // this dump (transient). The class histogram does not depend on this path.
    if (!_gcDescTrusted || _stopReason != TraversalStopReason::None)
    {
        return;
    }

    auto startTime = OpSysTools::GetHighPrecisionTimestamp();

    // Seeding reads the root's MethodTable, so it too runs under the fault guard.
    // A fault while seeding means we cannot even start this root -- skip it.
    if (!SeedRootGuarded(root))
    {
        _totalTraversalDuration += OpSysTools::GetHighPrecisionTimestamp() - startTime;
        return;
    }

    // Resume loop: each guarded drain returns either because the stack was fully
    // consumed or because a memory access fault interrupted it. On a fault the
    // faulting frame has already been popped (the drain pops before it scans), so
    // re-entering picks up at the next unscanned frame. We only lose the remaining
    // references of the single object that faulted, not the whole root subgraph.
    while (!_traversalStack.empty() &&
           _gcDescTrusted &&
           _stopReason == TraversalStopReason::None)
    {
        uint32_t faultsBeforeDrain = _faultCount;
        uint64_t objectsBeforeDrain = _objectsTraversed;

        DrainTraversalStackGuarded();

        // Progress guarantee. Each drain pops a frame BEFORE scanning it and bumps
        // _objectsTraversed right after the pop, so a faulting drain that scanned
        // anything has already removed the offending frame -- no action needed (using
        // stack size here would be wrong, since a drain also PUSHES children before it
        // faults). Only if a fault somehow occurred without popping any frame do we
        // drop one so the loop cannot spin. The fault budget is the ultimate backstop.
        if (_faultCount != faultsBeforeDrain && _objectsTraversed == objectsBeforeDrain && !_traversalStack.empty())
        {
            _traversalStack.pop_back();
        }
    }

    if (_traversalStack.capacity() > _traversalStackHighWatermark)
    {
        _traversalStackHighWatermark = _traversalStack.capacity();
    }

    _rootsProcessed++;
    _totalTraversalDuration += OpSysTools::GetHighPrecisionTimestamp() - startTime;
}

bool ReferenceChainTraverser::SeedRootGuarded(const RootInfo& root)
{
    return RunGuarded([this, &root] { SeedRoot(root); });
}

void ReferenceChainTraverser::SeedRoot(const RootInfo& root)
{
    _rootCategoryCounts[static_cast<int>(root.category)]++;

    TypeTreeNode* rootNode = _tree.AddRoot(root.classID, root.category, root.objectSize, root.fieldName);

    _visited.Clear();
    _traversalStack.clear();
    _traversalStack.reserve(_traversalStackHighWatermark);

    _visited.MarkVisitedAndStore(root.address, root.classID);
    PushTraversalFrameIfScannable(root.address, rootNode, 1, root.classID, root.objectSize);
}

void ReferenceChainTraverser::DrainTraversalStackGuarded()
{
    RunGuarded([this] { DrainTraversalStack(); });
}

void ReferenceChainTraverser::OnTraversalFault()
{
    // A memory access fault is a data-level event: one address happened to be
    // unreadable. It says nothing about whether the GCDesc/MethodTable layout
    // model is correct, so it must NOT touch _gcDescTrusted / _selfTest. Those
    // are only tripped by the layout self-test.
    _faultCount++;
    if (_faultCount >= MaxFaultsPerDump)
    {
        _stopReason = TraversalStopReason::FaultBudgetExhausted;
    }

    // The interrupted insert may have written an address into the visited set
    // without recording its bucket in _dirtyIndices, so the next Clear() must
    // wipe the whole table instead of only the tracked buckets.
    _visited.MarkPossiblyInconsistent();

    LogOnce(Warn,
            "Reference-chain traversal hit a memory access fault while reading object graph memory. "
            "The faulting object is skipped and traversal continues. "
            "See the per-dump fault count in the traversal statistics.");
}

void ReferenceChainTraverser::OnFaultGuardUnavailable()
{
    _stopReason = TraversalStopReason::FaultGuardUnavailable;
    _visited.MarkPossiblyInconsistent();

    LogOnce(Error,
            "Reference-chain traversal cannot start because memory fault recovery is unavailable. "
            "Traversal is skipped for the rest of this heap snapshot. "
            "The class histogram is unaffected.");
}

void ReferenceChainTraverser::OnTraversalAborted()
{
    // Something threw rather than faulted, so retrying makes no sense: stop traversing
    // for the rest of this dump. As with a fault, this says nothing about whether the
    // GCDesc/MethodTable layout model is correct, so _gcDescTrusted stays untouched.
    _stopReason = TraversalStopReason::UnexpectedException;

    // The traversal was interrupted at an arbitrary point, so the visited set may hold
    // an address whose bucket was never recorded in _dirtyIndices.
    _visited.MarkPossiblyInconsistent();

    LogOnce(Warn,
            "Reference-chain traversal was aborted by an unexpected exception. "
            "Traversal is skipped for the rest of this heap snapshot. "
            "The class histogram is unaffected.");
}

void ReferenceChainTraverser::LogPendingSelfTestFailure()
{
    if (_selfTestFailedClassID == 0 || _selfTestFailureLogged)
    {
        return;
    }

    _selfTestFailureLogged = true;

    Log::Warn("GCDesc reference-chain self-test failed for class ", GetClassName(_selfTestFailedClassID),
              " (classID=", _selfTestFailedClassID, "): the CLR MethodTable/GCDesc layout does not match expectations. ",
              "Disabling reference-chain traversal for the rest of the process. ",
              "The class histogram is unaffected.");
}

#ifdef DD_TEST
void ReferenceChainTraverser::Test_FaultReadUnderGuard(const volatile void* ptr)
{
    if (!_gcDescTrusted || _stopReason != TraversalStopReason::None)
    {
        return;
    }

    RunGuarded([ptr] { (void)*reinterpret_cast<const volatile char*>(ptr); });
}

void ReferenceChainTraverser::Test_ThrowUnderGuard()
{
    RunGuarded([] { throw std::runtime_error("Test_ThrowUnderGuard"); });
}
#endif

void ReferenceChainTraverser::LogStats() const
{
    // avoid calculation overhead when debug logging is not enabled
    if (!Log::IsDebugEnabled())
    {
        return;
    }

    auto durationMs = std::chrono::duration_cast<std::chrono::milliseconds>(_totalTraversalDuration).count();

    const char* stopDescription = "";
    switch (_stopReason)
    {
        case TraversalStopReason::FaultBudgetExhausted:
            stopDescription = " (fault budget exhausted; traversal aborted for this dump)";
            break;
        case TraversalStopReason::UnexpectedException:
            stopDescription = " (unexpected exception; traversal aborted for this dump)";
            break;
        case TraversalStopReason::FaultGuardUnavailable:
            stopDescription = " (memory fault recovery unavailable; traversal aborted for this dump)";
            break;
        case TraversalStopReason::None:
            break;
    }

    Log::Debug("Reference chain traversal completed in ", durationMs, "ms: ",
              _rootsProcessed, " roots, ",
              _objectsTraversed, " objects traversed, ",
              "stack high watermark: ", _traversalStackHighWatermark, ", ",
              "memory access faults: ", _faultCount,
              stopDescription);

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

void ReferenceChainTraverser::DrainTraversalStack()
{
    // Accepted residual risk: this runs under the fault guard and mutates the tree
    // via TypeTreeNode::GetOrCreateChild (which allocates unordered_map nodes and can
    // rehash). A fault landing mid-rehash could in theory leave the tree inconsistent.
    // In practice faults come from raw object/MethodTable reads (GCDesc slots,
    // GetClassFromObject), never from our own allocator, so this is not observed. The
    // airtight follow-up would be a harvest-then-process split (read slots under the
    // guard into a pre-sized buffer, mutate the tree outside it).
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

        // Run the GCDesc self-test on the first few scannable objects only. This
        // validates the raw MethodTable/GCDesc layout against profiling-API
        // metadata so we degrade gracefully instead of dereferencing garbage if a
        // future runtime ever changes the layout. It is never run per object.
        if (_selfTest == GCDesc::SelfTestResult::Pending && _selfTestObjectsChecked < MaxSelfTestObjects)
        {
            _selfTestObjectsChecked++;
            GCDesc::SelfTestResult result = GCDesc::ValidateAgainstMetadata(_pCorProfilerInfo, classID, objectSize);
            if (result == GCDesc::SelfTestResult::Failed)
            {
                _gcDescTrusted = false;
                _selfTest = GCDesc::SelfTestResult::Failed;

                // Only record the class here. Resolving its name takes the FrameStore
                // lock and logging takes the logger lock; a fault while either is held
                // would leave it held for good, because neither siglongjmp nor SEH
                // unwinding runs destructors. LogPendingSelfTestFailure reports it from
                // outside the guard.
                _selfTestFailedClassID = classID;
                return;
            }
            else if (result == GCDesc::SelfTestResult::Passed)
            {
                _selfTest = GCDesc::SelfTestResult::Passed;
            }
            // Pending: inconclusive on this object; try the next scannable one.
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
        // A type met for the first time is only known from the next snapshot on: it cannot be
        // inspected from here (see InlineVTCache::ResolvePendingTypes).
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

    // Stack-allocate for rank 1; use reusable members for multi-dimensional so no
    // vector destructor runs on this fault-guarded path (a fault unwinds without
    // running C++ destructors). resize() may allocate, but that happens before any
    // object-graph memory is read, and the members outlive a longjmp.
    ULONG32 dimSize1;
    int dimBound1;
    ULONG32* dimensionSizes;
    int* dimensionLowerBounds;
    if (rank == 1)
    {
        dimensionSizes = &dimSize1;
        dimensionLowerBounds = &dimBound1;
    }
    else
    {
        _dimSizesScratch.resize(rank);
        _dimBoundsScratch.resize(rank);
        dimensionSizes = _dimSizesScratch.data();
        dimensionLowerBounds = _dimBoundsScratch.data();
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
        ULONG32 dim = dimensionSizes[d];
        // A real array cannot have more elements than fit in memory; an overflow
        // here means the dimension sizes are corrupt, so refuse to enumerate
        // rather than walking arbitrary memory based on a wrapped count.
        if (dim != 0 && totalElements > UINT64_MAX / dim)
        {
            return;
        }
        totalElements *= dim;
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
        ClassID vtClassID = field.classID;
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
    for (const auto& field : vtInfo.fields)
    {
        ULONG vtStart = baseOffset + field.offset;
        if (vtStart < baseOffset)
        {
            continue;
        }

        if (refOffset < vtStart || refOffset - vtStart >= field.size)
        {
            continue;
        }

        TypeTreeNode* vtNode = currentNode->GetOrCreateChild(field.classID);

        const InlineVTCache::InlineVTInfo* nestedInfo = _inlineVTCache.GetInlineVTInfo(field.classID);
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

bool ReferenceChainTraverser::IsValidObjectAddress(uintptr_t address)
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
