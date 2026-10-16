// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IAddressSpaceMap.h"

#include <vector>

// Concrete, platform-independent IAddressSpaceMap. Owns the address-sorted run vector and implements
// the indexed lookups via binary search. The platform enumerators (OsSpecificApi::CaptureAddressSpaceMap
// in the Windows/Linux projects) build the run vector and hand it here; all query/reconciliation logic
// stays common and unit-testable with a hand-built vector.
class AddressSpaceMap : public IAddressSpaceMap
{
public:
    // Takes ownership of `regions`; sorts them by start address. `regions` may contain gaps but must
    // not overlap (one run per VirtualQuery run / VMA guarantees this).
    AddressSpaceMap(std::vector<AddressRegion> regions, bool providesCommitted, bool providesRss);

    const std::vector<AddressRegion>& Regions() const override;
    uint64_t GetCommittedBytes(uintptr_t base, uint64_t size) const override;
    uint64_t GetRss(uintptr_t base, uint64_t size) const override;
    bool TryGetRegion(uintptr_t address, AddressRegion& out) const override;
    bool IsAvailable() const override;
    bool ProvidesCommitted() const override;
    bool ProvidesRss() const override;

private:
    // Index of the first run whose End() is strictly greater than `address` (i.e. the first run that
    // could overlap [address, ...)). Returns Regions().size() when none.
    size_t FirstRunFrom(uintptr_t address) const;

    std::vector<AddressRegion> _regions;
    bool _providesCommitted;
    bool _providesRss;
};
