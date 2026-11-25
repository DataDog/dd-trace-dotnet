// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "ClrNativeHeapInfo.h"

#include <vector>

class IAddressSpaceMap;

// Shared, per-export snapshot of the CLR native/managed heaps (DAC/cDAC) plus the OS address-space
// map they were reconciled against. Captured once per export and consumed by both EEHeapReporter (JSON)
// and MemoryBreakdownProvider (samples), so the DAC/cDAC walk and the OS region walk each run at most
// once per export. All calls happen on the exporter thread inside Export().
class IClrNativeHeapSnapshot
{
public:
    virtual ~IClrNativeHeapSnapshot() = default;

    // CLR native heaps for the current export (enumerated once, then cached until Invalidate()).
    virtual const std::vector<ClrNativeHeapInfo>& GetSnapshot() = 0;

    // The OS address-space map for the current export (captured once, then cached until Invalidate()).
    // May be null when capture failed.
    virtual IAddressSpaceMap* GetAddressSpaceMap() = 0;

    // Clears the cached CLR heaps + address-space map so the next export re-captures. Called at the end
    // of ProfileExporter::Export().
    virtual void Invalidate() = 0;

    // Whether a CLR native-heap backend is available (the feature no-ops when false).
    virtual bool IsAvailable() = 0;

    // "cdac" | "dac" | "none" - used as the eeheap.json "source" field.
    virtual const char* GetBackendName() = 0;
};
