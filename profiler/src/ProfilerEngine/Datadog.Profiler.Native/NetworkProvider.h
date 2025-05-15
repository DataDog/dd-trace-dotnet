// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CallstackProvider.h"
#include "CollectorBase.h"
#include "CounterMetric.h"
#include "INetworkListener.h"
#include "MeanMaxMetric.h"
#include "MetricsRegistry.h"
#include "NetworkActivity.h"
#include "NetworkRequestInfo.h"
#include "RawNetworkSample.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

#include <memory>
#include <unordered_map>

class SampleValueTypeProvider;
class IManagedThreadList;
class IThreadsCpuManager;
class IConfiguration;
class RawSampleTransformer;


class NetworkProvider :
    public CollectorBase<RawNetworkSample>,
    public INetworkListener
{
public:
    NetworkProvider(
        SampleValueTypeProvider& valueTypeProvider,
        ICorProfilerInfo4* pCorProfilerInfo,
        IManagedThreadList* pManagedThreadList,
        RawSampleTransformer* rawSampleTransformer,
        IConfiguration* pConfiguration,
        MetricsRegistry& metricsRegistry,
        CallstackProvider callstackProvider,
        shared::pmr::memory_resource* memoryResource);

public:
    // Inherited via INetworkListener
    void OnRequestStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string url) override;
    void OnRequestStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, uint32_t statusCode) override;
    void OnRequestFailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string message) override;
    void OnRedirect(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string redirectUrl) override;
    void OnDnsResolutionStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId) override;
    void OnDnsResolutionStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, bool Success) override;
    void OnConnectStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId) override;
    void OnConnectStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId) override;
    void OnConnectFailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string message) override;
    void OnHandshakeStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string targetHost) override;
    void OnHandshakeStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId) override;
    void OnHandshakeFailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string message) override;
    void OnRequestHeaderStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId) override;
    void OnResponseHeaderStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, uint32_t statusCode) override;
    void OnResponseContentStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId) override;
    void OnResponseContentStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId) override;

private:
    bool MonitorRequest(NetworkRequestInfo*& info, LPCGUID pActivityId, bool isRoot = true);
    bool CaptureThreadInfo(NetworkRequestInfo& info);
    void FillRawSample(RawNetworkSample& sample, NetworkRequestInfo& info, std::chrono::nanoseconds timestamp);
    void UpdateHandshakeDuration(NetworkRequestInfo* pInfo, std::chrono::nanoseconds timestamp);
    void UpdateHandshakeWait(NetworkRequestInfo* pInfo);
    bool TryGetActivity(LPCGUID pActivityId, NetworkActivity& activity, bool isRoot = true);

private:
    static std::vector<SampleValueType> SampleTypeDefinitions;

    ICorProfilerInfo4* _pCorProfilerInfo;
    IManagedThreadList* _pManagedThreadList;
    IFrameStore* _pFrameStore;
    IConfiguration const* const _pConfiguration;
    CallstackProvider _callstackProvider;
    MetricsRegistry& _metricsRegistry;
    std::chrono::nanoseconds _requestDurationThreshold;

    std::mutex _requestsLock;
    std::unordered_map<NetworkActivity, NetworkRequestInfo> _requests;

    // count ALL requests (success or failure)
    std::shared_ptr<CounterMetric> _requestsCountMetric;

    // count non successful requests
    std::shared_ptr<CounterMetric> _failedRequestsCountMetric;

    // count redirection requests
    std::shared_ptr<CounterMetric> _redirectionRequestsCountMetric;

    std::shared_ptr<MeanMaxMetric> _totalDurationMetric;
    std::shared_ptr<MeanMaxMetric> _waitDurationMetric;
    std::shared_ptr<MeanMaxMetric> _dnsDurationMetric;
    std::shared_ptr<MeanMaxMetric> _handshakeDurationMetric;
    std::shared_ptr<MeanMaxMetric> _requestResponseDurationMetric;


    bool IsRedirect(uint32_t statusCode)
    {
        return (
            (statusCode >= REDIRECT_MULTI_CHOICES) &&
            (statusCode <= REDIRECT_PERMANENT)
            );
    }

private:
// see https://developer.mozilla.org/en-US/docs/Web/HTTP/Status#redirection_messages
    const uint32_t REDIRECT_MULTI_CHOICES   = 300;
    const uint32_t REDIRECT_MOVED_PERMENTLY = 301;
    const uint32_t REDIRECT_FOUND           = 302;
    const uint32_t REDIRECT_SEE_OTHERS      = 303;
    const uint32_t REDIRECT_NOT_MODIFIED    = 304;
    // 305 and 306 are not more used
    const uint32_t REDIRECT_TEMPORARY       = 307;
    const uint32_t REDIRECT_PERMANENT       = 308;
};