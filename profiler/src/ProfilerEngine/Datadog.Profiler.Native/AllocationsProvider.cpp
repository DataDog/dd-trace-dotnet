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

    // true if same proportional upscale for all types
    _isProportional = (_pConfiguration->AllocationUpscaleMode() == ALLOCATION_UPSCALE_PROPORTIONAL);

    // true if upscale proportionally after having applied a Poisson process per type
    _isProportionalAndPoisson = (_pConfiguration->AllocationUpscaleMode() == ALLOCATION_UPSCALE_POISSON_PER_TYPE);

    _realTotalAllocated = 0;
    _sampledTotalAllocated = 0;
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

    {
        std::unique_lock lock(_realTotalMutex);
        _realTotalAllocated += allocationAmount;
    }

    std::string sTypeName = shared::ToString(typeName);

    // remove sampling when recording allocations
    // however, we need to call the Sample() method to make per type aggregation work
    bool keepAllocation = false;
    if (_isProportional || _isProportionalAndPoisson)
    {
        keepAllocation = _groupSampler.Sample(sTypeName, objectSize);
    }
    else
    {
        keepAllocation = _sampler.Sample();
    }

    if (/* _shouldSubSample && */ (_sampleLimit > 0) && (!keepAllocation))
    {
        return;
    }

    // create a sample from the allocation
    {
        std::unique_lock lock(_realTotalMutex);
        _sampledTotalAllocated += objectSize;
    }

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
        rawSample.AllocationClass = sTypeName;
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
    if (_isProportionalAndPoisson)
    {
        // Simulate a Poisson per type:
        //      1_f64 / (1_f64 - (-avg / sampling_distance as f64).exp()
        // And we know that the proportionality factor will be Real/Sampled
        static const float distance = 100 * 1024;  // AllocationTick threshold
        std::vector<UpscaleStringGroup> upscaledGroups(allocatedTypes.size());
        for (UpscaleStringGroup& type: allocatedTypes)
        {
            float average = (float)type.RealValue / (float)type.RealCount;
            float upscaledFactor = (float)1 / ((float)1 - std::expf(-average / distance));

            UpscaleStringGroup upscaledType;
            upscaledType.Group = type.Group;
            upscaledType.RealCount = static_cast<uint64_t>(upscaledFactor);
            upscaledType.SampledCount = 1;
            upscaledType.RealValue = static_cast<uint64_t>(upscaledFactor);
            upscaledType.SampledValue = 1;

            upscaledGroups.push_back(upscaledType);
        }

        return {GetValueOffsets(), Sample::AllocationClassLabel, upscaledGroups};
    }

    // simple proportional upscaling
    //  the ratio is the real size / sampled size (sum of all sampled types)
    uint64_t totalAllocatedSize = 0;
    uint64_t sampledAllocatedSize = 0;
    {
        std::unique_lock lock(_realTotalMutex);

        totalAllocatedSize = _realTotalAllocated;
        _realTotalAllocated = 0;
        sampledAllocatedSize = _sampledTotalAllocated;
        _sampledTotalAllocated = 0;
    }

    for (UpscaleStringGroup& group: allocatedTypes)
    {
        // the same ratio is used for size and count because we don't have the real number of allocations
        // --> upscaled count will probably be much larger than the real one
        group.RealCount = totalAllocatedSize;
        group.SampledCount = sampledAllocatedSize;
        group.RealValue = totalAllocatedSize;
        group.SampledValue = sampledAllocatedSize;
    }

    return {GetValueOffsets(), Sample::AllocationClassLabel, allocatedTypes};
}


UpscalingPoissonInfo AllocationsProvider::GetPoissonInfo()
{
    auto const& offsets = GetValueOffsets(); //              sum(size)       count
    UpscalingPoissonInfo info {offsets, AllocTickThreshold, offsets[1], offsets[0]};
    return info;
}