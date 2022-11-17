// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "GarbageCollectionProvider.h"


std::vector<SampleValueType> GarbageCollectionProvider::SampleTypeDefinitions(
    {
        {"timeline", "nanoseconds"}
    });

GarbageCollectionProvider::GarbageCollectionProvider(
    uint32_t valueOffset,
    IFrameStore* pFrameStore,
    IThreadsCpuManager* pThreadsCpuManager,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore,
    IConfiguration* pConfiguration)
    :
    CollectorBase<RawGarbageCollectionSample>("GarbageCollectorProvider", valueOffset, pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore, pConfiguration)
{
}

void GarbageCollectionProvider::OnGarbageCollection(
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
    RawGarbageCollectionSample rawSample;
    rawSample.Timestamp = endTimestamp;
    rawSample.LocalRootSpanId = 0;
    rawSample.SpanId = 0;
    rawSample.AppDomainId = (AppDomainID) nullptr;
    rawSample.ThreadInfo = nullptr;
    rawSample.Stack.clear();

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