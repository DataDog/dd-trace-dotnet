// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <thread>
#include <memory>
#include <unordered_map>
#include <chrono>

#include "IHeapSnapshotManager.h"
#include "IConfiguration.h"
#include "IFrameStore.h"
#include "IGarbageCollectionsListener.h"
#include "IGCDumpListener.h"
#include "ServiceBase.h"
#include "MetricsRegistry.h"
#include "ProxyMetric.h"

#include "corprof.h"

// forward declarations
class IThreadsCpuManager;
class INativeThreadList;

using namespace std::chrono_literals;


class ClassHistogramEntry
{
public:
    ClassHistogramEntry(std::string&& className)
        :
        InstanceCount(0),
        TotalSize(0),
        ClassName(std::move(className))
    {
    }

public:
    std::string ClassName;
    uint64_t InstanceCount;
    uint64_t TotalSize;
};

class HeapSnapshotManager
    :
    public IHeapSnapshotManager,
    public IGarbageCollectionsListener,
    public IGCDumpListener,
    public ServiceBase
{
public:
    HeapSnapshotManager(
        IConfiguration* pConfiguration,
        ICorProfilerInfo12* pProfilerInfo,
        IFrameStore* pFrameStore,
        IThreadsCpuManager* pThreadsCpuManager,
        MetricsRegistry& metricsRegistry,
        INativeThreadList* pNativeThreadList);

    // Inherited via IHeapSnapshotManager
    void SetRuntimeSessionParameters(uint64_t keywords, uint32_t verbosity) override;
    std::string GetAndClearHeapSnapshotText() override;

    // used for debugging purpose
    std::string GetHeapSnapshotText();

    ~HeapSnapshotManager();

protected:
    // inherited via IService
    const char* GetName() override
    {
        return "HeapSnapshotManager";
    }

    // Inherited via IGarbageCollectionsListener
    void OnGarbageCollectionStart(
        std::chrono::nanoseconds timestamp,
        int32_t number,
        uint32_t generation,
        GCReason reason,
        GCType type) override;
    void OnGarbageCollectionEnd(
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
        uint32_t memPressure) override;

    // inherited via IGCDumpListener
    void OnBulkNodes(
        uint32_t index,
        uint32_t count,
        GCBulkNodeValue* pNodes) override;
    void OnBulkEdges(
        uint32_t index,
        uint32_t count,
        GCBulkEdgeValue* pEdges) override;

    // Inherited via ServiceBase
    bool StartImpl() override;
    bool StopImpl() override;

private:
    void MainLoop();
    void MainLoopIteration();
    void StartGCDump();
    void OnEndGCDump();
    void CleanupSession();
    void StartAsyncSnapshotIfNeeded();

private:
    std::chrono::minutes _heapDumpInterval;
    std::chrono::milliseconds _snapshotCheckInterval;
    uint32_t _memPressureThreshold;
    uint64_t _runtimeSessionKeywords;
    uint32_t _runtimeSessionVerbosity;

    uint64_t _gen2Size;
    uint64_t _lohSize;
    uint64_t _pohSize;
    uint32_t _memPressure;

    // metrics related to the last heap snapshot
    uint64_t _objectCount;
    uint64_t _totalSize;
    uint64_t _duration;
    std::shared_ptr<ProxyMetric> _heapSnapshotDurationMetric;
    std::shared_ptr<ProxyMetric> _heapSnapshotObjectCountMetric;
    std::shared_ptr<ProxyMetric> _heapSnapshotTotalSizeMetric;

    ICorProfilerInfo12* _pCorProfilerInfo;
    IFrameStore* _pFrameStore;
    IThreadsCpuManager* _pThreadsCpuManager;
    INativeThreadList* _pNativeThreadList;

    std::unique_ptr<std::thread> _pLoopThread;
    DWORD _loopThreadOsId;
    volatile bool _shutdownRequested = false;

    // session used to trigger a heap dump
    // TODO: check if we need to synchronize the update of this field from different threads
    EVENTPIPE_SESSION _session;

    // set to true when a heap dump is requested by starting an EventPipe session
    std::atomic<bool> _isHeapDumpInProgress;

    // set to true when the criterias are met after a GC
    // --> will trigger a heap dump in the dedicated thread to avoid triggering a GC from the GC callback
    std::atomic<bool> _shouldStartHeapDump;

    // set to true after the heap dump GC ends
    // --> taken into account in the dedicated thread to avoid triggering a gc from the GC callback
    // see https://github.com/dotnet/runtime/issues/121462 for more details
    std::atomic<bool> _shouldCleanupHeapDumpSession;

    // id of the induced GC triggering a heap dump
    std::atomic<int32_t> _inducedGCNumber;

    // keep track of each type instances count and size during heap snapshot
    std::unordered_map<ClassID, ClassHistogramEntry> _classHistogram;
    std::recursive_mutex _histogramLock;

    std::chrono::nanoseconds _startTimestamp;

    // timestamp of the last heap snapshot
    std::chrono::nanoseconds _lastTimestamp;

    // TODO: see if we should also try to detect old heap size growth before triggering a heap snapshot
    uint64_t _lastOldHeapSize; // gen2 + loh + poh

    // TODO: see if we should also try to detect memory pressure growth before triggering a heap snapshot
    //       instead of only using the configured threshold
    uint64_t _lastMemPressure;
};
