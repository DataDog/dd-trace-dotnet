// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "GarbageCollectionProvider.h"

#include "RawSampleTransformer.h"
#include "SampleValueTypeProvider.h"
#include "TimelineSampleType.h"

GarbageCollectionProvider::GarbageCollectionProvider(
    SampleValueTypeProvider& valueTypeProvider,
    RawSampleTransformer* rawSampleTransformer,
    MetricsRegistry& metricsRegistry,
    shared::pmr::memory_resource* memoryResource)
    :
    CollectorBase<RawGarbageCollectionSample>("GarbageCollectorProvider", valueTypeProvider.GetOrRegister(TimelineSampleType::Definitions), rawSampleTransformer, memoryResource)
{

    _gen0CountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_gc_gen0");
    _gen1CountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_gc_gen1");
    _gen2CountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_gc_gen2");
    _suspensionDurationMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_gc_suspensions_duration");
    _inducedCountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_gc_induced");
    _compactingGen2CountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_gc_compacting_gen2");
    _memoryPressureCountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_gc_memory_pressure");

    _gen2Size = 0;
    _lohSize = 0;
    _pohSize = 0;
    _gen2SizeMetric = metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_gc_gen2_size", [this]() {
        return static_cast<double>(_gen2Size);
    });
    _lohSizeMetric = metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_gc_loh_size", [this]() {
        return static_cast<double>(_lohSize);
    });

    // TODO: see if we need to "hide" this metrics for versions of .NET before POH was introduced
    //       or if we can just ignore the metric if the value is 0
    _pohSizeMetric = metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_gc_poh_size", [this]() {
        return static_cast<double>(_pohSize);
    });
}

void GarbageCollectionProvider::OnGarbageCollectionEnd(
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
    uint32_t memPressure)
{
    _suspensionDurationMetric->Add((double_t)pauseDuration.count());
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

    _gen2Size = gen2Size;
    _lohSize = lohSize;
    _pohSize = pohSize;

    RawGarbageCollectionSample rawSample;
    rawSample.Timestamp = std::chrono::nanoseconds(endTimestamp);
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
    std::chrono::nanoseconds timestamp,
    int32_t number,
    uint32_t generation,
    GCReason reason,
    GCType type
    )
{
}
