// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <string>
#include <vector>

#include "CallstackProvider.h"
#include "GarbageCollection.h"
#include "IConfiguration.h"
#include "LiveObjectsProvider.h"
#include "OpSysTools.h"
#include "Sample.h"
#include "SamplesEnumerator.h"
#include "SampleValueTypeProvider.h"

std::vector<SampleValueType> LiveObjectsProvider::SampleTypeDefinitions(
{
    {"inuse-objects", "count"},
    {"inuse-space", "bytes"}
});

const uint32_t MAX_LIVE_OBJECTS = 1024;

const std::string LiveObjectsProvider::Gen1("1");
const std::string LiveObjectsProvider::Gen2("2");

LiveObjectsProvider::LiveObjectsProvider(
    SampleValueTypeProvider& valueTypeProvider,
    ICorProfilerInfo13* pCorProfilerInfo,
    IManagedThreadList* pManagedThreadList,
    IFrameStore* pFrameStore,
    IThreadsCpuManager* pThreadsCpuManager,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore,
    IConfiguration* pConfiguration,
    MetricsRegistry& metricsRegistry)
    :
    _pCorProfilerInfo(pCorProfilerInfo),
    _isTimestampsAsLabelEnabled(pConfiguration->IsTimestampsAsLabelEnabled())
{
    _pAllocationsProvider = std::make_unique<AllocationsProvider>(
        valueTypeProvider.GetOrRegister(SampleTypeDefinitions),
        pCorProfilerInfo,
        pManagedThreadList,
        pFrameStore,
        pThreadsCpuManager,
        pAppDomainStore,
        pRuntimeIdStore,
        pConfiguration,
        nullptr,
        metricsRegistry,
        CallstackProvider(shared::pmr::null_memory_resource())); // safe to pass nullptr for the provider. This provider does not collect callstack
}

const char* LiveObjectsProvider::GetName()
{
    return "LiveObjectsProvider";
}

void LiveObjectsProvider::OnGarbageCollectionStart(
    uint64_t timestamp,
    int32_t number,
    uint32_t generation,
    GCReason reason,
    GCType type
)
{
    // The address provided during AllocationTick event is not pointing to real object
    // so we tried to wait for the next garbage collection to create a wrapping weak handle.
    // However, this triggered access violations during GCs...
    // Instead, the MethodTable is patched into memory in AllocationTick.
}

void LiveObjectsProvider::OnGarbageCollectionEnd(
    int32_t number,
    uint32_t generation,
    GCReason reason,
    GCType type,
    bool isCompacting,
    uint64_t pauseDuration,
    uint64_t totalDuration,
    uint64_t endTimestamp)
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

class LiveObjectsEnumerator : public SamplesEnumerator
{
public:
    LiveObjectsEnumerator(std::size_t size) :
        _currentPos{0}
    {
        _samples.reserve(size);
    }

    void Add(std::shared_ptr<Sample> sample)
    {
        _samples.push_back(std::move(sample));
    }

    // Inherited via SamplesEnumerator
    std::size_t size() const override
    {
        return _samples.size();
    }

    bool MoveNext(std::shared_ptr<Sample>& sample) override
    {
        if (_currentPos >= _samples.size())
            return false;

        sample = _samples[_currentPos++];
        return true;
    }

    std::vector<std::shared_ptr<Sample>> _samples;
    std::size_t _currentPos;
};

std::unique_ptr<SamplesEnumerator> LiveObjectsProvider::GetSamples()
{
    std::lock_guard<std::mutex> lock(_liveObjectsLock);

    int64_t currentTimestamp = OpSysTools::GetHighPrecisionTimestamp();
    std::size_t nbSamples = 0;

    // OPTIM maybe use an allocator
    auto samples = std::make_unique<LiveObjectsEnumerator>(_monitoredObjects.size());

    for (auto const& info : _monitoredObjects)
    {
        // gen2 objects are candidates for leaking however collections could be rare and only gen1 objects
        // are available for live heap profiling
        auto sample = info.GetSample();

        // update samples lifetime
        sample->ReplaceLabel(Label{Sample::ObjectLifetimeLabel, std::to_string(sample->GetTimeStamp() - currentTimestamp)});
        sample->ReplaceLabel(Label{Sample::ObjectGenerationLabel, info.IsGen2() ? Gen2 : Gen1});

        samples->Add(sample);
    }

    return samples;
}

void LiveObjectsProvider::OnAllocation(RawAllocationSample& rawSample)
{
    std::lock_guard<std::mutex> lock(_liveObjectsLock);

    // Limit the number of handle to create until the next GC
    // If _monitoredObjects is already full, stop adding new objects
    if (_monitoredObjects.size() < MAX_LIVE_OBJECTS)
    {
        // When the AllocationTick event is received, the object is not already initialized.
        // To call CreateWeakHandle(), it is needed to patch the MethodTable in memory
        *(uintptr_t*)rawSample.Address = rawSample.MethodTable;

        auto handle = CreateWeakHandle(rawSample.Address);
        if (handle != nullptr)
        {
            LiveObjectInfo info(
                _pAllocationsProvider->TransformRawSample(rawSample),
                rawSample.Address,
                rawSample.Timestamp);
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

    static ObjectID NullObjectID = static_cast<ObjectID>(NULL);

    auto object = NullObjectID;
    auto hr = _pCorProfilerInfo->GetObjectIDFromHandle(handle, &object);
    if (SUCCEEDED(hr))
    {
        return object != NullObjectID;
    }

    return false;
}

ObjectHandleID LiveObjectsProvider::CreateWeakHandle(uintptr_t address) const
{
    if (reinterpret_cast<void*>(address) == nullptr)
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
