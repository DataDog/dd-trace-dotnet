// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "HeapSnapshotManager.h"
#include "Log.h"

HeapSnapshotManager::HeapSnapshotManager(
    IConfiguration* pConfiguration,
    ICorProfilerInfo12* pCorProfilerInfo,
    IFrameStore* pFrameStore) :
    ServiceBase(),
    _inducedGCNumber(-1),
    _session(0),
    _gen2Size(0),
    _lohSize(0),
    _pohSize(0),
    _memPressure(0),
    _isHeapDumpInProgress(false),
    _pCorProfilerInfo{pCorProfilerInfo},
    _pFrameStore{pFrameStore}
{
    _heapDumpInterval = pConfiguration->GetHeapSnapshotInterval();
    _memPressureThreshold = pConfiguration->GetHeapSnapshotUsedMemoryThreshold();
}

bool HeapSnapshotManager::StartImpl()
{
    // TODO: decide how to trigger a heap snapshot (timer + memory pressure threshold + growing gen2+loh)
    return true;
}

bool HeapSnapshotManager::StopImpl()
{
    // don't forget to close the current EventPipe session if any
    if (_session != 0)
    {

    }

    return true;
}

std::string HeapSnapshotManager::GetHeapSnapshotText()
{
    return std::string{};
}

void HeapSnapshotManager::OnGarbageCollectionStart(
    std::chrono::nanoseconds timestamp,
    int32_t number,
    uint32_t generation,
    GCReason reason,
    GCType type)
{
    if ((_session != 0) && (_inducedGCNumber != -1))
    {
        // waiting for the first induced foregrouned gen2 collection
        if ((reason == GCReason::Induced) && (generation == 2) && (type == GCType::NonConcurrentGC))
        {
            _inducedGCNumber = number;
        }
    }
}

void HeapSnapshotManager::OnGarbageCollectionEnd(
    int32_t number,
    uint32_t generation,
    GCReason reason,
    GCType type,
    bool isCompacting,
    std::chrono::nanoseconds pauseDuration,
    std::chrono::nanoseconds totalDuration, // from start to end (includes pauses)
    std::chrono::nanoseconds endTimestamp,  // end of GC
    uint64_t gen2Size,
    uint64_t lohSize,
    uint64_t pohSize,
    uint32_t memPressure)
{
    if (_session != 0)
    {
        if (number == _inducedGCNumber)
        {
            // the induced GC triggered to generate the heap snapshot has ended
            _inducedGCNumber = -1;

            StopGCDump();

            // TODO: restart the timer before the next heap snapshot
        }
    }

    // store sizes for next heap snapshot
    _gen2Size = gen2Size;
    _lohSize = lohSize;
    _pohSize = pohSize;
    _memPressure = memPressure;
}

void HeapSnapshotManager::StartGCDump()
{
    if (_session != 0)
    {
        // TODO: log a message and probably stop the current session
        return;
    }

    // reset the class histogram
    _classHistogram.clear();

    // creating an EventPipe session with the right keywords/verbosity on the .NET profider triggers a GC heap dump
    // i.e. an induced GC will be started and specific BulkXXX events will be emitted while dumping the surviving objects in the managed heap
    // Read https://chnasarre.medium.com/net-gcdump-internals-fcce5d327be7?source=friends_link&sk=3225ff119458adafc0e6935951fcc323 for more details
    //
    // no need to add TypeKeyword or GCHeapAndTypeNamesKeyword because ICorProfilerInfo allows us
    // to directly get the name of the types
    UINT64 activatedKeywords = 0x900000;    // GCHeapDumpKeyword and ManagedHeapCollectKeyword

    uint32_t verbosity = 5; // verbose verbosity
    COR_PRF_EVENTPIPE_PROVIDER_CONFIG providers[1] = 
    {
        COR_PRF_EVENTPIPE_PROVIDER_CONFIG{
            WStr("Microsoft-Windows-DotNETRuntime"),
            activatedKeywords,
            verbosity,
            nullptr},
    };

    // TODO: maybe this is sort of synchronous so we won't get the session before some events might be received
    _isHeapDumpInProgress = true;
    auto hr =_pCorProfilerInfo->EventPipeStartSession(1, providers, false, &_session);
    if (FAILED(hr))
    {
        _session = 0;
        _isHeapDumpInProgress = false;
        Log::Error("Failed to start event pipe session with hr=0x", std::hex, hr, std::dec, " for heap snapshot.");
    }
}

void HeapSnapshotManager::StopGCDump()
{
    if (_session == 0)
    {
        return;
    }

    _pCorProfilerInfo->EventPipeStopSession(_session);
    _isHeapDumpInProgress = false;
    _session = 0;
}
