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

#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"

#include <cmath>
#include "Configuration.h"

std::vector<SampleValueType> AllocationsProvider::SampleTypeDefinitions(
    {
        {"alloc-samples", "count"},
        {"alloc-size", "bytes"}
    });


AllocationsProvider::AllocationsProvider(
    uint32_t valueOffset,
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
    CollectorBase<RawAllocationSample>("AllocationsProvider", valueOffset, SampleTypeDefinitions.size(), pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore, pConfiguration),
    _pCorProfilerInfo(pCorProfilerInfo),
    _pManagedThreadList(pManagedThreadList),
    _pFrameStore(pFrameStore),
    _sampleLimit(pConfiguration->AllocationSampleLimit()),
    _sampler(pConfiguration->AllocationSampleLimit(), pConfiguration->GetUploadInterval()),
    _groupSampler(pConfiguration->AllocationSampleLimit(), pConfiguration->GetUploadInterval(), false),
    _pListener(pListener),
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
    if (result->GetFramesCount() == 0)
    {
        Log::Warn("Failed to walk stack for sampled allocation: ", HResultConverter::ToStringWithCode(hrCollectStack));
        return;
    }

    result->SetUnixTimeUtc(GetCurrentTimestamp());
    result->DetermineAppDomain(threadInfo->GetClrThreadId(), _pCorProfilerInfo);

    RawAllocationSample rawSample;
    rawSample.Timestamp = result->GetUnixTimeUtc();
    rawSample.LocalRootSpanId = result->GetLocalRootSpanId();
    rawSample.SpanId = result->GetSpanId();
    rawSample.AppDomainId = result->GetAppDomainId();
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

UpscalingInfo AllocationsProvider::GetInfo()
{
    auto allocationUpscaleMode = _pConfiguration->AllocationUpscaleMode();

    auto allocatedTypes = _groupSampler.GetGroups();
    if (allocationUpscaleMode == ALLOCATION_UPSCALE_POISSON_PER_TYPE)
    {
        // Simulate a Poisson per type:
        //      1_f64 / (1_f64 - (-avg / sampling_distance as f64).exp()
        // And we know that the proportionality factor will be Real/Sampled
        static const float distance = 100 * 1024;  // AllocationTick threshold
        std::vector<UpscaleStringGroup> upscaledGroups(allocatedTypes.size());
        for (UpscaleStringGroup& type: allocatedTypes)
        {
            float average = (float)type.RealValue / (float)type.RealCount;
            float upscaledFactor = (float)1 - std::expf(-average / distance);

            //UpscaleStringGroup upscaledType(type);
            UpscaleStringGroup upscaledType;
            upscaledType.Group = type.Group;
            upscaledType.RealCount = 1;
            upscaledType.RealValue = 1;
            upscaledType.SampledCount = static_cast<uint64_t>(upscaledFactor);
            upscaledType.SampledValue = static_cast<uint64_t>(upscaledFactor);

            upscaledGroups.push_back(upscaledType);
        }

        return {GetValueOffsets(), Sample::AllocationClassLabel, upscaledGroups};
    }

    // simple proportional upscaling
    return {GetValueOffsets(), Sample::AllocationClassLabel, allocatedTypes};
}


UpscalingPoissonInfo AllocationsProvider::GetPoissonInfo()
{
    auto const& offsets = GetValueOffsets(); //              sum(size)       count
    UpscalingPoissonInfo info {offsets, AllocTickThreshold, offsets[1], offsets[0]};
    return info;
}