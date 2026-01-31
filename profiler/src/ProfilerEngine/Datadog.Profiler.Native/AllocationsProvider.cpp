// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "AllocationsProvider.h"

#include "COMHelpers.h"
#include "FrameworkThreadInfo.h"
#include "HResultConverter.h"
#include "IAppDomainStore.h"
#include "IConfiguration.h"
#include "IFrameStore.h"
#include "IManagedThreadList.h"
#include "IRuntimeIdStore.h"
#include "ISampledAllocationsListener.h"
#include "IThreadsCpuManager.h"
#include "Log.h"
#include "MetricsRegistry.h"
#include "OsSpecificApi.h"
#include "RawSampleTransformer.h"
#include "SampleValueTypeProvider.h"

#include <chrono>

#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"

std::vector<SampleValueType> AllocationsProvider::SampleTypeDefinitions(
    {
        {"alloc-samples", "count", -1},
        {"alloc-size", "bytes", -1}
    }
);

std::vector<SampleValueType> AllocationsProvider::FrameworkSampleTypeDefinitions(
    {
        {"alloc-samples", "count", -1},
    }
);

AllocationsProvider::AllocationsProvider(
    bool isFramework,
    SampleValueTypeProvider& valueTypeProvider,
    ICorProfilerInfo4* pCorProfilerInfo,
    IManagedThreadList* pManagedThreadList,
    IFrameStore* pFrameStore,
    RawSampleTransformer* rawSampleTransformer,
    IConfiguration* pConfiguration,
    ISampledAllocationsListener* pListener,
    MetricsRegistry& metricsRegistry,
    CallstackProvider pool,
    shared::pmr::memory_resource* memoryResource,
    libdatadog::SymbolsStore* pSymbolsStore)
    :
    AllocationsProvider(
        isFramework
            ? valueTypeProvider.GetOrRegister(FrameworkSampleTypeDefinitions)
            : valueTypeProvider.GetOrRegister(SampleTypeDefinitions),
        pCorProfilerInfo, pManagedThreadList, pFrameStore,
        rawSampleTransformer,
        pConfiguration,
        pListener,
        metricsRegistry,
        std::move(pool),
        memoryResource,
        pSymbolsStore)
{
}

