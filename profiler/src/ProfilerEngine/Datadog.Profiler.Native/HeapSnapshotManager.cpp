// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "HeapSnapshotManager.h"
#include "OpSysTools.h"
#include "ThreadsCpuManager.h"

#include "Log.h"

constexpr const WCHAR* ThreadName = WStr("DD_HeapSnapMgr");

HeapSnapshotManager::HeapSnapshotManager(
    IConfiguration* pConfiguration,
    ICorProfilerInfo12* pCorProfilerInfo,
    IFrameStore* pFrameStore,
    IThreadsCpuManager* pThreadsCpuManager) :
    ServiceBase(),
    _inducedGCNumber(-1),
    _session(0),
    _gen2Size(0),
    _lohSize(0),
    _pohSize(0),
    _memPressure(0),
    _isHeapDumpInProgress(false),
    _pCorProfilerInfo{pCorProfilerInfo},
    _pFrameStore{pFrameStore},
    _pThreadsCpuManager{pThreadsCpuManager},
    _lastTimestamp(0ns),
    _lastOldHeapSize(0),
    _lastMemPressure(0),
    _shouldStartHeapDump(false)
{
    _heapDumpInterval = pConfiguration->GetHeapSnapshotInterval();
    _memPressureThreshold = pConfiguration->GetHeapSnapshotMemoryPressureThreshold();
    _snapshotCheckInterval = pConfiguration->GetHeapSnapshotCheckInterval();

    _pCorProfilerInfo->AddRef();
}

HeapSnapshotManager::~HeapSnapshotManager()
{
    StopImpl();

    ICorProfilerInfo12* pCorProfilerInfo = _pCorProfilerInfo;
    if (pCorProfilerInfo != nullptr)
    {
        _pCorProfilerInfo = nullptr;
        pCorProfilerInfo->Release();
    }
}

bool HeapSnapshotManager::StartImpl()
{
    _pLoopThread = std::make_unique<std::thread>([this] {
        OpSysTools::SetNativeThreadName(ThreadName);
        MainLoop();
    });

    return true;
}

bool HeapSnapshotManager::StopImpl()
{
    _shutdownRequested = true;

    if (_pLoopThread != nullptr)
    {
        try
        {
            _pLoopThread->join();
            _pLoopThread.reset();
        }
        catch (const std::exception&)
        {
        }
    }

    // don't forget to close the current EventPipe session if any
    CleanupSession();

    return true;
}

void HeapSnapshotManager::CleanupSession()
{
    _isHeapDumpInProgress = false;
    if (_session != 0)
    {
        _pCorProfilerInfo->EventPipeStopSession(_session);
        _session = 0;
    }
}

void HeapSnapshotManager::MainLoop()
{
    Log::Debug("HeapSnapshotManager::MainLoop started.");

    _loopThreadOsId = OpSysTools::GetThreadId();
    _pThreadsCpuManager->Map(_loopThreadOsId, ThreadName);

    while (!_shutdownRequested)
    {
        try
        {
            OpSysTools::Sleep(_snapshotCheckInterval);
            MainLoopIteration();
        }
        catch (const std::runtime_error& re)
        {
            Log::Error("Runtime error in HeapSnapshotManager::MainLoop: ", re.what());
        }
        catch (const std::exception& ex)
        {
            Log::Error("Typed Exception in HeapSnapshotManager::MainLoop: ", ex.what());
        }
        catch (...)
        {
            Log::Error("Unknown Exception in HeapSnapshotManager::MainLoop.");
        }
    }

    Log::Debug("HeapSnapshotManager::MainLoop has ended.");
}

void HeapSnapshotManager::MainLoopIteration()
{
    if (_shouldStartHeapDump)
    {
        _shouldStartHeapDump = false;
        StartGCDump();
    }
}

