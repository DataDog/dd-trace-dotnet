// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#include "ContentionProvider.h"

#include "COMHelpers.h"
#include "IAppDomainStore.h"
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
    IConfiguration* pConfiguration)
    :
    CollectorBase<RawContentionSample>("ContentionProvider", valueOffset, pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore, pConfiguration),
    _pCorProfilerInfo{pCorProfilerInfo},
    _pManagedThreadList{pManagedThreadList},
    _sampler(pConfiguration->ContentionSampleLimit(), pConfiguration->GetUploadInterval()),
    _contentionDurationThreshold{pConfiguration->ContentionDurationThreshold()},
    _sampleLimit{pConfiguration->ContentionSampleLimit()}
{
}

void ContentionProvider::OnContention(double contentionDuration)
{
    // TODO: when upscaling will be done, implement per duration groups (100ms, 200ms, 500ms, +)
    //       to ensure a  better "statistical" distribution
    if (!_sampler.Sample())
    {
        return;
    }

    std::shared_ptr<ManagedThreadInfo> threadInfo;
    CALL(_pManagedThreadList->TryGetCurrentThreadInfo(threadInfo))

    const auto pStackFramesCollector = OsSpecificApi::CreateNewStackFramesCollectorInstance(_pCorProfilerInfo);
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
    rawSample.ContentionDuration = contentionDuration;

    Add(std::move(rawSample));
}
