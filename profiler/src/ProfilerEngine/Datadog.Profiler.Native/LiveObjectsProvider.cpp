// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <string>
#include <vector>

#include "CallstackProvider.h"
#include "GarbageCollection.h"
#include "IConfiguration.h"
#include "LiveObjectsProvider.h"
#include "OpSysTools.h"
#include "RawSampleTransformer.h"
#include "Sample.h"
#include "SamplesEnumerator.h"
#include "SampleValueTypeProvider.h"
#include "SymbolsStore.h"

std::vector<SampleValueType> LiveObjectsProvider::SampleTypeDefinitions(
{
    {"inuse-objects", "count", -1},
    {"inuse-space", "bytes", -1}
});

const uint32_t MAX_LIVE_OBJECTS = 1024;

const std::string LiveObjectsProvider::Gen1("1");
const std::string LiveObjectsProvider::Gen2("2");

LiveObjectsProvider::LiveObjectsProvider(
    ICorProfilerInfo13* pCorProfilerInfo,
    SampleValueTypeProvider& valueTypeProvider,
    RawSampleTransformer* rawSampleTransformer,
    IConfiguration* pConfiguration,
    libdatadog::SymbolsStore* symbolsStore)
    :
    _pCorProfilerInfo(pCorProfilerInfo),
    _rawSampleTransformer{rawSampleTransformer},
    _valueOffsets{valueTypeProvider.GetOrRegister(LiveObjectsProvider::SampleTypeDefinitions)},
    _symbolsStore{symbolsStore}
{
}

const char* LiveObjectsProvider::GetName()
{
    return "LiveObjectsProvider";
}

void LiveObjectsProvider::OnGarbageCollectionStart(
    std::chrono::nanoseconds timestamp,
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
    std::chrono::nanoseconds pauseDuration,
    std::chrono::nanoseconds totalDuration,
    std::chrono::nanoseconds endTimestamp,
    uint64_t gen2Size,
    uint64_t lohSize,
    uint64_t pohSize)
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

    auto currentTimestamp = OpSysTools::GetHighPrecisionTimestamp();
    std::size_t nbSamples = 0;

    // OPTIM maybe use an allocator
    auto samples = std::make_unique<LiveObjectsEnumerator>(_monitoredObjects.size());

    for (auto const& info : _monitoredObjects)
    {
        // gen2 objects are candidates for leaking however collections could be rare and only gen1 objects
        // are available for live heap profiling
        auto sample = info.GetSample();

        // update samples lifetime
        sample->ReplaceLabel(StringLabel{_symbolsStore->GetObjectLifetime(), std::to_string((sample->GetTimeStamp() - currentTimestamp).count())});
        sample->ReplaceLabel(StringLabel{_symbolsStore->GetObjectGeneration(), info.IsGen2() ? Gen2 : Gen1});

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
                _rawSampleTransformer->Transform(rawSample, _valueOffsets, _symbolsStore),
                rawSample.Address,
                rawSample.Timestamp,
                _symbolsStore);
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

bool LiveObjectsProvider::StartImpl()
{
    return true;
}

bool LiveObjectsProvider::StopImpl()
{
    return true;
}
