// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <atomic>
#include <chrono>

#include "CallstackProvider.h"
#include "CollectorBase.h"
#include "CounterMetric.h"
#include "GenericSampler.h"
#include "GroupSampler.h"
#include "IContentionListener.h"
#include "IUpscaleProvider.h"
#include "ManagedThreadInfo.h"
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
class RawSampleTransformer;
class SampleValueTypeProvider;


class ContentionProvider : public CollectorBase<RawContentionSample>,
                           public IContentionListener,
                           public IUpscaleProvider
{
public:
    ContentionProvider(
        SampleValueTypeProvider& valueTypeProvider,
        ICorProfilerInfo4* pCorProfilerInfo,
        IManagedThreadList* pManagedThreadList,
        RawSampleTransformer* rawSampleTransformer,
        IConfiguration* pConfiguration,
        MetricsRegistry& metricsRegistry,
        CallstackProvider callstackProvider,
        shared::pmr::memory_resource* memoryResource);

    // IContentionListener implementation
    void OnContention(std::chrono::nanoseconds contentionDuration) override;
    void OnContention(
        std::chrono::nanoseconds timestamp,
        uint32_t threadId,
        std::chrono::nanoseconds contentionDuration,
        const std::vector<uintptr_t>& stack) override;

    void SetBlockingThread(uint64_t osThreadId) override;
    void OnWaitStart(std::chrono::nanoseconds timestamp, uintptr_t associatedObjectId) override;
    void OnWaitStop(std::chrono::nanoseconds timestamp) override;

    // IUpscaleProvider implementation
    std::list<UpscalingInfo> GetInfos() override;

private:
    static std::string GetBucket(std::chrono::nanoseconds contentionDuration);
    static std::vector<SampleValueType> SampleTypeDefinitions;
    void AddContentionSample(std::chrono::nanoseconds timestamp, uint32_t threadId, ContentionType contentionType, std::chrono::nanoseconds contentionDuration, uint64_t blockingThreadId, shared::WSTRING blockingThreadName, const std::vector<uintptr_t>& stack);

private:
    static std::vector<uintptr_t> _emptyStack;

    ICorProfilerInfo4* _pCorProfilerInfo;
    IManagedThreadList* _pManagedThreadList;
    GroupSampler<std::string> _samplerLock;
    GroupSampler<std::string> _samplerWait;
    int32_t _sampleLimit;
    IConfiguration const* const _pConfiguration;
    std::shared_ptr<CounterMetric> _lockContentionsCountMetric;
    std::shared_ptr<MeanMaxMetric> _lockContentionsDurationMetric;
    std::shared_ptr<CounterMetric> _sampledLockContentionsCountMetric;
    std::shared_ptr<MeanMaxMetric> _sampledLockContentionsDurationMetric;
    std::mutex _contentionsLock;
    MetricsRegistry& _metricsRegistry;
    CallstackProvider _callstackProvider;
};