AllocationsProvider::AllocationsProvider(
    std::vector<SampleValueTypeProvider::Offset> valueTypes,
    ICorProfilerInfo4* pCorProfilerInfo,
    IManagedThreadList* pManagedThreadList,
    IFrameStore* pFrameStore,
    RawSampleTransformer* rawSampleTransformer,
    IConfiguration* pConfiguration,
    ISampledAllocationsListener* pListener,
    MetricsRegistry& metricsRegistry,
    CallstackProvider pool,
    shared::pmr::memory_resource* memoryResource,
    libdatadog::SymbolsStore* pSymbolsStore) :
    CollectorBase<RawAllocationSample>("AllocationsProvider", std::move(valueTypes), rawSampleTransformer, memoryResource, pSymbolsStore),
    _pCorProfilerInfo(pCorProfilerInfo),
    _pManagedThreadList(pManagedThreadList),
    _pFrameStore(pFrameStore),
    _pListener(pListener),
    _sampler(pConfiguration->AllocationSampleLimit(), pConfiguration->GetUploadInterval()),
    _sampleLimit(pConfiguration->AllocationSampleLimit()),
    _pConfiguration(pConfiguration),
    _callstackProvider{std::move(pool)},
    _metricsRegistry{metricsRegistry}
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

    auto threadInfo = ManagedThreadInfo::CurrentThreadInfo;
    if (threadInfo == nullptr)
    {
        LogOnce(Warn, "AllocationsProvider::OnAllocation: Profiler failed at getting the current managed thread info ");
        return;
    }

    const auto pStackFramesCollector = OsSpecificApi::CreateNewStackFramesCollectorInstance(
        _pCorProfilerInfo, _pConfiguration, &_callstackProvider, _metricsRegistry);
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
    rawSample.Stack = result->GetCallstack();
    rawSample.ThreadInfo = threadInfo;
    rawSample.AllocationSize = objectSize;
    rawSample.Address = address;
    rawSample.MethodTable = classId;

    // the classID can be null when events are replayed in integration tests
    if ((classId == 0) || !_pFrameStore->GetTypeName(classId, rawSample.AllocationClass))
    {
        // The provided type name contains the metadata-based `xx syntax for generics instead of <>
        // So rely on the frame store to get a C#-like representation like what is done for frames
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

void AllocationsProvider::OnAllocationSampled(
    uint32_t allocationKind,
    ClassID classId,
    const WCHAR* typeName,
    uintptr_t address,
    uint64_t objectSize,
    uint64_t allocationByteOffset)
{
    // TODO: deal with .NET+ AllocationSampled event and find a way to upscale the values
    // look at https://github.com/DataDog/dd-trace-dotnet/pull/4236

    _allocationsCountMetric->Incr();
    _allocationsSizeMetric->Add((double_t)objectSize);

    // It is not possible to compute the total allocation size because we don't have the allocation amount
    // that is available in AllocationTick
    // _totalAllocationsSizeMetric->Add((double_t)allocationAmount);

    // remove sampling when recording allocations
    if (_shouldSubSample && (_sampleLimit > 0) && (!_sampler.Sample()))
    {
        return;
    }

    // create a sample from the allocation

    auto threadInfo = ManagedThreadInfo::CurrentThreadInfo;
    if (threadInfo == nullptr)
    {
        LogOnce(Warn, "AllocationsProvider::OnAllocation: Profiler failed at getting the current managed thread info ");
        return;
    }

    const auto pStackFramesCollector = OsSpecificApi::CreateNewStackFramesCollectorInstance(
        _pCorProfilerInfo, _pConfiguration, &_callstackProvider, _metricsRegistry);
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
    rawSample.Stack = result->GetCallstack();
    rawSample.ThreadInfo = threadInfo;

    // TODO: compute the upscaled allocation size or rely on upscaling
    rawSample.AllocationSize = objectSize;

    rawSample.Address = address;
    rawSample.MethodTable = classId;

    // the classID can be null when events are replayed in integration tests
    if ((classId == 0) || !_pFrameStore->GetTypeName(classId, rawSample.AllocationClass))
    {
        // The provided type name contains the metadata-based `xx syntax for generics instead of <>
        // So rely on the frame store to get a C#-like representation like what is done for frames
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

void AllocationsProvider::OnAllocation(std::chrono::nanoseconds timestamp,
                                       uint32_t threadId,
                                       uint32_t allocationKind,
                                       ClassID classId,
                                       const std::string& typeName,
                                       uint64_t allocationAmount,
                                       const std::vector<uintptr_t>& stack)
{
    _allocationsCountMetric->Incr();

    // TODO: don't create that metric if running under .NET Framework
    // _allocationsSizeMetric->Add((double_t)objectSize);

    _totalAllocationsSizeMetric->Add((double_t)allocationAmount);

    // avoid the case where the ClrStackWalk event has been missed for the AllocationTick
    static uint64_t failureCount = 0;
    if ((stack.size() == 0) && (failureCount % 1000 == 0))
    {
        // log every 1000 failures
        failureCount++;
        Log::Info("Failed to get ", failureCount, " call stacks for sampled allocation");
        return;
    }

    // remove sampling when recording allocations
    if (_shouldSubSample && (_sampleLimit > 0) && (!_sampler.Sample()))
    {
        return;
    }

    // create a sample from the allocation
    RawAllocationSample rawSample;

    // We know that we don't have any span ID nor end point details

    rawSample.Timestamp = timestamp;
    auto cs = _callstackProvider.Get();
    const auto nbFrames = std::min(stack.size(), static_cast<std::size_t>(cs.Capacity()));
    auto end_stack = stack.begin() + nbFrames;
    std::copy(stack.begin(), end_stack, cs.begin());
    cs.SetCount(nbFrames);
    rawSample.Stack = std::move(cs);

    // we need to create a fake IThreadInfo if there is no thread in ManagedThreadList with the same OS thread id
    // There is one race condition here: the contention events are received asynchronously so the event thread might be dead
    // (i.e. no more in our ManagedThreadList). In that case, we need to create a fake IThreadInfo with a profilerId = 0
    // The unique thread id = <profiler thread id> [# OS thread id]
    // It means that it is possible that the backend will not match this sample with the other samples from the "same" thread.
    //
    // The second race condition is different: the emitting thread might be dead and a new one gets created with the same OS thread id.
    // In that case, the sample will be associated to the new thread (and not the old dead one)
    //
    std::shared_ptr<ManagedThreadInfo> threadInfo;
    if (_pManagedThreadList->TryGetThreadInfo(threadId, threadInfo))
    {
        rawSample.ThreadInfo = threadInfo;

        // TODO: we need to check that threads are not jumping from one AppDomain to the other too frequently
        // because we might be receiving this event 1 second after it has been emitted
        // It this is the case, we should simply set the AppDomainId to 0 all the time.
        AppDomainID appDomainId;
        if (SUCCEEDED(_pCorProfilerInfo->GetThreadAppDomain(threadInfo->GetClrThreadId(), &appDomainId)))
        {
            rawSample.AppDomainId = appDomainId;
        }
        else
        {
            rawSample.AppDomainId = 0;
        }
    }
    else // create a fake IThreadInfo that wraps the OS thread id (no name, no profiler thread id)
    {
        rawSample.ThreadInfo = std::make_shared<FrameworkThreadInfo>(threadId);
        rawSample.AppDomainId = 0;
    }

    // rawSample.AllocationSize = objectSize;
    // rawSample.Address = address;
    rawSample.MethodTable = classId;

    // The provided type name contains the metadata-based `xx syntax for generics instead of <>
    // So rely on the frame store to get a C#-like representation like what is done for frames
    if (!_pFrameStore->GetTypeName(classId, rawSample.AllocationClass))
    {
        rawSample.AllocationClass = typeName;
    }

    // the listener is the live objects profiler: could be null if disabled
    if (_pListener != nullptr)
    {
        _pListener->OnAllocation(rawSample);
    }

    Add(std::move(rawSample));
    _sampledAllocationsCountMetric->Incr();

    // TODO: don't create that metric if running under .NET Framework
    //_sampledAllocationsSizeMetric->Add((double_t)objectSize);
}

UpscalingPoissonInfo AllocationsProvider::GetPoissonInfo()
{
    auto const& offsets = GetValueOffsets(); //              sum(size)       count
    UpscalingPoissonInfo info{ offsets, AllocTickThreshold, offsets[1], offsets[0] };
    return info;
}
