// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ReferenceChainTraverser.h"
#include "Log.h"
#include "OpSysTools.h"

#include <cstdint>
#include <stdexcept>

#ifdef _WINDOWS
#include <Windows.h>

namespace
{
// Only the faults a raw object graph read can actually produce are recovered from.
// EXCEPTION_EXECUTE_HANDLER would also swallow stack overflow (leaving the guard page
// unreset for the rest of the process), CLR exceptions and C++ exceptions such as
// std::bad_alloc -- none of which mean "that object address was unreadable", and all
// of which the resume loop would otherwise retry.
int MemoryFaultFilter(DWORD exceptionCode)
{
    switch (exceptionCode)
    {
        case EXCEPTION_ACCESS_VIOLATION:
        case EXCEPTION_DATATYPE_MISALIGNMENT:
        case EXCEPTION_IN_PAGE_ERROR:
            return EXCEPTION_EXECUTE_HANDLER;

        default:
            return EXCEPTION_CONTINUE_SEARCH;
    }
}
} // namespace

#else
#include <csetjmp>
#include <csignal>
#include <pthread.h>

#include "ProfilerSignalManager.h"

// NOTE (macOS): macOS is not a supported profiler build today
// (profiler/src/CMakeLists.txt fails with "MACOS builds are not supported yet").
// If it is ever enabled, this guard needs a macOS path because ProfilerSignalManager
// lives in the Linux-only project and does not exist there. A macOS port would:
//   - install its own sigaction() for SIGSEGV and SIGBUS (saving the previous actions),
//   - in the handler, siglongjmp when t_inGuardedTraversal is set, otherwise manually
//     chain to the saved previous sa_sigaction/sa_handler (or restore SIG_DFL + re-raise
//     when there was none) so real faults keep their original crash semantics,
//   - register once (e.g. std::call_once) so re-creating the traverser does not save our
//     own handler as the "previous" one.
// The TLS recovery machinery (t_traversalJmpBuf / t_inGuardedTraversal / sigsetjmp in the
// wrapper) is portable and would be shared as-is.

namespace
{
thread_local sigjmp_buf t_traversalJmpBuf;
thread_local volatile sig_atomic_t t_inGuardedTraversal = 0;

// Clears the flag on every way out of the guarded body that still unwinds C++ frames:
// a normal return and, crucially, an escaping exception. Left set, the flag would
// arm the handler for the whole life of the thread, so the next SIGSEGV -- including
// one the CLR would have handled itself -- would siglongjmp into a dead frame.
// The siglongjmp path skips destructors, so the recovery branch clears it by hand.
struct InGuardedTraversalScope
{
    InGuardedTraversalScope()
    {
        t_inGuardedTraversal = 1;
    }

    ~InGuardedTraversalScope()
    {
        t_inGuardedTraversal = 0;
    }
};

// The guard is entered at least twice per root, and sigsetjmp with savemask = 1 costs
// an rt_sigprocmask syscall every time. Using savemask = 0 instead moves that cost to
// the (rare) recovery path: undo the handler's mask change here so a later fault is
// still deliverable. ProfilerSignalManager installs its handlers without SA_NODEFER
// and with an sa_mask holding only the handled signal, so unblocking SIGSEGV and
// SIGBUS restores exactly what was blocked. This runs after the handler frame has
// been abandoned, i.e. in normal context, so pthread_sigmask is safe to call.
void UnblockFaultSignals()
{
    sigset_t faultSignals;
    sigemptyset(&faultSignals);
    sigaddset(&faultSignals, SIGSEGV);
    sigaddset(&faultSignals, SIGBUS);
    pthread_sigmask(SIG_UNBLOCK, &faultSignals, nullptr);
}

// ProfilerSignalManager: return false to chain to the CLR's previous SIGSEGV/SIGBUS handler.
// When in guarded traversal we siglongjmp and do not return.
bool TraversalFaultHandler(int /*signal*/, siginfo_t* /*info*/, void* /*context*/)
{
    if (t_inGuardedTraversal != 0)
    {
        siglongjmp(t_traversalJmpBuf, 1);
    }
    return false;
}
} // namespace
#endif

