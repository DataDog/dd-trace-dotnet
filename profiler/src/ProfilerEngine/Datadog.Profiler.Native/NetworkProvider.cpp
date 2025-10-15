// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "NetworkProvider.h"

#include "COMHelpers.h"
#include "IManagedThreadList.h"
#include "Log.h"
#include "OsSpecificApi.h"
#include "RawSampleTransformer.h"

#include <chrono>

std::vector<SampleValueType> NetworkProvider::SampleTypeDefinitions(
{
    {"request-time", "nanoseconds", -1}
});


NetworkProvider::NetworkProvider(
    SampleValueTypeProvider& valueTypeProvider,
    ICorProfilerInfo4* pCorProfilerInfo,
    IManagedThreadList* pManagedThreadList,
    RawSampleTransformer* rawSampleTransformer,
    IConfiguration* pConfiguration,
    MetricsRegistry& metricsRegistry,
    CallstackProvider callstackProvider,
    shared::pmr::memory_resource* memoryResource)
    :
    CollectorBase<RawNetworkSample>(
        "NetworkProvider",
        valueTypeProvider.GetOrRegister(SampleTypeDefinitions),
        rawSampleTransformer,
        memoryResource),
    _pCorProfilerInfo{ pCorProfilerInfo },
    _pManagedThreadList{ pManagedThreadList },
    _pConfiguration{ pConfiguration },
    _callstackProvider{ std::move(callstackProvider) },
    _metricsRegistry{metricsRegistry}
{
    // all other durations in the code are in nanoseconds but the config is in milliseconds
    _requestDurationThreshold = std::chrono::duration_cast<std::chrono::nanoseconds>(pConfiguration->GetHttpRequestDurationThreshold());

    _requestsCountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_request_all");
    _failedRequestsCountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_request_failed");
    _redirectionRequestsCountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_request_redirect");
    _totalDurationMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_request_duration");
    _waitDurationMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_request_wait_duration");
    _dnsDurationMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_request_dns_duration");
    _handshakeDurationMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_request_handshake_duration");
    _requestResponseDurationMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_request_response_duration");
}

bool NetworkProvider::CaptureThreadInfo(NetworkRequestInfo& info)
{
    auto threadInfo = ManagedThreadInfo::CurrentThreadInfo;
    if (threadInfo == nullptr)
    {
        LogOnce(Warn, "NetworkProvider::CaptureThreadInfo: Profiler failed at getting the current managed thread info ");
        return false;
    }

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
    info.StartThreadInfo = std::move(threadInfo);

    return true;
}

void NetworkProvider::OnRequestStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string url)
{
    NetworkActivity activity;
    if (!TryGetActivity(pActivityId, activity))
    {
        return;
    }

    std::lock_guard<std::mutex> lock(_requestsLock);

    auto existingInfo = _requests.find(activity);
    if (existingInfo != _requests.end())
    {
        // TODO: this should never happen; i.e. a request with the same activity is already in progress
        _requests.erase(activity);

        // we start a new request with the same activity to avoid losing both the previous and the current one
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

    std::lock_guard<std::mutex> lock(_requestsLock);

    auto requestInfo = _requests.find(activity);
    if (requestInfo == _requests.end())
    {
        // skipped request
        return;
    }

    // sampling can be forced for tests
    if (!_pConfiguration->ForceHttpSampling())
    {
        // requests lasting less than a threshold are skipped
        if ((timestamp - requestInfo->second.StartTimestamp) < _requestDurationThreshold)
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
    _requestsCountMetric->Incr();

    rawSample.StatusCode = statusCode;
    if ((statusCode < 200) || (statusCode > 299))
    {
        _failedRequestsCountMetric->Incr();
    }

    rawSample.Error = std::move(requestInfo->second.Error);
    rawSample.HandshakeError = std::move(requestInfo->second.HandshakeError);

    if (requestInfo->second.Redirect != nullptr)
    {
        // will be an empty string for .NET 7
        rawSample.RedirectUrl = std::move(requestInfo->second.Redirect->Url);
        rawSample.HasBeenRedirected = true;

        _redirectionRequestsCountMetric->Incr();
    }

    Add(std::move(rawSample));

    _requests.erase(activity);
}

// OnRequestStop will be called AFTER this one so the sample is created in one place
void NetworkProvider::OnRequestFailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string message)
{
    NetworkRequestInfo* pInfo = nullptr;
    if (!MonitorRequest(pInfo, pActivityId))
    {
        return;
    }

    pInfo->Error = std::move(message);
}

// received in .NET 8+
void NetworkProvider::OnRedirect(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string redirectUrl)
{
    NetworkRequestInfo* pInfo = nullptr;
    if (!MonitorRequest(pInfo, pActivityId))
    {
        return;
    }

    if (pInfo->Redirect == nullptr)
    {
        // this should never happen because Redirect is created in OnRequestHeaderStop
        return;
    }

    // To support more than 1 redirection, concatenate the redirect urls with a separator
    if (pInfo->Redirect->Url.empty())
    {
        pInfo->Redirect->Url = std::move(redirectUrl);
    }
    else
    {
        pInfo->Redirect->Url += " | " + std::move(redirectUrl);
    }
}

void NetworkProvider::OnDnsResolutionStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId)
{
    NetworkRequestInfo* pInfo = nullptr;
    if (!MonitorRequest(pInfo, pActivityId, false))
    {
        return;
    }

    if (pInfo->Redirect == nullptr)
    {
        pInfo->DnsStartTime = timestamp;
    }
    else
    {
        pInfo->Redirect->DnsStartTime = timestamp;
    }
}

