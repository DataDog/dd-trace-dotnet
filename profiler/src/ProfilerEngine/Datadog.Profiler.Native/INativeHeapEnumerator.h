// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "ClrNativeHeapInfo.h"

#include <vector>

// Single seam over the two interchangeable native-heap backends:
//  - CdacNativeHeapEnumerator (cDAC contracts, .NET 11+)
//  - DacNativeHeapEnumerator  (ISOSDacInterface, pre-.NET 11 and .NET Framework)
//
// Implementations must never throw or destabilize the process: on any uncertainty they
// return an empty (or partial) vector.
class INativeHeapEnumerator
{
public:
    virtual ~INativeHeapEnumerator() = default;

    // Enumerate every CLR native heap (JIT code heaps, loader heaps deduped, GC native regions),
    // in the same order ClrMD uses.
    virtual std::vector<ClrNativeHeapInfo> EnumerateAll() = 0;

    // Whether the backend successfully initialized and can enumerate. When false, the reporter
    // produces no eeheap.json (the feature silently no-ops).
    virtual bool IsAvailable() const = 0;
};