ReferenceChainTraverser::ReferenceChainTraverser(
    ICorProfilerInfo12* pCorProfilerInfo,
    IFrameStore* pFrameStore,
    TypeReferenceTree& tree,
    InlineVTCache& inlineVTCache,
    size_t visitedSetInitialCapacity,
    const GCHeapRangeSet* pHeapRanges)
    : _pCorProfilerInfo(pCorProfilerInfo),
      _pFrameStore(pFrameStore),
      _tree(tree),
      _inlineVTCache(inlineVTCache),
      _pHeapRanges(pHeapRanges),
      _visited(visitedSetInitialCapacity),
      _objectsTraversed(0),
      _rootsProcessed(0)
{
#ifndef _WINDOWS
    auto* segv = ProfilerSignalManager::Get(SIGSEGV);
    if (segv != nullptr)
    {
        segv->RegisterHandler(&TraversalFaultHandler);
    }
    auto* bus = ProfilerSignalManager::Get(SIGBUS);
    if (bus != nullptr)
    {
        bus->RegisterHandler(&TraversalFaultHandler);
    }
#endif
}

template <typename TBody>
bool ReferenceChainTraverser::RunGuarded(TBody&& body)
{
    // IMPORTANT: this function must own no local object requiring C++ unwinding, and
    // neither must body or anything it calls. On Windows /EHsc, SEH unwinding does not
    // run the destructors of intervening frames; on Linux, siglongjmp does not run
    // destructors at all. (InGuardedTraversalScope below is the one exception: it is
    // there precisely to cover the paths that DO unwind, and the recovery branch
    // reproduces its effect for the path that does not.)
#ifdef _WINDOWS
    __try
    {
        body();
    }
    __except (MemoryFaultFilter(GetExceptionCode()))
    {
        OnTraversalFault();
        return false;
    }
#else
    if (sigsetjmp(t_traversalJmpBuf, 0) != 0)
    {
        UnblockFaultSignals();
        t_inGuardedTraversal = 0;
        OnTraversalFault();
        return false;
    }

    InGuardedTraversalScope guardScope;
    body();
#endif

    return true;
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
    if (!_gcDescTrusted || _faultBudgetExhausted)
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
    while (!_traversalStack.empty() && _gcDescTrusted && !_faultBudgetExhausted)
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
        _faultBudgetExhausted = true;
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

void ReferenceChainTraverser::OnTraversalAborted()
{
    // Something threw rather than faulted, so retrying makes no sense: stop traversing
    // for the rest of this dump. As with a fault, this says nothing about whether the
    // GCDesc/MethodTable layout model is correct, so _gcDescTrusted stays untouched.
    _faultBudgetExhausted = true;

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
    if (!_gcDescTrusted || _faultBudgetExhausted)
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

    Log::Debug("Reference chain traversal completed in ", durationMs, "ms: ",
              _rootsProcessed, " roots, ",
              _objectsTraversed, " objects traversed, ",
              "stack high watermark: ", _traversalStackHighWatermark, ", ",
              "memory access faults: ", _faultCount,
              (_faultBudgetExhausted ? " (fault budget exhausted; traversal aborted for this dump)" : ""),
              ", refs rejected by heap-range filter: ", _refsRejectedByRangeFilter);

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

    // Coarse GC-heap plausibility check. The set is empty (accepts everything) when
    // the bounds could not be captured, so this never becomes a hard gate. A false
    // reject silently drops a real edge, so this stays intentionally permissive.
    if (_pHeapRanges != nullptr && !_pHeapRanges->Contains(address))
    {
        _refsRejectedByRangeFilter++;
        return false;
    }

    return true;
}
