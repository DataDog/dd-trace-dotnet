// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>

// Produces the eeheap.json report (a from-scratch SOS !eeheap) attached to each profiling export.
// Implemented by EEHeapReporter, which dispatches to the cDAC (.NET 11+) or DAC (pre-.NET 11)
// native-heap backend.
class IEEHeapReporter
{
public:
    virtual ~IEEHeapReporter() = default;

    // Enumerates the CLR native heaps now and returns the eeheap.json content. Returns an empty
    // string when the backend is unavailable or produced no heaps (the feature silently no-ops).
    virtual std::string GetAndClearEEHeapContent() = 0;
};
