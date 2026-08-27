// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "AddressRegion.h"

#include <vector>

// A snapshot of the current process' OS address space, captured once per export. Holds fine-grained,
// address-sorted, non-overlapping runs (one per VirtualQuery run on Windows / VMA on Linux) and
// answers indexed range/point queries without re-walking the OS region map on every call.
//
// The same instance is shared by the CLR native-heap enumerators (for the gap-aware card-table
// committed size) and by MemoryBreakdownProvider (for the OS/CLR reconciliation), so the region map
// is walked at most once per export.
class IAddressSpaceMap
{
public:
    virtual ~IAddressSpaceMap() = default;

    // Fine-grained, address-sorted, non-overlapping runs.
    virtual const std::vector<AddressRegion>& Regions() const = 0;

    // Sum of committed bytes overlapping [base, base + size). O(log n + k) via binary search over the
    // address-sorted runs. Gap-aware (does not stop at the first hole). The result is capped at size.
    virtual uint64_t GetCommittedBytes(uintptr_t base, uint64_t size) const = 0;

    // Sum of resident (RSS) bytes overlapping [base, base + size). 0 when RSS was not captured.
    virtual uint64_t GetRss(uintptr_t base, uint64_t size) const = 0;

    // Fills `out` with the run containing `address` and returns true; returns false when no run
    // contains it.
    virtual bool TryGetRegion(uintptr_t address, AddressRegion& out) const = 0;

    // Whether the map was captured successfully (non-empty).
    virtual bool IsAvailable() const = 0;

    // Whether committed bytes are a meaningful per-sample value on this platform (true on Windows,
    // false on Linux where RSS is used instead). GetCommittedBytes still returns a value regardless
    // (needed for the CLR card-table committed size on both platforms).
    virtual bool ProvidesCommitted() const = 0;

    // Whether RSS is available as a per-sample value (true on Linux, false on Windows).
    virtual bool ProvidesRss() const = 0;
};
