// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "HeapSnapshotManager.h"
#include "INativeThreadList.h"
#include "OpSysTools.h"
#include "ThreadsCpuManager.h"

#include "Log.h"

constexpr const WCHAR* ThreadName = WStr("DD_HeapSnapMgr");

HeapSnapshotManager::HeapSnapshotManager(
    IConfiguration* pConfiguration,
    ICorProfilerInfo12* pCorProfilerInfo,
    IFrameStore* pFrameStore,
    IThreadsCpuManager* pThreadsCpuManager,
    MetricsRegistry& metricsRegistry,
    INativeThreadList* pNativeThreadList) :
    ServiceBase(),
    _session(0),
    _gen2Size(0),
    _lohSize(0),
    _pohSize(0),
    _memPressure(0),
    _pCorProfilerInfo{pCorProfilerInfo},
    _pFrameStore{pFrameStore},
    _pThreadsCpuManager{pThreadsCpuManager},
    _pNativeThreadList{pNativeThreadList},
    _runtimeSessionKeywords(0),
    _runtimeSessionVerbosity(0),
    _startTimestamp(0ns),
    _lastTimestamp(0ns),
    _lastOldHeapSize(0),
    _lastMemPressure(0),
    _loopThreadOsId(0),
    _objectCount(0),
    _totalSize(0),
    _duration(0)
{
    _isHeapDumpInProgress.store(false),
    _inducedGCNumber.store(-1);
    _shouldStartHeapDump.store(false);
    _shouldCleanupHeapDumpSession.store(false);
    _heapDumpInterval = pConfiguration->GetHeapSnapshotInterval();
    _memPressureThreshold = pConfiguration->GetHeapSnapshotMemoryPressureThreshold();
    _snapshotCheckInterval = pConfiguration->GetHeapSnapshotCheckInterval();

    _heapSnapshotDurationMetric = metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_heapsnapshot_duration", [this]() {
        return static_cast<double>(_duration);
    });

    _heapSnapshotObjectCountMetric = metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_heapsnapshot_object_count", [this]() {
        return static_cast<double>(_objectCount);
    });

    _heapSnapshotTotalSizeMetric = metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_heapsnapshot_total_size", [this]() {
        return static_cast<double>(_totalSize);
    });


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

void HeapSnapshotManager::SetRuntimeSessionParameters(uint64_t keywords, uint32_t verbosity)
{
    _runtimeSessionKeywords = keywords;
    _runtimeSessionVerbosity = verbosity;
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

    // NOTE: don't cleanup the session - should be done in the dedicated thread

    return true;
}

void HeapSnapshotManager::MainLoop()
{
    Log::Debug("HeapSnapshotManager::MainLoop started.");

    _loopThreadOsId = OpSysTools::GetThreadId();
    _pThreadsCpuManager->Map(_loopThreadOsId, ThreadName);
    _pNativeThreadList->RegisterThread(_loopThreadOsId);

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
    if (_shouldCleanupHeapDumpSession.load())
    {
        // close the session + start/stop the fake session to reset the keywords/verbosity
        _shouldCleanupHeapDumpSession.store(false);
        CleanupSession();
    }
    else
    if (_shouldStartHeapDump.load())
    {
        _shouldStartHeapDump.store(false);
        StartGCDump();
    }
}

std::string HeapSnapshotManager::GetAndClearHeapSnapshotText()
{
    // this should be protected by a lock because both the dedicated thread and the exporter thread
    // could call this method at the same time
    // --> We could otherwise create the string when the heap snapshot ends.
    std::lock_guard lock(_histogramLock);

    std::string heapSnapshotText = GetHeapSnapshotText();
    _classHistogram.clear();

    return heapSnapshotText;
}

