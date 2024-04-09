// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "GarbageCollectionProvider.h"

#include "SampleValueTypeProvider.h"
#include "TimelineSampleType.h"

GarbageCollectionProvider::GarbageCollectionProvider(
    SampleValueTypeProvider& valueTypeProvider,
    IFrameStore* pFrameStore,
    IThreadsCpuManager* pThreadsCpuManager,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore,
    IConfiguration* pConfiguration,
    MetricsRegistry& metricsRegistry)
    :
    CollectorBase<RawGarbageCollectionSample>("GarbageCollectorProvider", valueTypeProvider.GetOrRegister(TimelineSampleType::Definitions), pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore)
{
    _gen0CountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_gc_gen0");
    _gen1CountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_gc_gen1");
    _gen2CountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_gc_gen2");
    _suspensionDurationMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_gc_suspensions_duration");
    _inducedCountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_gc_induced");
    _compactingGen2CountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_gc_compacting_gen2");
    _memoryPressureCountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_gc_memory_pressure");
}

void GarbageCollectionProvider::OnGarbageCollectionEnd(
    int32_t number,
    uint32_t generation,
    GCReason reason,
    GCType type,
    bool isCompacting,
    uint64_t pauseDuration,
    uint64_t totalDuration, // from start to end (includes pauses)
    uint64_t endTimestamp   // end of GC
)
{
    _suspensionDurationMetric->Add((double_t)pauseDuration);
    if (generation == 0)
    {
        _gen0CountMetric->Incr();
    }
    else
    if (generation == 1)
    {
        _gen1CountMetric->Incr();
    }
    else
    if (generation == 2)
    {
        _gen2CountMetric->Incr();
    }

    // use Reason to figure out "manual" trigger and memory pressure
    //     manual: Induced, InducedNotForced, InducedCompacting     (GC.Collect or GC.Add/RemoveMemoryPressure)
    // low memory: InducedLowMemory                                 (more than the high pressure threshold = not enough memory in host)
    //
    if (
        (reason == GCReason::InducedCompacting) ||
        (reason == GCReason::InducedNotForced) ||
        (reason == GCReason::Induced)
        )
    {
        _inducedCountMetric->Incr();
    }
    else
    if (reason == GCReason::InducedLowMemory)
    {
        _memoryPressureCountMetric->Incr();
    }

    if ((generation == 2) && (isCompacting))
    {
        _compactingGen2CountMetric->Incr();
    }

    RawGarbageCollectionSample rawSample;
    rawSample.Timestamp = endTimestamp;
    rawSample.LocalRootSpanId = 0;
    rawSample.SpanId = 0;
    rawSample.AppDomainId = (AppDomainID) nullptr;
    rawSample.ThreadInfo = nullptr;

    rawSample.Number = number;
    rawSample.Generation = generation;
    rawSample.Duration = pauseDuration;
    rawSample.TotalDuration = totalDuration;
    rawSample.PauseDuration = pauseDuration;
    rawSample.Reason = reason;
    rawSample.Type = type;
    rawSample.IsCompacting = isCompacting;
    Add(std::move(rawSample));
}

void GarbageCollectionProvider::OnGarbageCollectionStart(
    uint64_t timestamp,
    int32_t number,
    uint32_t generation,
    GCReason reason,
    GCType type
    )
{
}
