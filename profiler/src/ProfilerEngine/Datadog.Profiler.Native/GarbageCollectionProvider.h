// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CollectorBase.h"

#include "IGarbageCollectionsListener.h"
#include "RawGarbageCollectionSample.h"
#include "MetricsRegistry.h"
#include "CounterMetric.h"
#include "MeanMaxMetric.h"

class SampleValueTypeProvider;

class GarbageCollectionProvider
    : public CollectorBase<RawGarbageCollectionSample>,
      public IGarbageCollectionsListener
{
public:
    GarbageCollectionProvider(
        SampleValueTypeProvider& valueTypeProvider,
        IFrameStore* pFrameStore,
        IThreadsCpuManager* pThreadsCpuManager,
        IAppDomainStore* pAppDomainStore,
        IRuntimeIdStore* pRuntimeIdStore,
        IConfiguration* pConfiguration,
        MetricsRegistry& metricsRegistry);

    // Inherited via IGarbageCollectionsListener
    void OnGarbageCollectionStart(
        int32_t number,
        uint32_t generation,
        GCReason reason,
        GCType type
        ) override;

    void OnGarbageCollectionEnd(
        int32_t number,
        uint32_t generation,
        GCReason reason,
        GCType type,
        bool isCompacting,
        uint64_t pauseDuration,
        uint64_t totalDuration, // from start to end (includes pauses)
        uint64_t endTimestamp   // end of GC
        ) override;

private:
    std::shared_ptr<CounterMetric> _gen0CountMetric;
    std::shared_ptr<CounterMetric> _gen1CountMetric;
    std::shared_ptr<CounterMetric> _gen2CountMetric;
    std::shared_ptr<MeanMaxMetric> _suspensionDurationMetric;
    std::shared_ptr<CounterMetric> _inducedCountMetric;
    std::shared_ptr<CounterMetric> _compactingGen2CountMetric;
    std::shared_ptr<CounterMetric> _memoryPressureCountMetric;
};
