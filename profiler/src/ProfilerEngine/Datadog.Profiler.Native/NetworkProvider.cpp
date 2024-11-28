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
    // all other durations in the code are in nanoseconds but the config is in milliseconds
    _requestDurationThreshold = static_cast<double>(pConfiguration->GetHttpRequestDurationThreshold().count() * 1000000);
}

bool NetworkProvider::CaptureThreadInfo(NetworkRequestInfo& info)
{
    std::shared_ptr<ManagedThreadInfo> threadInfo;
    INVOKE(_pManagedThreadList->TryGetCurrentThreadInfo(threadInfo))

    // sampling can be forced for tests
    if (!_pConfiguration->ForceHttpSampling())
    {
        // only requests emitted by managed threads with span ID are captured
        if (!threadInfo->HasTraceContext())
        {
            return false;
        }
    }

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

void NetworkProvider::OnRequestStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string url)
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

void NetworkProvider::OnRequestStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, uint32_t statusCode)
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

    // sampling can be forced for tests
    if (!_pConfiguration->ForceHttpSampling())
    {
        // only requests lasting more than a threshold are captured
        if ((timestamp - requestInfo->second.StartTimestamp).count() > _requestDurationThreshold)
        {
            // skip this request
            _requests.erase(activity);
            return;
        }

        // TODO: add additional filtering to avoid too many samples
        //       e.g. requests with specific status codes, min duration per phase, dynamic such as for exceptions, etc.
    }

    RawNetworkSample rawSample;
    FillRawSample(rawSample, requestInfo->second, timestamp);
    rawSample.StatusCode = statusCode;
    if (!requestInfo->second.Error.empty())
    {
        rawSample.Error = std::move(requestInfo->second.Error);
    }

    if (!requestInfo->second.RedirectUrl.empty())
    {
        rawSample.RedirectUrl = std::move(requestInfo->second.RedirectUrl);
    }

    if (!requestInfo->second.HandshakeError.empty())
    {
        rawSample.HandshakeError = std::move(requestInfo->second.HandshakeError);
    }

    Add(std::move(rawSample));

    _requests.erase(activity);
}

void NetworkProvider::OnRequestFailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string message)
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

void NetworkProvider::OnRedirect(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string redirectUrl)
{
    NetworkActivity activity;
    if (!TryGetActivity(pActivityId, activity))
    {
        return;
    }
    auto requestInfo = _requests.find(activity);
    if (requestInfo == _requests.end())
    {
        return;
    }

    requestInfo->second.RedirectUrl = std::move(redirectUrl);
}

void NetworkProvider::OnDnsResolutionStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId)
{
    NetworkActivity activity;
    if (!TryGetActivity(pActivityId, activity, false))
    {
        return;
    }
    auto requestInfo = _requests.find(activity);
    if (requestInfo == _requests.end())
    {
        return;
    }
    requestInfo->second.DnsStartTime = timestamp;
}
void NetworkProvider::OnDnsResolutionStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, bool success)
{
    NetworkActivity activity;
    if (!TryGetActivity(pActivityId, activity, false))
    {
        return;
    }
    auto requestInfo = _requests.find(activity);
    if (requestInfo == _requests.end())
    {
        return;
    }
    requestInfo->second.DnsDuration = timestamp - requestInfo->second.DnsStartTime;
    requestInfo->second.DnsResolutionSuccess = success;
}

void NetworkProvider::OnConnectStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId)
{
    NetworkActivity activity;
    if (!TryGetActivity(pActivityId, activity, false))
    {
        return;
    }
    auto requestInfo = _requests.find(activity);
    if (requestInfo == _requests.end())
    {
        return;
    }

    requestInfo->second.SocketConnectStartTime = timestamp;
}

void NetworkProvider::OnConnectStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId)
{
    NetworkActivity activity;
    if (!TryGetActivity(pActivityId, activity, false))
    {
        return;
    }
    auto requestInfo = _requests.find(activity);
    if (requestInfo == _requests.end())
    {
        return;
    }
    requestInfo->second.SocketDuration = timestamp - requestInfo->second.SocketConnectStartTime;
}

void NetworkProvider::OnConnectFailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string message)
{
    NetworkActivity activity;
    if (!TryGetActivity(pActivityId, activity, false))
    {
        return;
    }
    auto requestInfo = _requests.find(activity);
    if (requestInfo == _requests.end())
    {
        return;
    }
    requestInfo->second.SocketDuration = timestamp - requestInfo->second.SocketConnectStartTime;
    requestInfo->second.Error = std::move(message);
}

void NetworkProvider::OnRequestHeaderStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId)
{
    NetworkActivity activity;
    if (!TryGetActivity(pActivityId, activity, false))
    {
        return;
    }
    auto requestInfo = _requests.find(activity);
    if (requestInfo == _requests.end())
    {
        return;
    }

    requestInfo->second.ReqRespStartTime = timestamp;
}

void NetworkProvider::OnRequestContentStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId)
{
    NetworkActivity activity;
    if (!TryGetActivity(pActivityId, activity, false))
    {
        return;
    }
    auto requestInfo = _requests.find(activity);
    if (requestInfo == _requests.end())
    {
        return;
    }

    requestInfo->second.ReqRespDuration = timestamp - requestInfo->second.ReqRespStartTime;
}


void NetworkProvider::FillRawSample(RawNetworkSample& sample, NetworkRequestInfo& info, std::chrono::nanoseconds timestamp)
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
    sample.DnsStartTimestamp = info.DnsStartTime;
    sample.DnsDuration = info.DnsDuration;
    sample.DnsSuccess = info.DnsResolutionSuccess;
    sample.HandshakeDuration = info.HandshakeDuration;
    sample.SocketConnectDuration = info.SocketDuration;
    sample.ReqRespDuration = info.ReqRespDuration;
}


bool NetworkProvider::TryGetActivity(LPCGUID pActivityId, NetworkActivity& activity, bool isRoot)
{
    return NetworkActivity::GetRootActivity(pActivityId, activity, isRoot);
}
