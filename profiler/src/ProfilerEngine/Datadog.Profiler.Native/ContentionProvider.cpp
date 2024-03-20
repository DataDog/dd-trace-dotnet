// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#include "ContentionProvider.h"

#include "COMHelpers.h"
#include "FrameworkThreadInfo.h"
#include "IAppDomainStore.h"
#include "IConfiguration.h"
#include "IFrameStore.h"
#include "IManagedThreadList.h"
#include "IRuntimeIdStore.h"
#include "IThreadsCpuManager.h"
#include "IUpscaleProvider.h"
#include "OsSpecificApi.h"
#include "Sample.h"
#include "SampleValueTypeProvider.h"

#include <math.h>


std::vector<uintptr_t> ContentionProvider::_emptyStack;

std::vector<SampleValueType> ContentionProvider::SampleTypeDefinitions(
    {
        {"lock-count", "count"},
        {"lock-time", "nanoseconds"}
    });


ContentionProvider::ContentionProvider(
    SampleValueTypeProvider& valueTypeProvider,
    ICorProfilerInfo4* pCorProfilerInfo,
    IManagedThreadList* pManagedThreadList,
    IFrameStore* pFrameStore,
    IThreadsCpuManager* pThreadsCpuManager,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore,
    IConfiguration* pConfiguration,
    MetricsRegistry& metricsRegistry)
    :
    CollectorBase<RawContentionSample>("ContentionProvider", valueTypeProvider.GetOrRegister(SampleTypeDefinitions), pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore),
    _pCorProfilerInfo{pCorProfilerInfo},
    _pManagedThreadList{pManagedThreadList},
    _sampler(pConfiguration->ContentionSampleLimit(), pConfiguration->GetUploadInterval(), false),
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
        return "0 - 9 ms";
    }

    if (contentionDurationNs < 50'000'000.0)
    {
        return " 10 - 49 ms";
    }

    if (contentionDurationNs < 100'000'000.0)
    {
        return "50 - 99 ms";
    }

    if (contentionDurationNs < 500'000'000.0)
    {
        return "100 - 499 ms";
    }

    return "+500 ms";
}

// .NET Framework implementation
void ContentionProvider::OnContention(uint64_t timestamp, uint32_t threadId, double contentionDurationNs, const std::vector<uintptr_t>& stack)
{
    AddContentionSample(timestamp, threadId, contentionDurationNs, stack);
}

// .NET synchronous implementation: we are expecting to be called from the same thread that is contending.
// It means that the current thread will be stack walking itself.
void ContentionProvider::OnContention(double contentionDurationNs)
{
    AddContentionSample(0, -1, contentionDurationNs, _emptyStack);
}

void ContentionProvider::AddContentionSample(uint64_t timestamp, uint32_t threadId, double contentionDurationNs, const std::vector<uintptr_t>& stack)
{
    _lockContentionsCountMetric->Incr();
    _lockContentionsDurationMetric->Add(contentionDurationNs);

    auto bucket = GetBucket(contentionDurationNs);

    {
        std::lock_guard lock(_contentionsLock);

        if (!_sampler.Sample(bucket, static_cast<uint64_t>(contentionDurationNs)))
        {
            return;
        }
    }

    RawContentionSample rawSample;

    // Synchronous case where the current thread is the contended thread
    // (i.e. receiving the contention events directly from ICorProfilerCallback)
    static uint64_t failureCount = 0;
    if ((timestamp == 0) && (threadId == -1) && stack.empty())
    {
        std::shared_ptr<ManagedThreadInfo> threadInfo;
        CALL(_pManagedThreadList->TryGetCurrentThreadInfo(threadInfo))

        const auto pStackFramesCollector = OsSpecificApi::CreateNewStackFramesCollectorInstance(_pCorProfilerInfo, _pConfiguration);
        pStackFramesCollector->PrepareForNextCollection();

        uint32_t hrCollectStack = E_FAIL;
        const auto result = pStackFramesCollector->CollectStackSample(threadInfo.get(), &hrCollectStack);
        if ((result->GetFramesCount() == 0) && (failureCount % 1000 == 0))
        {
            // log every 1000 failures
            failureCount++;
            Log::Info("Failed to walk ", failureCount, " stacks for sampled contention: ", HResultConverter::ToStringWithCode(hrCollectStack));
            return;
        }

        result->SetUnixTimeUtc(GetCurrentTimestamp());
        result->DetermineAppDomain(threadInfo->GetClrThreadId(), _pCorProfilerInfo);

        rawSample.LocalRootSpanId = result->GetLocalRootSpanId();
        rawSample.SpanId = result->GetSpanId();
        rawSample.AppDomainId = result->GetAppDomainId();
        rawSample.Timestamp = result->GetUnixTimeUtc();
        rawSample.Stack = result->GetCallstack();
        rawSample.ThreadInfo = threadInfo;
    }
    else
    // CLR events are received asynchronously from the Agent
    {
        // avoid the case where the ClrStackWalk event has been missed for the ContentionStart
        if ((stack.size() == 0) && (failureCount % 1000 == 0))
        {
            // log every 1000 failures
            failureCount++;
            Log::Info("Failed to get ", failureCount, " call stacks for sampled contention");
            return;
        }

        // We know that we don't have any span ID nor end point details

        rawSample.Timestamp = timestamp;
        auto end_stack = stack.begin() + std::min(stack.size(), static_cast<std::size_t>(Callstack::MaxFrames));
        std::copy(stack.begin(), end_stack, rawSample.Stack.begin());

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
            // It this is the case, we should simply set the AppDomainId to -1 all the time.
            AppDomainID appDomainId;
            if (SUCCEEDED(_pCorProfilerInfo->GetThreadAppDomain(threadInfo->GetClrThreadId(), &appDomainId)))
            {
                rawSample.AppDomainId = appDomainId;
            }
            else
            {
                rawSample.AppDomainId = -1;
            }
        }
        else  // create a fake IThreadInfo that wraps the OS thread id (no name, no profiler thread id)
        {
            rawSample.ThreadInfo = std::make_shared<FrameworkThreadInfo>(threadId);
        }
    }

    rawSample.ContentionDuration = contentionDurationNs;
    rawSample.Bucket = std::move(bucket);

    Add(std::move(rawSample));
    _sampledLockContentionsCountMetric->Incr();
    _sampledLockContentionsDurationMetric->Add(contentionDurationNs);
}


UpscalingInfo ContentionProvider::GetInfo()
{
    return {GetValueOffsets(), RawContentionSample::BucketLabelName, _sampler.GetGroups()};
}
