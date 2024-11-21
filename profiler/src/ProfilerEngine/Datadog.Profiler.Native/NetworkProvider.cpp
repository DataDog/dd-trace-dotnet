// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "NetworkProvider.h"

#include "COMHelpers.h"
#include "IManagedThreadList.h"
#include "Log.h"
#include "OsSpecificApi.h"

std::vector<SampleValueType> NetworkProvider::SampleTypeDefinitions(
{
    {"request-time", "nanoseconds"}
});


NetworkProvider::NetworkProvider(
    SampleValueTypeProvider& valueTypeProvider,
    ICorProfilerInfo4* pCorProfilerInfo,
    IManagedThreadList* pManagedThreadList,
    IFrameStore* pFrameStore,
    IThreadsCpuManager* pThreadsCpuManager,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore,
    IConfiguration* pConfiguration,
    MetricsRegistry& metricsRegistry,
    CallstackProvider callstackProvider,
    shared::pmr::memory_resource* memoryResource)
    :
    CollectorBase<RawNetworkSample>(
        "NetworkProvider",
        valueTypeProvider.GetOrRegister(SampleTypeDefinitions),
        pThreadsCpuManager,
        pFrameStore,
        pAppDomainStore,
        pRuntimeIdStore,
        memoryResource),
    _pCorProfilerInfo{ pCorProfilerInfo },
    _pManagedThreadList{ pManagedThreadList },
    _pConfiguration{ pConfiguration },
    _callstackProvider{ std::move(callstackProvider) },
    _metricsRegistry{metricsRegistry}
{
}

bool NetworkProvider::CaptureThreadInfo(NetworkRequestInfo& info)
{
    std::shared_ptr<ManagedThreadInfo> threadInfo;
    INVOKE(_pManagedThreadList->TryGetCurrentThreadInfo(threadInfo))

    // TODO: add a DD_INTERNAL_PROFILING_FORCE_HTTP_SAMPLING environment variable to force sampling
    //       for the integration tests (no need to have a span nor a minimum duration)
    //
    // TODO: only requests emitted by managed threads with span ID are captured
    //if (!threadInfo->HasTraceContext())
    //{
    //    return false;
    //}

    // TODO: implement additional sampling strategy if needed

    // collect current call stack
    uint32_t hrCollectStack = E_FAIL;
    const auto pStackFramesCollector = OsSpecificApi::CreateNewStackFramesCollectorInstance(_pCorProfilerInfo, _pConfiguration, &_callstackProvider, _metricsRegistry);

    pStackFramesCollector->PrepareForNextCollection();
    const auto result = pStackFramesCollector->CollectStackSample(threadInfo.get(), &hrCollectStack);

    static uint64_t failureCount = 0;
    if ((result->GetFramesCount() == 0) && (failureCount % 100 == 0))
    {
        // log every 100 failures
        failureCount++;
        Log::Warn("Failed to walk ", failureCount, " stacks for sampled HTTP request: ", HResultConverter::ToStringWithCode(hrCollectStack));
        return false;
    }

    info.AppDomainId = threadInfo->GetAppDomainId();
    info.LocalRootSpanID = result->GetLocalRootSpanId();
    info.SpanID = result->GetSpanId();
    info.StartCallStack = result->GetCallstack();
    info.StartThreadInfo = threadInfo;

    return true;
}

void NetworkProvider::OnRequestStart(uint64_t timestamp, LPCGUID pActivityId, std::string url)
{
    NetworkActivity activity;
    if (!TryGetActivity(pActivityId, activity))
    {
        return;
    }

    auto existingInfo = _requests.find(activity);
    if (existingInfo != _requests.end())
    {
        // TODO: this should never happen; i.e. a request with the same activity is already in progress
        _requests.erase(activity);
        return;
    }

    auto slot = _requests.insert_or_assign(activity, NetworkRequestInfo{ url, timestamp });
    if (!CaptureThreadInfo(slot.first->second))
    {
        _requests.erase(activity);
    }
}

void NetworkProvider::OnRequestStop(uint64_t timestamp, LPCGUID pActivityId, uint32_t statusCode)
{
    NetworkActivity activity;
    if (!TryGetActivity(pActivityId, activity))
    {
        return;
    }

    auto requestInfo = _requests.find(activity);
    if (requestInfo == _requests.end())
    {
        // TODO: this should never happen; i.e. a request with the same activity is not in progress
        return;
    }

    RawNetworkSample rawSample;
    FillRawSample(rawSample, requestInfo->second, timestamp);
    rawSample.StatusCode = statusCode;
    rawSample.Error = std::move(requestInfo->second.Error);

    Add(std::move(rawSample));

    _requests.erase(activity);
}

void NetworkProvider::OnRequestFailed(uint64_t timestamp, LPCGUID pActivityId, std::string message)
{
    NetworkActivity activity;
    if (!TryGetActivity(pActivityId, activity))
    {
        return;
    }

    auto requestInfo = _requests.find(activity);
    if (requestInfo == _requests.end())
    {
        // TODO: this should never happen; i.e. a request with the same activity is not in progress
        return;
    }

    requestInfo->second.Error = std::move(message);
}

void NetworkProvider::FillRawSample(RawNetworkSample& sample, NetworkRequestInfo& info, uint64_t timestamp)
{
    sample.StartTimestamp = info.StartTimestamp;
    sample.Timestamp = timestamp;
    sample.Url = std::move(info.Url);
    sample.AppDomainId = info.AppDomainId;
    sample.LocalRootSpanId = info.LocalRootSpanID;
    sample.SpanId = info.SpanID;
    sample.Stack = std::move(info.StartCallStack);
    sample.ThreadInfo = std::move(info.StartThreadInfo);
    auto currentThreadInfo = ManagedThreadInfo::CurrentThreadInfo;
    sample.EndThreadId = currentThreadInfo->GetProfileThreadId();
}


bool NetworkProvider::TryGetActivity(LPCGUID pActivityId, NetworkActivity& activity, bool isRoot)
{
    return NetworkActivity::GetRootActivity(pActivityId, activity, isRoot);
}
