// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "NetworkProvider.h"

#include "IManagedThreadList.h"

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


void NetworkProvider::OnRequestStart(uint64_t timestamp, LPCGUID pActivityId, std::string url)
{
    NetworkActivity activity;
    if (!TryGetActivity(pActivityId, activity))
    {
        return;
    }

    BYTE* pActivityIdBytes = reinterpret_cast<BYTE*>(&activity);
}

void NetworkProvider::OnRequestStop(uint64_t timestamp, LPCGUID pActivityId, uint32_t statusCode)
{

}

void NetworkProvider::OnRequestFailed(uint64_t timestamp, LPCGUID pActivityId, std::string message)
{

}



bool NetworkProvider::TryGetActivity(LPCGUID pActivityId, NetworkActivity& activity)
{
    return false;
}
