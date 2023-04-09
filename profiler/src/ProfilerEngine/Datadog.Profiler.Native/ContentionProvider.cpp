// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#include "ContentionProvider.h"

#include "COMHelpers.h"
#include "IAppDomainStore.h"
#include "IConfiguration.h"
#include "IFrameStore.h"
#include "IManagedThreadList.h"
#include "IRuntimeIdStore.h"
#include "IThreadsCpuManager.h"
#include "OsSpecificApi.h"
#include "Sample.h"


std::vector<SampleValueType> ContentionProvider::SampleTypeDefinitions(
    {
        {"lock-count", "count"},
        {"lock-time", "nanoseconds"}
    });


ContentionProvider::ContentionProvider(
    uint32_t valueOffset,
    ICorProfilerInfo4* pCorProfilerInfo,
    IManagedThreadList* pManagedThreadList,
    IFrameStore* pFrameStore,
    IThreadsCpuManager* pThreadsCpuManager,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore,
    IConfiguration* pConfiguration,
    MetricsRegistry& metricsRegistry)
    :
    CollectorBase<RawContentionSample>("ContentionProvider", valueOffset, pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore, pConfiguration),
    _pCorProfilerInfo{pCorProfilerInfo},
    _pManagedThreadList{pManagedThreadList},
    _sampler(pConfiguration->ContentionSampleLimit(), pConfiguration->GetUploadInterval()),
    _contentionDurationThreshold{pConfiguration->ContentionDurationThreshold()},
    _sampleLimit{pConfiguration->ContentionSampleLimit()},
    _pConfiguration{pConfiguration}
{
    _lockContentionsCountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_lock_contentions");
    _lockContentionsDurationMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_lock_contentions_duration");
    _sampledLockContentionsCountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_sampled_lock_contentions");
    _sampledLockContentionsDurationMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_sampled_lock_contentions_duration");
}

std::string ContentionProvider::GetBucket(double contentionDurationNs)
{
    if (contentionDurationNs < 10'000'000.0)
    {
        return "0-9ms";
    }

    if (contentionDurationNs < 50'000'000.0)
    {
        return "10-49ms";
    }

    if (contentionDurationNs < 100'000'000.0)
    {
        return "50-99ms";
    }

    if (contentionDurationNs < 500'000'000.0)
    {
        return "100-499ms";
    }

    return "+500ms";
}

void ContentionProvider::OnContention(double contentionDurationNs)
{
    _lockContentionsCountMetric->Incr();
    _lockContentionsDurationMetric->Add(contentionDurationNs);

    auto bucket = GetBucket(contentionDurationNs);

    if (!_sampler.Sample(bucket))
    {
        return;
    }

    std::shared_ptr<ManagedThreadInfo> threadInfo;
    CALL(_pManagedThreadList->TryGetCurrentThreadInfo(threadInfo))

    const auto pStackFramesCollector = OsSpecificApi::CreateNewStackFramesCollectorInstance(_pCorProfilerInfo, _pConfiguration);
    pStackFramesCollector->PrepareForNextCollection();

    uint32_t hrCollectStack = E_FAIL;
    const auto result = pStackFramesCollector->CollectStackSample(threadInfo.get(), &hrCollectStack);
    if (result->GetFramesCount() == 0)
    {
        Log::Warn("Failed to walk stack for sampled contention: ", HResultConverter::ToStringWithCode(hrCollectStack));
        return;
    }

    result->SetUnixTimeUtc(GetCurrentTimestamp());
    result->DetermineAppDomain(threadInfo->GetClrThreadId(), _pCorProfilerInfo);

    RawContentionSample rawSample;
    rawSample.Timestamp = result->GetUnixTimeUtc();
    rawSample.LocalRootSpanId = result->GetLocalRootSpanId();
    rawSample.SpanId = result->GetSpanId();
    rawSample.AppDomainId = result->GetAppDomainId();
    result->CopyInstructionPointers(rawSample.Stack);
    rawSample.ThreadInfo = threadInfo;
    rawSample.ContentionDuration = contentionDurationNs;
    rawSample.Bucket = std::move(bucket);
    Add(std::move(rawSample));
    _sampledLockContentionsCountMetric->Incr();
    _sampledLockContentionsDurationMetric->Add(contentionDurationNs);
}
