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
    IManagedThreadList* pManagedThreadList,
    IFrameStore* pFrameStore,
    IThreadsCpuManager* pThreadsCpuManager,
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
    _pAllocationsProvider = std::make_unique<AllocationsProvider>(
        valueOffset,  // the values (allocation count and size are stored in the live object values, not tha allocation values)
        pCorProfilerInfo,
        pManagedThreadList,
        pFrameStore,
        pThreadsCpuManager,
        pAppDomainStore,
        pRuntimeIdStore,
        pConfiguration,
        nullptr);
}

const char* LiveObjectsProvider::GetName()
{
    return "LiveObjectsProvider";
}

void LiveObjectsProvider::OnAllocation(RawAllocationSample& rawSample)
{
    // std::cout << rawSample.AllocationClass << std::endl;

    LiveObjectInfo info(
        _pAllocationsProvider.get()->TransformRawSample(rawSample),
        rawSample.Address
        );
    _objectsToMonitor.push_back(std::move(info));
}

std::list<Sample> LiveObjectsProvider::GetSamples()
{
    // TODO: return the live object samples
    return std::list<Sample>();
}

void LiveObjectsProvider::OnGarbageCollectionStarted()
{
    // address provided during AllocationTick event were not pointing to real objects
    // so we have to wait for the next garbage collection to create a wrapping weak handle
    for (auto& info : _objectsToMonitor)
    {
        info.SetHandle(CreateWeakHandle(info.GetAddress()));
    }

    _monitoredObjects.splice(_monitoredObjects.end(), _objectsToMonitor);
}

void** LiveObjectsProvider::CreateWeakHandle(uintptr_t address)
{
    // create WeakHandle with ICorProfilerInfo13
    return nullptr;
}

void LiveObjectsProvider::OnGarbageCollectionFinished()
{
    _monitoredObjects.remove_if([this](LiveObjectInfo& info)
    {
        return !IsAlive(info.GetHandle());
    });
}

bool LiveObjectsProvider::Start()
{
    return true;
}

bool LiveObjectsProvider::Stop()
{
    return true;
}

bool LiveObjectsProvider::IsAlive(void** handle)
{
    // TODO: check WeakHandle with ICorProfilerInfo13
    return false;
}