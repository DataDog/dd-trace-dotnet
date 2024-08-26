// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CallstackProvider.h"
#include "CollectorBase.h"
#include "CounterMetric.h"
#include "GenericSampler.h"
#include "IAllocationsListener.h"
#include "MeanMaxMetric.h"
#include "MetricsRegistry.h"
#include "RawAllocationSample.h"
#include "SumMetric.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

#include <memory>

class IConfiguration;
class IManagedThreadList;
class IFrameStore;
class IThreadsCpuManager;
class IAppDomainStore;
class IRuntimeIdStore;
class ISampledAllocationsListener;
class SampleValueTypeProvider;


class AllocationsProvider
    :
    public CollectorBase<RawAllocationSample>,
    public IAllocationsListener
{
public:
    AllocationsProvider(
        bool isFramework,
        SampleValueTypeProvider& valueTypeProvider,
        ICorProfilerInfo4* pCorProfilerInfo,
        IManagedThreadList* pManagedThreadList,
        IFrameStore* pFrameStore,
        IThreadsCpuManager* pThreadsCpuManager,
        IAppDomainStore* pAppDomainStore,
        IRuntimeIdStore* pRuntimeIdStore,
        IConfiguration* pConfiguration,
        ISampledAllocationsListener* pListener,
        MetricsRegistry& metricsRegistry,
        CallstackProvider callstackProvider,
        shared::pmr::memory_resource* memoryResource);

    AllocationsProvider(
        std::vector<SampleValueTypeProvider::Offset> valueTypeProvider,
        ICorProfilerInfo4* pCorProfilerInfo,
        IManagedThreadList* pManagedThreadList,
        IFrameStore* pFrameStore,
        IThreadsCpuManager* pThreadsCpuManager,
        IAppDomainStore* pAppDomainStore,
        IRuntimeIdStore* pRuntimeIdStore,
        IConfiguration* pConfiguration,
        ISampledAllocationsListener* pListener,
        MetricsRegistry& metricsRegistry,
        CallstackProvider callstackProvider,
        shared::pmr::memory_resource* memoryResource);

    void OnAllocation(uint32_t allocationKind,
                      ClassID classId,
                      const WCHAR* typeName,
                      uintptr_t address,
                      uint64_t objectSize,
                      uint64_t allocationAmount) override;

    void OnAllocation(uint64_t timestamp,
                      uint32_t threadId,
                      uint32_t allocationKind,
                      ClassID classId,
                      const std::string& typeName,
                      uint64_t allocationAmount,
                      const std::vector<uintptr_t>& stack) override;

private:
    static std::vector<SampleValueType> SampleTypeDefinitions;
    static std::vector<SampleValueType> FrameworkSampleTypeDefinitions;

    ICorProfilerInfo4* _pCorProfilerInfo;
    IManagedThreadList* _pManagedThreadList;
    IFrameStore* _pFrameStore;
    ISampledAllocationsListener* _pListener = nullptr;
    GenericSampler _sampler;
    int32_t _sampleLimit;
    IConfiguration const* const _pConfiguration;
    bool _shouldSubSample;
    std::shared_ptr<CounterMetric> _allocationsCountMetric;
    std::shared_ptr<MeanMaxMetric> _allocationsSizeMetric;
    std::shared_ptr<CounterMetric> _sampledAllocationsCountMetric;
    std::shared_ptr<MeanMaxMetric> _sampledAllocationsSizeMetric;
    std::shared_ptr<SumMetric> _totalAllocationsSizeMetric;
    CallstackProvider _callstackProvider;
};
