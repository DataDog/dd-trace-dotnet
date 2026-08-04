// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>

class IMemoryReader;
class IAddressSpaceMap;

namespace eeheap
{
// Returns the number of committed bytes within [base, base + reserved). Probes one byte per page
// under the reader's fault guard (which never crashes on an unmapped/guard page), stopping at the
// first unreadable page - mirroring SOS's SafeReadMemory page probe. The result is capped at
// reserved. Used by both backends for the non-GC heaps whose block/Traverse* APIs only expose a
// reserved block size. Returns 0 for a null/zero region.
uint64_t ProbeCommittedBytes(IMemoryReader& reader, uintptr_t base, uint64_t reserved);

// Returns the number of committed bytes within [base, base + reserved) using the shared OS region
// map (IAddressSpaceMap). Unlike ProbeCommittedBytes this is gap-aware: it sums every committed
// sub-range in the reservation rather than stopping at the first hole, at O(log n + k) cost. Required
// for the GC "bookkeeping" (card table) block, whose committed runs are scattered per element with
// reserved gaps between them.
//
// When `map` is non-null the answer comes from it directly (no syscalls - the map was captured once
// for the whole export). When `map` is null a map is captured on demand via
// OsSpecificApi::CaptureAddressSpaceMap(false), preserving the previous standalone behavior (used by
// tests and any caller without a shared map). In-process only; returns 0 when the region map cannot
// be determined. The result is capped at reserved.
uint64_t QueryCommittedBytes(const IAddressSpaceMap* map, uintptr_t base, uint64_t reserved);

// Convenience overload that captures a fresh map on demand (map == nullptr).
uint64_t QueryCommittedBytes(uintptr_t base, uint64_t reserved);
} // namespace eeheap
