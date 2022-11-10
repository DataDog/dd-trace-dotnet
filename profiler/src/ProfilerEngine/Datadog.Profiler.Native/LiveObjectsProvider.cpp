// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <vector>

#include "IConfiguration.h"
#include "LiveObjectsProvider.h"
#include "Sample.h"

std::vector<SampleValueType> LiveObjectsProvider::SampleTypeDefinitions(
    {{"inuse_objects", "count"},
     {"inuse_space", "bytes"}});


LiveObjectsProvider::LiveObjectsProvider(
    uint32_t valueOffset,
    ICorProfilerInfo4* pCorProfilerInfo,
    IFrameStore* pFrameStore,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore,
    IConfiguration* pConfiguration)
    :
    _valueOffset(valueOffset),
    _pCorProfilerInfo(pCorProfilerInfo),
    _pFrameStore(pFrameStore),
    _pAppDomainStore(pAppDomainStore),
    _pRuntimeIdStore(pRuntimeIdStore),
    _isTimestampsAsLabelEnabled(pConfiguration->IsTimestampsAsLabelEnabled())
{
}

const char* LiveObjectsProvider::GetName()
{
    return "LiveObjectsProvider";
}

void LiveObjectsProvider::OnAllocation(RawAllocationSample& rawSample)
{
    // TODO:
    // - create the corresponding Sample by transforming the RawAllocationSample
    // - add it into the objects to monitor list
}

std::list<Sample> LiveObjectsProvider::GetSamples()
{
    // TODO: return the live object samples
    return std::list<Sample>();
}

void LiveObjectsProvider::OnGarbageCollectionStarted()
{
    // TODO: create a WeakReference handle for each monitored allocation
}

void LiveObjectsProvider::OnGarbageCollectionFinished()
{
    // TODO: check monitored WeakReference handles and remove those no more referenced
}

bool LiveObjectsProvider::Start()
{
    return true;
}

bool LiveObjectsProvider::Stop()
{
    return true;
}
