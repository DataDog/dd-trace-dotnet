// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <list>
#include <mutex>

#include "corprof.h"

#include "AllocationsProvider.h"
#include "IBatchedSamplesProvider.h"
#include "IGarbageCollectionsListener.h"
#include "ISampledAllocationsListener.h"
#include "LiveObjectInfo.h"
#include "Sample.h"
#include "ServiceBase.h"

class IManagedThreadList;
class IFrameStore;
class IThreadsCpuManager;
class IAppDomainStore;
class IRuntimeIdStore;
class IConfiguration;
class ISampledAllocationsListener;
class SampleValueTypeProvider;

class LiveObjectsProvider : public ServiceBase,
                            public IBatchedSamplesProvider,
                            public ISampledAllocationsListener,
                            public IGarbageCollectionsListener
{
public:
    LiveObjectsProvider(
        SampleValueTypeProvider& valueTypeProvider,
        ICorProfilerInfo13* pCorProfilerInfo,
        IManagedThreadList* pManagedThreadList,
        IFrameStore* pFrameStore,
        IThreadsCpuManager* pThreadsCpuManager,
        IAppDomainStore* pAppDomainStore,
        IRuntimeIdStore* pRuntimeIdStore,
        IConfiguration* pConfiguration,
        MetricsRegistry& metricsRegistry);

public:

    // Inherited via IBatchedSamplesProvider
    std::unique_ptr<SamplesEnumerator> GetSamples() override;

    const char* GetName() override;

    // Inherited via ISampledAllocationsListener
    void OnAllocation(RawAllocationSample& rawSample) override;

    // Inherited via IGarbageCollectionsListener
    void OnGarbageCollectionStart(
        uint64_t timestamp,
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
        uint64_t pauseDuration,
        uint64_t totalDuration,
        uint64_t endTimestamp,
        uint64_t gen2Size,
        uint64_t lohSize,
        uint64_t pohSize) override;

private:
    ObjectHandleID CreateWeakHandle(uintptr_t address) const;
    void CloseWeakHandle(ObjectHandleID handle) const;
    bool IsAlive(ObjectHandleID handle) const;

    // Inherited via ServiceBase
    bool StartImpl() override;
    bool StopImpl() override;

private:
    static std::vector<SampleValueType> SampleTypeDefinitions;

    ICorProfilerInfo13* _pCorProfilerInfo = nullptr;
    std::unique_ptr<AllocationsProvider> _pAllocationsProvider;

    bool _isTimestampsAsLabelEnabled = false;

    std::mutex _liveObjectsLock;
    std::list<LiveObjectInfo> _monitoredObjects;
    // WeakHandle are checked after each GC

    static const std::string Gen1;
    static const std::string Gen2;
};
