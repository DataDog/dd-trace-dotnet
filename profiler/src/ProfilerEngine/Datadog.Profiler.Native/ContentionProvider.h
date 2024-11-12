// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <atomic>

#include "CallstackProvider.h"
#include "CollectorBase.h"
#include "CounterMetric.h"
#include "GenericSampler.h"
#include "GroupSampler.h"
#include "IContentionListener.h"
#include "IUpscaleProvider.h"
#include "MeanMaxMetric.h"
#include "MetricsRegistry.h"
#include "RawContentionSample.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

#include <memory>

class IConfiguration;
class IManagedThreadList;
class IFrameStore;
class IThreadsCpuManager;
class IAppDomainStore;
class IRuntimeIdStore;
class SampleValueTypeProvider;


class ContentionProvider :
    public CollectorBase<RawContentionSample>,
    public IContentionListener,
    public IUpscaleProvider
{
public:
    ContentionProvider(
        SampleValueTypeProvider& valueTypeProvider,
        ICorProfilerInfo4* pCorProfilerInfo,
        IManagedThreadList* pManagedThreadList,
        IFrameStore* pFrameStore,
        IThreadsCpuManager* pThreadsCpuManager,
        IAppDomainStore* pAppDomainStore,
        IRuntimeIdStore* pRuntimeIdStore,
        IConfiguration* pConfiguration,
        MetricsRegistry& metricsRegistry,
        CallstackProvider callstackProvider,
        shared::pmr::memory_resource* memoryResource);

    // IContentionListener implementation
    void OnContention(double contentionDurationNs) override;
    void OnContention(uint64_t timestamp, uint32_t threadId, double contentionDurationNs, const std::vector<uintptr_t>& stack) override;
    void SetBlockingThread(uint64_t osThreadId) override;

    // IUpscaleProvider implementation
    UpscalingInfo GetInfo() override;

private:
    static std::string GetBucket(double contentionDurationNs);
    static std::vector<SampleValueType> SampleTypeDefinitions;
    void AddContentionSample(uint64_t timestamp, uint32_t threadId, double contentionDurationNs, uint64_t blockingThreadId, shared::WSTRING blockingThreadName, const std::vector<uintptr_t>& stack);

private:
    static std::vector<uintptr_t> _emptyStack;

    ICorProfilerInfo4* _pCorProfilerInfo;
    IManagedThreadList* _pManagedThreadList;
    GroupSampler<std::string> _sampler;
    int32_t _contentionDurationThreshold;
    int32_t _sampleLimit;
    IConfiguration const* const _pConfiguration;
    std::shared_ptr<CounterMetric> _lockContentionsCountMetric;
    std::shared_ptr<MeanMaxMetric> _lockContentionsDurationMetric;
    std::shared_ptr<CounterMetric> _sampledLockContentionsCountMetric;
    std::shared_ptr<MeanMaxMetric> _sampledLockContentionsDurationMetric;
    std::mutex _contentionsLock;

    CallstackProvider _callstackProvider;
};
