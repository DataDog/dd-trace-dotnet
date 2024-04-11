// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "AllocationsProvider.h"
#include "COMHelpers.h"
#include "HResultConverter.h"
#include "IConfiguration.h"
#include "IManagedThreadList.h"
#include "IFrameStore.h"
#include "IThreadsCpuManager.h"
#include "IAppDomainStore.h"
#include "IRuntimeIdStore.h"
#include "ISampledAllocationsListener.h"
#include "Log.h"
#include "MetricsRegistry.h"
#include "OsSpecificApi.h"
#include "SampleValueTypeProvider.h"

#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"


std::vector<SampleValueType> AllocationsProvider::SampleTypeDefinitions(
    {
        {"alloc-samples", "count"},
        {"alloc-size", "bytes"}});

AllocationsProvider::AllocationsProvider(
    SampleValueTypeProvider& valueTypeProvider,
    ICorProfilerInfo4* pCorProfilerInfo,
    IManagedThreadList* pManagedThreadList,
    IFrameStore* pFrameStore,
    IThreadsCpuManager* pThreadsCpuManager,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore,
    IConfiguration* pConfiguration,
    ISampledAllocationsListener* pListener,
    MetricsRegistry& metricsRegistry)
    :
    AllocationsProvider(
        valueTypeProvider.GetOrRegister(SampleTypeDefinitions),
        pCorProfilerInfo, pManagedThreadList, pFrameStore,
        pThreadsCpuManager, pAppDomainStore, pRuntimeIdStore,
        pConfiguration,
        pListener,
        metricsRegistry)
{
}

AllocationsProvider::AllocationsProvider(
    std::vector<SampleValueTypeProvider::Offset> valueTypes,
    ICorProfilerInfo4* pCorProfilerInfo,
    IManagedThreadList* pManagedThreadList,
    IFrameStore* pFrameStore,
    IThreadsCpuManager* pThreadsCpuManager,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore,
    IConfiguration* pConfiguration,
    ISampledAllocationsListener* pListener,
    MetricsRegistry& metricsRegistry) :
    CollectorBase<RawAllocationSample>("AllocationsProvider", std::move(valueTypes), pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore),
    _pCorProfilerInfo(pCorProfilerInfo),
    _pManagedThreadList(pManagedThreadList),
    _pFrameStore(pFrameStore),
    _pListener(pListener),
    _sampler(pConfiguration->AllocationSampleLimit(), pConfiguration->GetUploadInterval()),
    _sampleLimit(pConfiguration->AllocationSampleLimit()),
    _pConfiguration(pConfiguration)
{
    _allocationsCountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_allocations");
    _allocationsSizeMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_allocations_size");
    _sampledAllocationsCountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_sampled_allocations");
    _sampledAllocationsSizeMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_sampled_allocations_size");
    _totalAllocationsSizeMetric = metricsRegistry.GetOrRegister<SumMetric>("dotnet_total_allocations_size");

    // disable sub sampling when recording allocations
    _shouldSubSample = !_pConfiguration->IsAllocationRecorderEnabled();
}


void AllocationsProvider::OnAllocation(uint32_t allocationKind,
                                       ClassID classId,
                                       const WCHAR* typeName,
                                       uintptr_t address,
                                       uint64_t objectSize,
                                       uint64_t allocationAmount)
{
    _allocationsCountMetric->Incr();
    _allocationsSizeMetric->Add((double_t)objectSize);
    _totalAllocationsSizeMetric->Add((double_t)allocationAmount);

    // remove sampling when recording allocations
    if (_shouldSubSample && (_sampleLimit > 0) && (!_sampler.Sample()))
    {
        return;
    }

    // create a sample from the allocation

    std::shared_ptr<ManagedThreadInfo> threadInfo;
    CALL(_pManagedThreadList->TryGetCurrentThreadInfo(threadInfo))

    const auto pStackFramesCollector = OsSpecificApi::CreateNewStackFramesCollectorInstance(_pCorProfilerInfo, _pConfiguration);
    pStackFramesCollector->PrepareForNextCollection();

    uint32_t hrCollectStack = E_FAIL;
    const auto result = pStackFramesCollector->CollectStackSample(threadInfo.get(), &hrCollectStack);
    static uint64_t failureCount = 0;
    if ((result->GetFramesCount() == 0) && (failureCount % 100 == 0))
    {
        // log every 100 failures (every ~10 MB worse case)
        failureCount++;
        Log::Warn("Failed to walk ", failureCount, " stacks for sampled allocation: ", HResultConverter::ToStringWithCode(hrCollectStack));
        return;
    }

    result->SetUnixTimeUtc(GetCurrentTimestamp());

    RawAllocationSample rawSample;
    rawSample.Timestamp = result->GetUnixTimeUtc();
    rawSample.LocalRootSpanId = result->GetLocalRootSpanId();
    rawSample.SpanId = result->GetSpanId();
    rawSample.AppDomainId = threadInfo->GetAppDomainId();
    result->CopyInstructionPointers(rawSample.Stack);
    rawSample.ThreadInfo = threadInfo;
    rawSample.AllocationSize = objectSize;
    rawSample.Address = address;
    rawSample.MethodTable = classId;

    // The provided type name contains the metadata-based `xx syntax for generics instead of <>
    // So rely on the frame store to get a C#-like representation like what is done for frames
    if (!_pFrameStore->GetTypeName(classId, rawSample.AllocationClass))
    {
        rawSample.AllocationClass = shared::ToString(shared::WSTRING(typeName));
    }

    // the listener is the live objects profiler: could be null if disabled
    if (_pListener != nullptr)
    {
        _pListener->OnAllocation(rawSample);
    }

    Add(std::move(rawSample));
    _sampledAllocationsCountMetric->Incr();
    _sampledAllocationsSizeMetric->Add((double_t)objectSize);
}