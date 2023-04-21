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
#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"


StopTheWorldGCProvider::StopTheWorldGCProvider(
    uint32_t valueOffset,
    IFrameStore* pFrameStore,
    IThreadsCpuManager* pThreadsCpuManager,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore,
    IConfiguration* pConfiguration)
    :
    CollectorBase<RawStopTheWorldSample>("StopTheWorldGCProvider", valueOffset, GarbageCollectionProvider::SampleTypeDefinitions.size(), pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore, pConfiguration)
{
}

void StopTheWorldGCProvider::OnSuspension(int32_t number, uint32_t generation, uint64_t pauseDuration, uint64_t timestamp)
{
    RawStopTheWorldSample rawSample;
    rawSample.Timestamp = timestamp;
    rawSample.LocalRootSpanId = 0;
    rawSample.SpanId = 0;
    rawSample.AppDomainId = (AppDomainID)nullptr;
    rawSample.ThreadInfo = nullptr;
    rawSample.Stack.clear();

    rawSample.Number = number;
    rawSample.Generation = generation;
    rawSample.Duration = pauseDuration;

    Add(std::move(rawSample));
}
