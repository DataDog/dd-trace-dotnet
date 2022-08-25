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

ContentionProvider::ContentionProvider(
    ICorProfilerInfo4* pCorProfilerInfo,
    IManagedThreadList* pManagedThreadList,
    IFrameStore* pFrameStore,
    IThreadsCpuManager* pThreadsCpuManager,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore)
    :
    CollectorBase<RawContentionSample>("ContentionProvider", pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore),
    _pCorProfilerInfo{pCorProfilerInfo},
    _pManagedThreadList{pManagedThreadList}
{
}

void ContentionProvider::OnContention(double contentionDuration)
{
    ManagedThreadInfo* threadInfo;
    CALL(_pManagedThreadList->TryGetCurrentThreadInfo(&threadInfo))

    const auto pStackFramesCollector = OsSpecificApi::CreateNewStackFramesCollectorInstance(_pCorProfilerInfo);
    pStackFramesCollector->PrepareForNextCollection();

    uint32_t hrCollectStack = E_FAIL;
    const auto result = pStackFramesCollector->CollectStackSample(threadInfo, &hrCollectStack);
    if (result->GetFramesCount() == 0)
    {
        Log::Warn("Failed to walk stack for sampled contention: ", HResultConverter::ToStringWithCode(hrCollectStack));
        return;
    }

    result->DetermineAppDomain(threadInfo->GetClrThreadId(), _pCorProfilerInfo);

    RawContentionSample rawSample;
    rawSample.Timestamp = result->GetUnixTimeUtc();
    rawSample.LocalRootSpanId = result->GetLocalRootSpanId();
    rawSample.SpanId = result->GetSpanId();
    rawSample.AppDomainId = result->GetAppDomainId();
    result->CopyInstructionPointers(rawSample.Stack);
    rawSample.ThreadInfo = threadInfo;
    threadInfo->AddRef();
    rawSample.ContentionDuration = contentionDuration;

    Add(std::move(rawSample));
}
