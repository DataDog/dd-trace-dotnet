// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ThreadLifetimeProvider.h"
#include "SampleValueTypeProvider.h"
#include "GarbageCollectionProvider.h"
#include "OpSysTools.h"
#include "TimelineSampleType.h"

ThreadLifetimeProvider::ThreadLifetimeProvider(
    SampleValueTypeProvider& valueTypeProvider,
    IFrameStore* pFrameStore,
    IThreadsCpuManager* pThreadsCpuManager,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore,
    IConfiguration* pConfiguration,
    shared::pmr::memory_resource* memoryResource)
    :
    CollectorBase<RawThreadLifetimeSample>(
        "ThreadLifetimeProvider",
        valueTypeProvider.GetOrRegister(TimelineSampleType::Definitions),
        pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore, memoryResource)
{
}

void ThreadLifetimeProvider::OnThreadStart(std::shared_ptr<ManagedThreadInfo> pThreadInfo)
{
    Add(CreateSample(pThreadInfo, ThreadEventKind::Start));
}

void ThreadLifetimeProvider::OnThreadStop(std::shared_ptr<ManagedThreadInfo> pThreadInfo)
{
    Add(CreateSample(pThreadInfo, ThreadEventKind::Stop));
}

RawThreadLifetimeSample ThreadLifetimeProvider::CreateSample(std::shared_ptr<ManagedThreadInfo> pThreadInfo, ThreadEventKind kind)
{
    RawThreadLifetimeSample rawSample;
    rawSample.Timestamp = GetCurrentTimestamp();
    rawSample.LocalRootSpanId = 0;
    rawSample.SpanId = 0;
    rawSample.AppDomainId = pThreadInfo->GetAppDomainId();
    rawSample.ThreadInfo = std::move(pThreadInfo);
    rawSample.Kind = kind;

    return rawSample;
}

uint64_t ThreadLifetimeProvider::GetCurrentTimestamp()
{
    return OpSysTools::GetHighPrecisionTimestamp();
}
