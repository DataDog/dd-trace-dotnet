// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "INativeHeapEnumerator.h"
#include "InProcessMemoryReader.h"

#include <memory>

namespace cdac
{
class Target;
}

// cDAC backend (.NET 11+): enumerates CLR native heaps by reading the runtime's
// DotNetRuntimeContractDescriptor directly (no DAC, no ClrMD). Composes the Loader and GC contracts,
// in the same order ClrMD uses (code heaps -> loader heaps deduped -> GC regions). Each source is
// guarded so one corrupt structure degrades to "skip".
class CdacNativeHeapEnumerator : public INativeHeapEnumerator
{
public:
    CdacNativeHeapEnumerator();
    ~CdacNativeHeapEnumerator() override;

    std::vector<ClrNativeHeapInfo> EnumerateAll() override;
    bool IsAvailable() const override;

private:
    InProcessMemoryReader _reader;
    std::unique_ptr<cdac::Target> _target;
    bool _available = false;
};
