// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <string>
#include <vector>

#include "GarbageCollection.h"
#include "IConfiguration.h"
#include "LiveObjectsProvider.h"
#include "OpSysTools.h"
#include "Sample.h"

std::vector<SampleValueType> LiveObjectsProvider::SampleTypeDefinitions(
{
    {"inuse-objects", "count"},
    {"inuse-space", "bytes"}
});


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

void LiveObjectsProvider::OnGarbageCollectionStart(
    int32_t number,
    uint32_t generation,
    GCReason reason,
    GCType type
    )
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

void LiveObjectsProvider::OnGarbageCollectionEnd(
    int32_t number,
    uint32_t generation,
    GCReason reason,
    GCType type,
    bool isCompacting,
    uint64_t pauseDuration,
    uint64_t totalDuration,
    uint64_t endTimestamp
    )
{
    std::lock_guard<std::mutex> lock(_liveObjectsLock);

    // it is now time to check if the monitored allocated objects have been collected or are still alive
    _monitoredObjects.remove_if([this](LiveObjectInfo& info) {
        bool hasBeenCollected = !IsAlive(info.GetHandle());
        if (hasBeenCollected)
        {
            CloseWeakHandle(info.GetHandle());
        }
        else
        {
            info.IncrementGC();
        }
        return hasBeenCollected;
    });
}

std::list<std::shared_ptr<Sample>> LiveObjectsProvider::GetSamples()
{
    // return the live object samples
    std::list<std::shared_ptr<Sample>> liveObjectsSamples;

    std::lock_guard<std::mutex> lock(_liveObjectsLock);
    for (auto const& info : _monitoredObjects)
    {
        // only gen2 objects are candidates for leaking: gen0 and gen1 could be transient
        if (info.IsGen2())
        {
            liveObjectsSamples.push_back(info.GetSample());
        }
    }

    // update samples lifetime
    int64_t currentTimestamp = OpSysTools::GetHighPrecisionTimestamp();
    for (auto& sample : liveObjectsSamples)
    {
        sample->ReplaceLastLabel(Label{Sample::ObjectLifetimeLabel, std::to_string(sample->GetTimeStamp() - currentTimestamp)});
    }

    return liveObjectsSamples;
}

void LiveObjectsProvider::OnAllocation(RawAllocationSample& rawSample)
{
    std::lock_guard<std::mutex> lock(_liveObjectsLock);

    LiveObjectInfo info(
        _pAllocationsProvider->TransformRawSample(rawSample),
        rawSample.Address,
        rawSample.Timestamp);

    // Limit the number of handle to create until the next GC
    // If _objectsToMonitor is already full, stop adding new objects
    if (_objectsToMonitor.size() + _monitoredObjects.size() < MAX_LIVE_OBJECTS)
    {
        //_objectsToMonitor.push_back(std::move(info));

        // When the AllocationTick event is received, the object is not already initialized.
        // To call CreateWeakHandle(), it is needed to patch the MethodTable in memory
        *(uintptr_t*)rawSample.Address = rawSample.MethodTable;

        auto handle = CreateWeakHandle(rawSample.Address);
        if (handle != nullptr)
        {
            info.SetHandle(handle);
            _monitoredObjects.push_back(std::move(info));
        }
        else
        {
            // this should never happen
        }

    }
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
