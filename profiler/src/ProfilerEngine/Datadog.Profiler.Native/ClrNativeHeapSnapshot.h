// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "ClrNativeHeapInfo.h"
#include "IClrNativeHeapSnapshot.h"

#include <memory>
#include <mutex>
#include <vector>

class IRuntimeInfo;
class INativeHeapEnumerator;
class IAddressSpaceMap;

// Owns the DAC/cDAC native-heap enumerator (selected by runtime version, the same rule EEHeapReporter
// used) and the per-export OS address-space map. On the first GetSnapshot()/GetAddressSpaceMap() of an
// export it captures the map, injects it into the enumerator (so the card-table committed size reuses
// it instead of a second OS walk), and enumerates the CLR heaps. Both are cached until Invalidate().
class ClrNativeHeapSnapshot : public IClrNativeHeapSnapshot
{
public:
    ClrNativeHeapSnapshot(IRuntimeInfo* pRuntimeInfo, bool captureWorkingSet);
    ~ClrNativeHeapSnapshot() override;

    const std::vector<ClrNativeHeapInfo>& GetSnapshot() override;
    IAddressSpaceMap* GetAddressSpaceMap() override;
    void Invalidate() override;
    bool IsAvailable() override;
    const char* GetBackendName() override;

public: // for tests
    // Backend selection rule: .NET 11+ (and not .NET Framework) -> cDAC; everything earlier -> DAC.
    static bool ShouldUseCdac(IRuntimeInfo* pRuntimeInfo);

    // Injects a (fake) enumerator, bypassing the version-based factory. For tests only.
    void InjectEnumeratorForTest(std::unique_ptr<INativeHeapEnumerator> enumerator, const char* backendName = "fake");

    // Injects a (fake) address-space map, bypassing OS capture. For tests only.
    void InjectAddressSpaceMapForTest(std::unique_ptr<IAddressSpaceMap> map);

private:
    void EnsureAddressSpaceMap();
    void EnsureBackend();

    IRuntimeInfo* _pRuntimeInfo;
    bool _captureWorkingSet;

    std::mutex _lock;
    std::unique_ptr<IAddressSpaceMap> _map;             // captured once per export
    std::unique_ptr<INativeHeapEnumerator> _enumerator; // Cdac/Dac, created lazily
    const char* _backendName = "none";
    bool _backendCreated = false;
    std::vector<ClrNativeHeapInfo> _cache;
    bool _cacheValid = false;
    bool _mapCaptured = false;
};
