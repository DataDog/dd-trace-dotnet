// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "CollectorBase.h"

#include "IContentionListener.h"
#include "RawContentionSample.h"
#include "GenericSampler.h"
#include "GroupSampler.h"
#include "MetricsRegistry.h"
#include "CounterMetric.h"
#include "MeanMaxMetric.h"

class IConfiguration;
class IManagedThreadList;
class IFrameStore;
class IThreadsCpuManager;
class IAppDomainStore;
class IRuntimeIdStore;


class ContentionProvider : public CollectorBase<RawContentionSample>, public IContentionListener
{
public:
    static std::vector<SampleValueType> SampleTypeDefinitions;

public:
    ContentionProvider(
        uint32_t valueOffset,
        ICorProfilerInfo4* pCorProfilerInfo,
        IManagedThreadList* pManagedThreadList,
        IFrameStore* pFrameStore,
        IThreadsCpuManager* pThreadsCpuManager,
        IAppDomainStore* pAppDomainStore,
        IRuntimeIdStore* pRuntimeIdStore,
        IConfiguration* pConfiguration,
        MetricsRegistry& metricsRegistry);

    void OnContention(double contentionDurationNs) override;

private:
    static std::string GetBucket(double contentionDurationNs);

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
};