std::string HeapSnapshotManager::GetHeapSnapshotText()
{
    std::stringstream ss;
    ss << "[" << std::endl;
    int current = 1;
    int last = static_cast<int>(_classHistogram.size());
    for (auto& [classID, entry] : _classHistogram)
    {
        ss << "[\"";
        ss << entry.ClassName << "\","
           << entry.InstanceCount << ","
           << entry.TotalSize;
        ss << "]";
        if (current < last)
        {
            ss << "," << std::endl;
        }
        else
        {
            ss << std::endl;
        }
    }
    ss << "]" << std::endl;

    return ss.str();
}

void HeapSnapshotManager::OnBulkNodes(
    uint32_t index,
    uint32_t count,
    GCBulkNodeValue* pNodes)
{
    for (size_t i = 0; i < count; i++)
    {
        // TODO: we should not be called from different threads so no need to lock
        auto entry = _classHistogram.find(pNodes[i].TypeID);
        if (entry == _classHistogram.end())
        {
            std::string className;
            if (_pFrameStore->GetTypeName(static_cast<ClassID>(pNodes[i].TypeID), className))
            {
                ClassHistogramEntry histogramEntry(className);
                histogramEntry.InstanceCount = 1;
                histogramEntry.TotalSize = pNodes[i].Size;
                _classHistogram.emplace(pNodes[i].TypeID, histogramEntry);
            }
            else // should never happen  :^(
            {
            }
        }
        else
        {
            entry->second.InstanceCount++;
            entry->second.TotalSize += pNodes[i].Size;
        }
    }
}

void HeapSnapshotManager::OnBulkEdges(
    uint32_t Index,
    uint32_t Count,
    GCBulkEdgeValue* pEdges)
{
    // TODO: use to rebuild the reference chain
}

void HeapSnapshotManager::OnGarbageCollectionStart(
    std::chrono::nanoseconds timestamp,
    int32_t number,
    uint32_t generation,
    GCReason reason,
    GCType type)
{
    // waiting for the first induced foregrouned gen2 collection
    if (_isHeapDumpInProgress && (_inducedGCNumber == -1))
    {
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
    if (_isHeapDumpInProgress)
    {
        if (number == _inducedGCNumber)
        {
            // the induced GC triggered to generate the heap snapshot has ended
            _inducedGCNumber = -1;

            // keep track of the last metrics
            _lastOldHeapSize = gen2Size + lohSize + pohSize;
            _lastMemPressure = memPressure;

            StopGCDump();
            _lastTimestamp = OpSysTools::GetHighPrecisionTimestamp();
        }
    }

    // store sizes for next heap snapshot
    _gen2Size = gen2Size;
    _lohSize = lohSize;
    _pohSize = pohSize;
    _memPressure = memPressure;

    StartSnapshotTimerIfNeeded();
}

// the heuristic to trigger a heap snapshot is the following:
//  - the memory pressure must be greater than the configured threshold
//  - wait for the configured interval since the previous one
void HeapSnapshotManager::StartSnapshotTimerIfNeeded()
{
    // DEBUG: we cannot start a gcdump: it breaks in the CLR because it is not allowed to start a GC during another GC

    // already set so no need to check again
    if (_shouldStartHeapDump)
    {
        return;
    }

    // Note: if the memory pressure is set to 0 in the configuration, a snapshot will be generated at each interval
    //       (used for testing purpose)
    if (_memPressureThreshold > 0)
    {
        if (_memPressure < _memPressureThreshold)
        {
            return;
        }
    }

    auto now = OpSysTools::GetHighPrecisionTimestamp();
    if (_lastTimestamp != 0ns)
    {
        auto durationSinceLastSnapshot = now - _lastTimestamp;
        if (durationSinceLastSnapshot < _heapDumpInterval)
        {
            return;
        }
    }

    _shouldStartHeapDump = true;
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

    // TODO: Maybe this is sort of synchronous so we won't get the session before some events might be received.
    //       It might also imply that _session won't be set when the induced GC ends; i.e. impossible to cleanup
    //       --> clean up in the loop or when the next snapshot is triggered
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

#ifdef NDEBUG
    // for debugging purpose only
#else
    // dump each entry in _classHistogram
    auto content = GetHeapSnapshotText();
    std::cout << content << std::endl;
#endif

    CleanupSession();
}
