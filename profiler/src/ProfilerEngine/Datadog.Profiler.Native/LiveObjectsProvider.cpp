// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <vector>

#include "IConfiguration.h"
#include "LiveObjectsProvider.h"
#include "Sample.h"

std::vector<SampleValueType> LiveObjectsProvider::SampleTypeDefinitions(
    {{"inuse_objects", "count"},
     {"inuse_space", "bytes"}});


const uint32_t MAX_LIVE_OBJECTS = 1024;


LiveObjectsProvider::LiveObjectsProvider(
    uint32_t valueOffset,
    ICorProfilerInfo13* pCorProfilerInfo,
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

std::list<std::shared_ptr<Sample>> LiveObjectsProvider::GetSamples()
{
    // return the live object samples
    std::list<std::shared_ptr<Sample>> liveObjectsSamples;

    std::lock_guard<std::mutex> lock(_liveObjectsLock);
    for (auto const& info : _monitoredObjects)
    {
        liveObjectsSamples.push_back(info.GetSample());
    }

    return liveObjectsSamples;
}

void LiveObjectsProvider::OnAllocation(RawAllocationSample& rawSample)
{
    // std::cout << rawSample.AllocationClass << std::endl;

    std::lock_guard<std::mutex> lock(_liveObjectsLock);

    LiveObjectInfo info(
        _pAllocationsProvider.get()->TransformRawSample(rawSample),
        rawSample.Address);

    // Limit the number of handle to create until the next GC
    // If _objectsToMonitor is already full, stop adding new objects
    if (_objectsToMonitor.size() + _monitoredObjects.size() <= MAX_LIVE_OBJECTS)
    {
        _objectsToMonitor.push_back(std::move(info));
    }
}

void LiveObjectsProvider::OnGarbageCollectionStarted()
{
    std::lock_guard<std::mutex> lock(_liveObjectsLock);

    // address provided during AllocationTick event were not pointing to real objects
    // so we have to wait for the next garbage collection to create a wrapping weak handle
    for (auto& info : _objectsToMonitor)
    {
        auto handle = CreateWeakHandle(info.GetAddress());

        if (handle != nullptr)
        {
            info.SetHandle(handle);
        }
        else
        {
            // this should never happen
        }

    }

    _monitoredObjects.splice(_monitoredObjects.end(), _objectsToMonitor);
}

void LiveObjectsProvider::OnGarbageCollectionFinished()
{
    std::lock_guard<std::mutex> lock(_liveObjectsLock);

    // it is now time to check if the monitored allocated objects have been collected or are still alive
    _monitoredObjects.remove_if([this](LiveObjectInfo& info)
    {
        bool hasBeenCollected = !IsAlive(info.GetHandle());
        if (hasBeenCollected)
        {
            CloseWeakHandle(info.GetHandle());
        }
        return hasBeenCollected;
    });
}

bool LiveObjectsProvider::IsAlive(ObjectHandleID handle) const
{
    if (handle == nullptr)
    {
        return false;
    }

    ObjectID object = NULL;
    auto hr = _pCorProfilerInfo->GetObjectIDFromHandle(handle, &object);
    if (SUCCEEDED(hr))
    {
        return object != NULL;
    }

    return false;
}

ObjectHandleID LiveObjectsProvider::CreateWeakHandle(uintptr_t address) const
{
    if (address == NULL)
    {
        return nullptr;
    }

    ObjectHandleID handle = nullptr;
    auto hr = _pCorProfilerInfo->CreateHandle(address, COR_PRF_HANDLE_TYPE::COR_PRF_HANDLE_TYPE_WEAK, &handle);
    if (SUCCEEDED(hr))
    {
        return handle;
    }

    return nullptr;
}

void LiveObjectsProvider::CloseWeakHandle(ObjectHandleID handle) const
{
    if (handle == nullptr)
    {
        return;
    }

    _pCorProfilerInfo->DestroyHandle(handle);
}

bool LiveObjectsProvider::Start()
{
    return true;
}

bool LiveObjectsProvider::Stop()
{
    return true;
}
