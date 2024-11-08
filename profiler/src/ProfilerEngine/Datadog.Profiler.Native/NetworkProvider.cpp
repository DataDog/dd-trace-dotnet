// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "NetworkProvider.h"

#include "COMHelpers.h"
#include "IManagedThreadList.h"
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
    _callstackProvider{ std::move(callstackProvider) }
{
}

bool NetworkProvider::CaptureThreadInfo(NetworkRequestInfo& info)
{
    std::shared_ptr<ManagedThreadInfo> threadInfo;
    INVOKE(_pManagedThreadList->TryGetCurrentThreadInfo(threadInfo))

    // only requests emitted by managed threads with span ID are captured
    if (!threadInfo->HasTraceContext())
    {
        return false;
    }

    // TODO: implement additional sampling strategy if needed

    // collect current call stack
    uint32_t hrCollectStack = E_FAIL;
    const auto pStackFramesCollector = OsSpecificApi::CreateNewStackFramesCollectorInstance(_pCorProfilerInfo, _pConfiguration, &_callstackProvider);

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

    auto slot = _requests.insert_or_assign(activity, NetworkRequestInfo { url, timestamp });
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


    // compute the durations and close the request
    auto duration = timestamp - requestInfo->second.StartTimestamp;
    RawNetworkSample rawSample;
    rawSample.Timestamp = requestInfo->second.StartTimestamp;
    rawSample.Url = std::move(requestInfo->second.Url);
    rawSample.AppDomainId = requestInfo->second.AppDomainId;
    rawSample.LocalRootSpanId = requestInfo->second.LocalRootSpanID;
    rawSample.SpanId = requestInfo->second.SpanID;
    rawSample.Stack = std::move(requestInfo->second.StartCallStack);
    rawSample.ThreadInfo = std::move(requestInfo->second.StartThreadInfo);

    rawSample.EndTimestamp = timestamp;
    auto currentThreadInfo = ManagedThreadInfo::CurrentThreadInfo;
    rawSample.EndThreadId = currentThreadInfo->GetProfileThreadId();
    rawSample.StatusCode = requestInfo->second.StatusCode;

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

    // compute the durations and close the request
    auto duration = timestamp - requestInfo->second.StartTimestamp;
    RawNetworkSample rawSample;
    rawSample.Timestamp = requestInfo->second.StartTimestamp;
    rawSample.Url = std::move(requestInfo->second.Url);
    rawSample.AppDomainId = requestInfo->second.AppDomainId;
    rawSample.LocalRootSpanId = requestInfo->second.LocalRootSpanID;
    rawSample.SpanId = requestInfo->second.SpanID;
    rawSample.Stack = std::move(requestInfo->second.StartCallStack);
    rawSample.ThreadInfo = std::move(requestInfo->second.StartThreadInfo);

    rawSample.EndTimestamp = timestamp;
    auto currentThreadInfo = ManagedThreadInfo::CurrentThreadInfo;
    rawSample.EndThreadId = currentThreadInfo->GetProfileThreadId();
    rawSample.Error = std::move(message);

    Add(std::move(rawSample));

    _requests.erase(activity);
}


bool NetworkProvider::TryGetActivity(LPCGUID pActivityId, NetworkActivity& activity, bool isRoot)
{
    return NetworkActivity::GetRootActivity(pActivityId, activity, isRoot);
}