void NetworkProvider::OnDnsResolutionStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, bool success)
{
    NetworkRequestInfo* pInfo = nullptr;
    if (!MonitorRequest(pInfo, pActivityId, false))
    {
        return;
    }

    // DNS is the first phase...
    if (pInfo->Redirect == nullptr)
    {
        // ... of the first part of the request
        pInfo->DnsDuration = timestamp - pInfo->DnsStartTime;
        pInfo->DnsWait = pInfo->DnsStartTime - pInfo->StartTimestamp;
    }
    else
    {
        // ... of the redirected part of the request
        pInfo->Redirect->DnsDuration = timestamp - pInfo->Redirect->DnsStartTime;
        pInfo->Redirect->DnsWait = pInfo->Redirect->DnsStartTime - pInfo->Redirect->StartTimestamp;
    }

    // even if the first request succeeds, the redirected one might fail
    pInfo->DnsResolutionSuccess = success;
}

void NetworkProvider::OnConnectStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId)
{
    NetworkRequestInfo* pInfo = nullptr;
    if (!MonitorRequest(pInfo, pActivityId, false))
    {
        return;
    }

    if (pInfo->Redirect == nullptr)
    {
        pInfo->SocketConnectStartTime = timestamp;
    }
    else
    {
        pInfo->Redirect->SocketConnectStartTime = timestamp;
    }
}

void NetworkProvider::OnConnectStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId)
{
    NetworkRequestInfo* pInfo = nullptr;
    if (!MonitorRequest(pInfo, pActivityId, false))
    {
        return;
    }

    if (pInfo->Redirect == nullptr)
    {
        pInfo->SocketDuration = timestamp - pInfo->SocketConnectStartTime;
    }
    else
    {
        pInfo->Redirect->SocketDuration = timestamp - pInfo->Redirect->SocketConnectStartTime;
    }
}

void NetworkProvider::OnConnectFailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string message)
{
    NetworkRequestInfo* pInfo = nullptr;
    if (!MonitorRequest(pInfo, pActivityId, false))
    {
        return;
    }

    if (pInfo->Redirect == nullptr)
    {
        pInfo->SocketDuration = timestamp - pInfo->SocketConnectStartTime;
    }
    else
    {
        pInfo->Redirect->SocketDuration = timestamp - pInfo->Redirect->SocketConnectStartTime;
    }

    // TODO: check if we should rely on the RequestFailed event to set the error message
    pInfo->Error = std::move(message);
}

