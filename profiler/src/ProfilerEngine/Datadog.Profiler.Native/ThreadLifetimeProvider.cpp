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
    IConfiguration* pConfiguration)
    :
    CollectorBase<RawThreadLifetimeSample>(
        "ThreadLifetimeProvider",
        valueTypeProvider.GetOrRegister(TimelineSampleType::Definitions),
        pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore, pConfiguration)
{
}

void ThreadLifetimeProvider::OnThreadStart(std::shared_ptr<ManagedThreadInfo> pThreadInfo)
{
    RawThreadLifetimeSample rawSample;
    Init(rawSample);
    rawSample.ThreadInfo = pThreadInfo;
    rawSample.Kind = ThreadEventKind::Start;

    Add(std::move(rawSample));
}

void ThreadLifetimeProvider::OnThreadStop(std::shared_ptr<ManagedThreadInfo> pThreadInfo)
{
    RawThreadLifetimeSample rawSample;
    Init(rawSample);
    rawSample.ThreadInfo = pThreadInfo;
    rawSample.Kind = ThreadEventKind::Stop;

    Add(std::move(rawSample));
}

void ThreadLifetimeProvider::Init(RawThreadLifetimeSample& rawSample)
{
    rawSample.Timestamp = GetCurrentTimestamp();
    rawSample.LocalRootSpanId = 0;
    rawSample.SpanId = 0;
    rawSample.AppDomainId = (AppDomainID) nullptr;
    rawSample.Stack.clear();
}

uint64_t ThreadLifetimeProvider::GetCurrentTimestamp()
{
    return OpSysTools::GetHighPrecisionTimestamp();
}
