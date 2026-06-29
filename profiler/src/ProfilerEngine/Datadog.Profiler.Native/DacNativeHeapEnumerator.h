// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "DacInterface.h"
#include "INativeHeapEnumerator.h"

// Forward declaration (defined by sospriv.h, isolated inside DacNativeHeapEnumerator.cpp).
struct ISOSDacInterface;

class IRuntimeInfo;

// DAC backend (pre-.NET 11 + .NET Framework): enumerates CLR native heaps via ISOSDacInterface - the
// same APIs SOS !eeheap and ClrMD's EnumerateClrNativeHeaps use. Order matches ClrMD:
// JIT code heaps -> AppDomain/Module loader + VCS stub heaps (deduped) -> GC segments.
class DacNativeHeapEnumerator : public INativeHeapEnumerator
{
public:
    explicit DacNativeHeapEnumerator(IRuntimeInfo* pRuntimeInfo);
    ~DacNativeHeapEnumerator() override = default;

    std::vector<ClrNativeHeapInfo> EnumerateAll() override;
    bool IsAvailable() const override;

private:
    DacInterface _dac;
    bool _available = false;
};

namespace dac
{
// Core enumeration over a (possibly fake) ISOSDacInterface. Factored out so unit tests can drive it
// with a fake ISOSDacInterface without loading a real DAC. Every SOS call is HRESULT-checked; a
// failing call degrades to "skip that source".
std::vector<ClrNativeHeapInfo> EnumerateNativeHeapsFromSos(ISOSDacInterface* sos);
} // namespace dac