void NetworkProvider::OnRequestHeaderStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId)
{
    NetworkRequestInfo* pInfo = nullptr;
    if (!MonitorRequest(pInfo, pActivityId, false))
    {
        return;
    }

    if (pInfo->Redirect == nullptr)
    {
        pInfo->RequestHeadersStartTimestamp = timestamp;
    }
    else
    {
        pInfo->Redirect->RequestHeadersStartTimestamp = timestamp;
    }
}

void NetworkProvider::OnResponseHeaderStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, uint32_t statusCode)
{
    NetworkRequestInfo* pInfo = nullptr;
    if (!MonitorRequest(pInfo, pActivityId, false))
    {
        return;
    }

    if (pInfo->Redirect == nullptr)
    {
        pInfo->RequestDuration = timestamp - pInfo->RequestHeadersStartTimestamp;

        if (IsRedirect(statusCode))
        {
            // the redirect url will be "" for .NET 7 because the Redirect event is not emitted
            pInfo->Redirect = std::make_unique<NetworkRequestInfo>("", timestamp);
        }
    }
    else
    {
        // support more than 1 redirection; i.e. accumulate the duration of all redirections
        pInfo->Redirect->RequestDuration += timestamp - pInfo->Redirect->RequestHeadersStartTimestamp;
    }

}

void NetworkProvider::OnResponseContentStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId)
{
    NetworkRequestInfo* pInfo = nullptr;
    if (!MonitorRequest(pInfo, pActivityId, false))
    {
        return;
    }

    if (pInfo->Redirect == nullptr)
    {
        pInfo->ResponseContentStartTimestamp = timestamp;
    }
    else
    {
        pInfo->Redirect->ResponseContentStartTimestamp = timestamp;
    }
}


void NetworkProvider::OnResponseContentStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId)
{
    NetworkRequestInfo* pInfo = nullptr;
    if (!MonitorRequest(pInfo, pActivityId, false))
    {
        return;
    }

    if (pInfo->Redirect == nullptr)
    {
        pInfo->ResponseDuration = timestamp - pInfo->ResponseContentStartTimestamp;
    }
    else
    {
        pInfo->Redirect->ResponseDuration = timestamp - pInfo->Redirect->ResponseContentStartTimestamp;
    }
}

void NetworkProvider::OnHandshakeStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string targetHost)
{
    NetworkRequestInfo* pInfo = nullptr;
    if (!MonitorRequest(pInfo, pActivityId, false))
    {
        return;
    }

    if (pInfo->Redirect == nullptr)
    {
        pInfo->HandshakeStartTime = timestamp;
    }
    else
    {
        pInfo->Redirect->HandshakeStartTime = timestamp;
    }
}

void NetworkProvider::OnHandshakeStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId)
{
    NetworkRequestInfo* pInfo = nullptr;
    if (!MonitorRequest(pInfo, pActivityId, false))
    {
        return;
    }

    UpdateHandshakeDuration(pInfo, timestamp);
    UpdateHandshakeWait(pInfo);
}

void NetworkProvider::OnHandshakeFailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string message)
{
    NetworkRequestInfo* pInfo = nullptr;
    if (!MonitorRequest(pInfo, pActivityId, false))
    {
        return;
    }

    UpdateHandshakeDuration(pInfo, timestamp);
    UpdateHandshakeWait(pInfo);

    pInfo->HandshakeError = std::move(message);
}

void NetworkProvider::UpdateHandshakeDuration(NetworkRequestInfo* pInfo, std::chrono::nanoseconds timestamp)
{
    if (pInfo->Redirect == nullptr)
    {
        pInfo->HandshakeDuration = timestamp - pInfo->HandshakeStartTime;
    }
    else
    {
        pInfo->HandshakeDuration = timestamp - pInfo->Redirect->HandshakeStartTime;
    }
}


