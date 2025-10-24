// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IHeapSnapshotManager.h"
#include "IConfiguration.h"
#include "IFrameStore.h"
#include "IGarbageCollectionsListener.h"
#include "IGCDumpListener.h"
#include "ServiceBase.h"

#include "corprof.h"

#include <unordered_map>

class ClassHistogramEntry
{
public:
    ClassHistogramEntry(std::string& className)
        : 
        InstanceCount(0),
        TotalSize(0),
        ClassName(className)
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
        IFrameStore* pFrameStore);
    ~HeapSnapshotManager() override = default;

protected:
    // inherited via IService
    const char* GetName() override
    {
        return "HeapSnapshotManager";
    }

    // Inherited via IHeapSnapshotManager
    std::string GetHeapSnapshotText() override;

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
        uint32_t Index,
        uint32_t Count,
        GCBulkNodeValue* pNodes) override;
    void OnBulkEdges(
        uint32_t Index,
        uint32_t Count,
        GCBulkEdgeValue* pEdges) override;

    // Inherited via ServiceBase
    bool StartImpl() override;
    bool StopImpl() override;

private:
    void StartGCDump();
    void StopGCDump();

private:
    std::chrono::minutes _heapDumpInterval;
    int32_t _memPressureThreshold;
    uint64_t _gen2Size;
    uint64_t _lohSize;
    uint64_t _pohSize;
    uint32_t _memPressure;

    ICorProfilerInfo12* _pCorProfilerInfo;
    IFrameStore* _pFrameStore;

    // session used to trigger a heap dump
    // TODO: check if we need to synchronize the update of this field from different threads
    EVENTPIPE_SESSION _session;
    bool _isHeapDumpInProgress;

    // id of the induced GC triggering a heap dump
    int32_t _inducedGCNumber;

    // keep track of each type instances count and size during heap snapshot
    std::unordered_map<ClassID, ClassHistogramEntry> _classHistogram;
};
