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
#include "IService.h"
#include "LiveObjectInfo.h"
#include "Sample.h"

class IManagedThreadList;
class IFrameStore;
class IThreadsCpuManager;
class IAppDomainStore;
class IRuntimeIdStore;
class IConfiguration;
class ISampledAllocationsListener;

class LiveObjectsProvider : public IService,
                            public IBatchedSamplesProvider,
                            public ISampledAllocationsListener,
                            public IGarbageCollectionsListener
{
public:
    static std::vector<SampleValueType> SampleTypeDefinitions;

public:
    LiveObjectsProvider(
        uint32_t valueOffset,
        ICorProfilerInfo13* pCorProfilerInfo,
        IManagedThreadList* pManagedThreadList,
        IFrameStore* pFrameStore,
        IThreadsCpuManager* pThreadsCpuManager,
        IAppDomainStore* pAppDomainStore,
        IRuntimeIdStore* pRuntimeIdStore,
        IConfiguration* pConfiguration,
        MetricsRegistry& metricsRegistry);

public:
    // Inherited via IService
    bool Start() override;
    bool Stop() override;

    // Inherited via IBatchedSamplesProvider
    std::list<std::shared_ptr<Sample>> GetSamples() override;
    const char* GetName() override;

    // Inherited via ISampledAllocationsListener
    void OnAllocation(RawAllocationSample& rawSample) override;

    // Inherited via IGarbageCollectionsListener
    void OnGarbageCollectionStart(
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
        uint64_t endTimestamp) override;

private:
    ObjectHandleID CreateWeakHandle(uintptr_t address) const;
    void CloseWeakHandle(ObjectHandleID handle) const;
    bool IsAlive(ObjectHandleID handle) const;

private:
    uint32_t _valueOffset = 0;
    ICorProfilerInfo13* _pCorProfilerInfo = nullptr;
    IFrameStore* _pFrameStore = nullptr;
    IAppDomainStore* _pAppDomainStore = nullptr;
    IRuntimeIdStore* _pRuntimeIdStore = nullptr;
    IThreadsCpuManager* _pThreadsCpuManager = nullptr;
    std::unique_ptr<AllocationsProvider> _pAllocationsProvider;

    bool _isTimestampsAsLabelEnabled = false;

    std::mutex _liveObjectsLock;
    std::list<LiveObjectInfo> _monitoredObjects;
    // WeakHandle are checked after each GC

    static const std::string Gen1;
    static const std::string Gen2;
};