void NetworkProvider::UpdateHandshakeWait(NetworkRequestInfo* pInfo)
{
    // phases order: Start --> DNS --> socket --> handshake
    // we need to take into account situations where DNS/socket phases might be missing
    if (pInfo->Redirect == nullptr)
    {
        if (pInfo->SocketConnectStartTime != 0ns)
        {
            pInfo->HandshakeWait =        // = socket end time
                pInfo->HandshakeStartTime - (pInfo->SocketConnectStartTime + pInfo->SocketDuration);
        }
        else
        if (pInfo->DnsStartTime != 0ns)
        {
            pInfo->HandshakeWait =        // = DNS end time
                pInfo->HandshakeStartTime - (pInfo->DnsStartTime + pInfo->DnsDuration);
        }
        else
        {
            pInfo->HandshakeWait = pInfo->HandshakeStartTime - pInfo->StartTimestamp;
        }
    }
    else
    {
        if (pInfo->Redirect->SocketConnectStartTime != 0ns)
        {
            pInfo->Redirect->HandshakeWait =        // = socket end time
                pInfo->Redirect->HandshakeStartTime - (pInfo->Redirect->SocketConnectStartTime + pInfo->Redirect->SocketDuration);
        }
        else
        if (pInfo->Redirect->DnsStartTime != 0ns)
        {
            pInfo->Redirect->HandshakeWait =        // = DNS end time
                pInfo->Redirect->HandshakeStartTime - (pInfo->Redirect->DnsStartTime + pInfo->Redirect->DnsDuration);
        }
        else
        {
            pInfo->Redirect->HandshakeWait = pInfo->Redirect->HandshakeStartTime - pInfo->Redirect->StartTimestamp;
        }
    }
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
    sample.EndThreadName = currentThreadInfo->GetProfileThreadName();
    sample.DnsSuccess = info.DnsResolutionSuccess;

    // count the durations for the initial request...
    sample.DnsDuration = info.DnsDuration;
    sample.DnsWait = info.DnsWait;
    sample.HandshakeDuration = info.HandshakeDuration;
    sample.HandshakeWait = info.HandshakeWait;
    sample.SocketConnectDuration = info.SocketDuration;
    sample.RequestDuration = info.RequestDuration;
    sample.ResponseDuration = info.ResponseDuration;

    // ...plus the redirected requests if any
    if (info.Redirect != nullptr)
    {
        sample.DnsDuration += info.Redirect->DnsDuration;
        sample.DnsWait += info.Redirect->DnsWait;
        sample.HandshakeDuration += info.Redirect->HandshakeDuration;
        sample.HandshakeWait += info.Redirect->HandshakeWait;
        sample.SocketConnectDuration += info.Redirect->SocketDuration;
        sample.RequestDuration += info.Redirect->RequestDuration;
        sample.ResponseDuration += info.Redirect->ResponseDuration;

        // The computation of the wait/queueing time is based on the time difference
        // between end of a phase and the start of the next one
        // --> high values usually reflect the lack of ThreadPool threads availability
        // During the tests, the wait time before the socket connection is very short so no need to provide it
    }

    _totalDurationMetric->Add(static_cast<double_t>((sample.Timestamp - sample.StartTimestamp).count()));
    _waitDurationMetric->Add(static_cast<double_t>((sample.DnsWait + sample.HandshakeWait).count()));
    _dnsDurationMetric->Add(static_cast<double_t>(sample.DnsDuration.count()));
    _handshakeDurationMetric->Add(static_cast<double_t>(sample.HandshakeDuration.count()));
    _requestResponseDurationMetric->Add(static_cast<double_t>((sample.RequestDuration + sample.ResponseDuration).count()));
}


// This helper function is called with isRoot = true for Start/Stop events handlers and false otherwise
bool NetworkProvider::TryGetActivity(LPCGUID pActivityId, NetworkActivity& activity, bool isRoot)
{
    return NetworkActivity::GetRootActivity(pActivityId, activity, isRoot);
}

bool NetworkProvider::MonitorRequest(NetworkRequestInfo*& pInfo, LPCGUID pActivityId, bool isRoot)
{
    NetworkActivity activity;
    if (!TryGetActivity(pActivityId, activity, isRoot))
    {
        return false;
    }

    std::lock_guard<std::mutex> lock(_requestsLock);

    auto existingInfo = _requests.find(activity);
    if (existingInfo == _requests.end())
    {
        return false;
    }

    pInfo = &(existingInfo->second);

    return true;
}

