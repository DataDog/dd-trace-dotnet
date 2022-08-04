// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "AllocationsProvider.h"
#include "HResultConverter.h"
#include "IManagedThreadList.h"
#include "IFrameStore.h"
#include "IThreadsCpuManager.h"
#include "IAppDomainStore.h"
#include "IRuntimeIdStore.h"
#include "Log.h"
#include "OsSpecificApi.h"
#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"

#define CALL(x)                                                                                               \
    {                                                                                                         \
        HRESULT hr = x;                                                                                       \
        if (FAILED(hr))                                                                                       \
        {                                                                                                     \
            Log::Warn("Profiler call failed with result ", HResultConverter::ToStringWithCode(hr), ": ", #x); \
            return;                                                                                           \
        }                                                                                                     \
    }


AllocationsProvider::AllocationsProvider(
    ICorProfilerInfo4* pCorProfilerInfo,
    IManagedThreadList* pManagedThreadList,
    IFrameStore* pFrameStore,
    IThreadsCpuManager* pThreadsCpuManager,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore)
    :
    CollectorBase<RawAllocationSample>("AllocationsProvider", pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore),
    _pCorProfilerInfo(pCorProfilerInfo),
    _pManagedThreadList(pManagedThreadList)
{
}


void AllocationsProvider::OnAllocation(uint32_t allocationKind,
                                       const WCHAR* typeName,
                                       uintptr_t address,
                                       uint64_t objectSize)
{
    // create a sample from the allocation

    ManagedThreadInfo* threadInfo;
    CALL(_pManagedThreadList->TryGetCurrentThreadInfo(&threadInfo))

    const auto pStackFramesCollector = OsSpecificApi::CreateNewStackFramesCollectorInstance(_pCorProfilerInfo);
    pStackFramesCollector->PrepareForNextCollection();

    uint32_t hrCollectStack = E_FAIL;
    const auto result = pStackFramesCollector->CollectStackSample(threadInfo, &hrCollectStack);
    if (result->GetFramesCount() == 0)
    {
        Log::Warn("Failed to walk stack for sampled allocation: ", HResultConverter::ToStringWithCode(hrCollectStack));
        return;
    }

    result->DetermineAppDomain(threadInfo->GetClrThreadId(), _pCorProfilerInfo);

    RawAllocationSample rawSample;
    rawSample.Timestamp = result->GetUnixTimeUtc();
    rawSample.LocalRootSpanId = result->GetLocalRootSpanId();
    rawSample.SpanId = result->GetSpanId();
    rawSample.AppDomainId = result->GetAppDomainId();
    result->CopyInstructionPointers(rawSample.Stack);
    rawSample.ThreadInfo = threadInfo;
    threadInfo->AddRef();
    rawSample.AllocationSize = objectSize;
    rawSample.AllocationClass = std::move(shared::ToString(shared::WSTRING(typeName)));

    Add(std::move(rawSample));
}