// NOTE: must be called under the lock
std::string HeapSnapshotManager::GetHeapSnapshotText()
{
    auto count = _classHistogram.size();
    if (count == 0)
    {
        return "";
    }

    std::stringstream ss;
    ss << "[" << std::endl;
    int current = 1;
    int last = static_cast<int>(count);
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
        current++;
    }
    ss << "]" << std::endl;

    return ss.str();
}

void HeapSnapshotManager::OnBulkNodes(
    uint32_t index,
    uint32_t count,
    GCBulkNodeValue* pNodes)
{
#ifndef NDEBUG
    // for debugging purpose only
    std::cout << "OnBulkNodes #" << index << " x" << count  << std::endl;
#endif

    std::lock_guard lock(_histogramLock);

    _objectCount += count;
    for (size_t i = 0; i < count; i++)
    {
        auto size = pNodes[i].Size;
        _totalSize += size;

        auto entry = _classHistogram.find(pNodes[i].TypeID);
        if (entry == _classHistogram.end())
        {
            std::string className;
            if (_pFrameStore->GetTypeName(static_cast<ClassID>(pNodes[i].TypeID), className))
            {
                ClassHistogramEntry histogramEntry(std::move(className));
                histogramEntry.InstanceCount = 1;
                histogramEntry.TotalSize = size;
                _classHistogram.emplace(pNodes[i].TypeID, histogramEntry);
            }
            else // should never happen  :^(
            {
            }
        }
        else
        {
            entry->second.InstanceCount++;
            entry->second.TotalSize += size;
        }
    }
}

void HeapSnapshotManager::OnBulkEdges(
    uint32_t index,
    uint32_t count,
    GCBulkEdgeValue* pEdges)
{
#ifndef NDEBUG
    // for debugging purpose only
    std::cout << "OnBulkEdges #" << index << " x" << count << std::endl;
#endif

    // TODO: should be used to rebuild the reference chain. For more details,
    //       the array of edges is strongly related to the array of nodes received in OnBulkNodes.
    // read https://chnasarre.medium.com/net-gcdump-internals-fcce5d327be7?source=friends_link&sk=3225ff119458adafc0e6935951fcc323
}

