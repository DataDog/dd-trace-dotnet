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
#include "SumMetric.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

#include <memory>
#include <unordered_map>

class SampleValueTypeProvider;
class IManagedThreadList;
class IFrameStore;
class IThreadsCpuManager;
class IAppDomainStore;
class IRuntimeIdStore;
class IConfiguration;


class NetworkProvider :
    public CollectorBase<RawNetworkSample>,
    public INetworkListener
{
public:
    NetworkProvider(
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
        shared::pmr::memory_resource* memoryResource);

public:
    // Inherited via INetworkListener
    void OnRequestStart(uint64_t timestamp, LPCGUID pActivityId, std::string url) override;
    void OnRequestStop(uint64_t timestamp, LPCGUID pActivityId, uint32_t statusCode) override;
    void OnRequestFailed(uint64_t timestamp, LPCGUID pActivityId, std::string message) override;
    void OnRedirect(uint64_t timestamp, LPCGUID pActivityId, std::string redirectUrl) override;
    void OnDnsResolutionStart(uint64_t timestamp, LPCGUID pActivityId) override;
    void OnDnsResolutionStop(uint64_t timestamp, LPCGUID pActivityId, bool Success) override;
    void OnConnectStart(uint64_t timestamp, LPCGUID pActivityId) override;
    void OnConnectStop(uint64_t timestamp, LPCGUID pActivityId) override;
    void OnConnectFailed(uint64_t timestamp, LPCGUID pActivityId, std::string message) override;

private:
    bool TryGetActivity(LPCGUID pActivityId, NetworkActivity& activity, bool isRoot = true);
    bool CaptureThreadInfo(NetworkRequestInfo& info);
    void FillRawSample(RawNetworkSample& sample, NetworkRequestInfo& info, uint64_t timestamp);

private:
    static std::vector<SampleValueType> SampleTypeDefinitions;

    ICorProfilerInfo4* _pCorProfilerInfo;
    IManagedThreadList* _pManagedThreadList;
    IFrameStore* _pFrameStore;
    IConfiguration const* const _pConfiguration;
    CallstackProvider _callstackProvider;
    MetricsRegistry& _metricsRegistry;

    std::unordered_map<NetworkActivity, NetworkRequestInfo> _requests;
};