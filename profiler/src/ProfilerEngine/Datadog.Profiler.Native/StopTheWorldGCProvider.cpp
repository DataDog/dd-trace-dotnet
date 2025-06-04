// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "StopTheWorldGCProvider.h"

#include "COMHelpers.h"
#include "GarbageCollectionProvider.h"
#include "HResultConverter.h"
#include "IAppDomainStore.h"
#include "IFrameStore.h"
#include "IManagedThreadList.h"
#include "IRuntimeIdStore.h"
#include "IThreadsCpuManager.h"
#include "Log.h"
#include "OsSpecificApi.h"
#include "RawSampleTransformer.h"
#include "SampleValueTypeProvider.h"
#include "TimelineSampleType.h"

#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"


StopTheWorldGCProvider::StopTheWorldGCProvider(
    SampleValueTypeProvider& valueTypeProvider,
    RawSampleTransformer* rawSampleTransformer,
    shared::pmr::memory_resource* memoryResource)
    :
    CollectorBase<RawStopTheWorldSample>("StopTheWorldGCProvider", valueTypeProvider.GetOrRegister(TimelineSampleType::Definitions), rawSampleTransformer, memoryResource)
{
}

void StopTheWorldGCProvider::OnSuspension(std::chrono::nanoseconds timestamp, int32_t number, uint32_t generation, std::chrono::nanoseconds pauseDuration)
{
    RawStopTheWorldSample rawSample;
    rawSample.Timestamp = timestamp;
    rawSample.LocalRootSpanId = 0;
    rawSample.SpanId = 0;
    rawSample.AppDomainId = (AppDomainID)nullptr;
    rawSample.ThreadInfo = nullptr;

    rawSample.Number = number;
    rawSample.Generation = generation;
    rawSample.Duration = pauseDuration;

    Add(std::move(rawSample));
}
