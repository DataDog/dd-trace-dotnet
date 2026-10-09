// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ClrNativeHeapSnapshot.h"

#include "CdacNativeHeapEnumerator.h"
#include "DacNativeHeapEnumerator.h"
#include "IAddressSpaceMap.h"
#include "INativeHeapEnumerator.h"
#include "IRuntimeInfo.h"
#include "Log.h"
#include "OsSpecificApi.h"

ClrNativeHeapSnapshot::ClrNativeHeapSnapshot(IRuntimeInfo* pRuntimeInfo) :
    _pRuntimeInfo{pRuntimeInfo}
{
}

ClrNativeHeapSnapshot::~ClrNativeHeapSnapshot() = default;

bool ClrNativeHeapSnapshot::ShouldUseCdac(IRuntimeInfo* pRuntimeInfo)
{
    return (pRuntimeInfo != nullptr) &&
           !pRuntimeInfo->IsDotnetFramework() &&
           (pRuntimeInfo->GetMajorVersion() >= 11);
}

void ClrNativeHeapSnapshot::InjectEnumeratorForTest(std::unique_ptr<INativeHeapEnumerator> enumerator, const char* backendName)
{
    std::lock_guard<std::mutex> lock(_lock);
    _backendCreated = true;
    _backendName = backendName;
    _enumerator = std::move(enumerator);
}

void ClrNativeHeapSnapshot::InjectAddressSpaceMapForTest(std::unique_ptr<IAddressSpaceMap> map)
{
    std::lock_guard<std::mutex> lock(_lock);
    _mapCaptured = true;
    _map = std::move(map);
}

void ClrNativeHeapSnapshot::EnsureAddressSpaceMap()
{
    if (_mapCaptured)
    {
        return;
    }
    _mapCaptured = true;
    _map = OsSpecificApi::CaptureAddressSpaceMap();
}

void ClrNativeHeapSnapshot::EnsureBackend()
{
    if (_backendCreated)
    {
        return;
    }
    _backendCreated = true;

    // .NET 11+ (and not .NET Framework) -> cDAC contracts; everything earlier -> the DAC.
    if (ShouldUseCdac(_pRuntimeInfo))
    {
        _backendName = "cdac";
        _enumerator = std::make_unique<CdacNativeHeapEnumerator>(_map.get());
    }
    else
    {
        _backendName = "dac";
        _enumerator = std::make_unique<DacNativeHeapEnumerator>(_pRuntimeInfo, _map.get());
    }

    if (_enumerator == nullptr || !_enumerator->IsAvailable())
    {
        Log::Info("ClrNativeHeapSnapshot: native-heap backend unavailable; no CLR heap detail will be produced.");
        _enumerator.reset();
    }
}

const std::vector<ClrNativeHeapInfo>& ClrNativeHeapSnapshot::GetSnapshot()
{
    std::lock_guard<std::mutex> lock(_lock);

    if (_cacheValid)
    {
        return _cache;
    }

    EnsureAddressSpaceMap();
    EnsureBackend();

    _cache.clear();
    if (_enumerator != nullptr)
    {
        _cache = _enumerator->EnumerateAll();
    }
    _cacheValid = true;

    return _cache;
}

IAddressSpaceMap* ClrNativeHeapSnapshot::GetAddressSpaceMap()
{
    std::lock_guard<std::mutex> lock(_lock);
    EnsureAddressSpaceMap();
    return _map.get();
}

void ClrNativeHeapSnapshot::Invalidate()
{
    std::lock_guard<std::mutex> lock(_lock);
    _cache.clear();
    _cacheValid = false;
    _map.reset();
    _mapCaptured = false;
    // Keep the enumerator: only the DAC/cDAC descriptor discovery is expensive; re-enumeration reads
    // fresh data each cycle. But the enumerator holds a raw pointer to the (now-freed) map, so drop it
    // too and let EnsureBackend rebuild it against the next export's map.
    _enumerator.reset();
    _backendCreated = false;
}

bool ClrNativeHeapSnapshot::IsAvailable()
{
    std::lock_guard<std::mutex> lock(_lock);
    EnsureAddressSpaceMap();
    EnsureBackend();
    return _enumerator != nullptr;
}

const char* ClrNativeHeapSnapshot::GetBackendName()
{
    std::lock_guard<std::mutex> lock(_lock);
    return _backendName;
}
