// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <list>
#include <mutex>

#include "corprof.h"

#include "IBatchedSamplesProvider.h"
#include "IGarbageCollectionsListener.h"
#include "ISampledAllocationsListener.h"
#include "LiveObjectInfo.h"
#include "Sample.h"
#include "ServiceBase.h"

class IManagedThreadList;
class IConfiguration;
class ISampledAllocationsListener;
class RawSampleTransformer;
class SampleValueTypeProvider;

class LiveObjectsProvider : public ServiceBase,
                            public IBatchedSamplesProvider,
                            public ISampledAllocationsListener,
                            public IGarbageCollectionsListener
{
public:
    LiveObjectsProvider(
        ICorProfilerInfo13* pCorProfilerInfo,
        SampleValueTypeProvider& valueTypeProvider,
        RawSampleTransformer* rawSampleTransformer,
        IConfiguration* pConfiguration);

public:

    static std::vector<SampleValueType> SampleTypeDefinitions;

    // Inherited via IBatchedSamplesProvider
    std::unique_ptr<SamplesEnumerator> GetSamples() override;

    const char* GetName() override;

    // Inherited via ISampledAllocationsListener
    void OnAllocation(RawAllocationSample& rawSample) override;

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
        std::chrono::nanoseconds totalDuration,
        std::chrono::nanoseconds endTimestamp,
        uint64_t gen2Size,
        uint64_t lohSize,
        uint64_t pohSize,
        uint32_t memPressure) override;

private:
    ObjectHandleID CreateWeakHandle(uintptr_t address) const;
    void CloseWeakHandle(ObjectHandleID handle) const;
    bool IsAlive(ObjectHandleID handle) const;

    // Inherited via ServiceBase
    bool StartImpl() override;
    bool StopImpl() override;

private:
    uint32_t _heapHandleLimit;

     // used to access the CLR to create weak handles
     // and get object generation
    ICorProfilerInfo13* _pCorProfilerInfo = nullptr;
    RawSampleTransformer* _rawSampleTransformer = nullptr;

    std::mutex _liveObjectsLock;
    std::list<LiveObjectInfo> _monitoredObjects;
    // WeakHandle are checked after each GC
    std::vector<SampleValueTypeProvider::Offset> _valueOffsets;

    static const std::string Gen1;
    static const std::string Gen2;
};
