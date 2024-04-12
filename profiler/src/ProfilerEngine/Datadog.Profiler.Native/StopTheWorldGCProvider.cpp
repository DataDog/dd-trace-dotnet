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
#include "SampleValueTypeProvider.h"
#include "TimelineSampleType.h"

#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"


StopTheWorldGCProvider::StopTheWorldGCProvider(
    SampleValueTypeProvider& valueTypeProvider,
    IFrameStore* pFrameStore,
    IThreadsCpuManager* pThreadsCpuManager,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore,
    IConfiguration* pConfiguration,
    shared::pmr::memory_resource* memoryResource)
    :
    CollectorBase<RawStopTheWorldSample>("StopTheWorldGCProvider", valueTypeProvider.GetOrRegister(TimelineSampleType::Definitions), pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore, memoryResource)
{
}

void StopTheWorldGCProvider::OnSuspension(uint64_t timestamp, int32_t number, uint32_t generation, uint64_t pauseDuration)
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