void HeapSnapshotManager::OnGarbageCollectionStart(
    std::chrono::nanoseconds timestamp,
    int32_t number,
    uint32_t generation,
    GCReason reason,
    GCType type)
{
    // waiting for the first induced foreground gen2 collection corresponding to the one
    // triggered by the session creation
    if (_isHeapDumpInProgress.load() && (_inducedGCNumber.load() == -1))
    {
        if ((reason == GCReason::Induced) && (generation == 2) && (type == GCType::NonConcurrentGC))
        {
#ifndef NDEBUG
            // for debugging purpose only
            std::cout << "OnGarbageCollectionStart" << std::endl;
#endif

            _inducedGCNumber.store(number);
            _objectCount = 0;
            _totalSize = 0;
            _duration = 0;
            _startTimestamp = OpSysTools::GetHighPrecisionTimestamp();
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
    // TODO: store sizes to decide whether or nor a heap snapshot should start
    _gen2Size = gen2Size;
    _lohSize = lohSize;
    _pohSize = pohSize;
    _memPressure = memPressure;

    if (_isHeapDumpInProgress.load())
    {
        if (number == _inducedGCNumber.load())
        {
#ifndef NDEBUG
            // for debugging purpose only
            std::cout << "OnGarbageCollectionEnd" << std::endl;
#endif

            // the induced GC triggered to generate the heap snapshot has ended
            _inducedGCNumber.store(-1);
            _isHeapDumpInProgress.store(false);

            // keep track of the last metrics
            _lastOldHeapSize = gen2Size + lohSize + pohSize;
            _lastMemPressure = memPressure;

            _lastTimestamp = OpSysTools::GetHighPrecisionTimestamp();
            _duration = static_cast<uint64_t>((_lastTimestamp - _startTimestamp).count() / 1000000);

            OnEndGCDump();

            // NOTE: don't reset object count and duration so it can be used by metrics
            //       --> will be reset when the collection starts
        }
    }
    else
    {
        StartAsyncSnapshotIfNeeded();
    }
}

// the heuristic to trigger a heap snapshot is the following:
//  - the memory pressure must be greater than the configured threshold
//  - wait for the configured interval since the previous one
void HeapSnapshotManager::StartAsyncSnapshotIfNeeded()
{
    // DEBUG: we cannot start a gcdump: it breaks in the CLR because it is not allowed to start a GC during another GC

    // already set so no need to check again
    if (_shouldStartHeapDump.load() || _shouldCleanupHeapDumpSession.load())
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
    if (_lastTimestamp == 0ns)
    {
        // for tests purposes, we start the first snapshot right away
        if (_memPressureThreshold == 0)
        {

            // wait at least _heapDumpInterval after the first snapshot
            _lastTimestamp = now;

            _shouldStartHeapDump.store(true);
            return;
        }
    }
    else
    {
        auto durationSinceLastSnapshot = now - _lastTimestamp;
        if (durationSinceLastSnapshot < _heapDumpInterval)
        {
            return;
        }
        _shouldStartHeapDump.store(true);
    }
}

void HeapSnapshotManager::StartGCDump()
{
    if (_session != 0)
    {
        // TODO: log a message and probably stop the current session
        return;
    }

    // reset the class histogram
    {
        std::lock_guard lock(_histogramLock);
        _classHistogram.clear();
    }

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

    // Maybe this is sort of synchronous so we won't get the session before some events might be received.
    // It might also imply that _session won't be set when the induced GC ends; i.e. impossible to cleanup
    // --> clean up in the loop or when the next snapshot is triggered
    _isHeapDumpInProgress.store(true);
    auto hr =_pCorProfilerInfo->EventPipeStartSession(1, providers, false, &_session);
    if (FAILED(hr))
    {
        _session = 0;
        _isHeapDumpInProgress.store(false);
        Log::Error("Failed to start event pipe session with hr=0x", std::hex, hr, std::dec, " for heap snapshot.");
    }
}

void HeapSnapshotManager::OnEndGCDump()
{
#ifndef NDEBUG
    // for debugging purpose only
    std::cout << _objectCount << " objects for " << _totalSize / (1024 * 1024) << " MB during " << _duration << "ms" << std::endl
              << std::endl;

//    {
//        // dump each entry in _classHistogram
//        std::lock_guard lock(_histogramLock);
//        auto content = GetHeapSnapshotText();
//        std::cout << content << std::endl;
//    }
#endif

    // DEBUG: we cannot stop here the session + start/stop a fake one to reset the keywords/verbosity
    //        because it could deadlock the GC
    _shouldCleanupHeapDumpSession.store(true);
}

void HeapSnapshotManager::CleanupSession()
{
    if (_session != 0)
    {
        _pCorProfilerInfo->EventPipeStopSession(_session);
        _session = 0;
    }
    else
    {
        // TODO: could this happen if the dedicated thread is scheduled BEFORE the GC End callback returns?
    }

    // Before the fix of https://github.com/dotnet/runtime/issues/121462, it is needed to start
    // and stop a session JUST to reset the keywords/verbosity of the Microsoft-Windows-DotNETRuntime provider
    if (_runtimeSessionKeywords != 0)
    {
        COR_PRF_EVENTPIPE_PROVIDER_CONFIG providers[] = {
            {WStr("Microsoft-Windows-DotNETRuntime"),
             _runtimeSessionKeywords,
             _runtimeSessionVerbosity,
             nullptr}};
        EVENTPIPE_SESSION tempSession = 0;
        HRESULT hr = _pCorProfilerInfo->EventPipeStartSession(
            1,
            providers,
            false,
            &tempSession);
        if (SUCCEEDED(hr) && (tempSession != 0))
        {
            _pCorProfilerInfo->EventPipeStopSession(tempSession);
        }
    }
}
