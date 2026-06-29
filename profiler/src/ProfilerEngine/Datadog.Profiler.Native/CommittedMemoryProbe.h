// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>

class IMemoryReader;

namespace eeheap
{
// Returns the number of committed bytes within [base, base + reserved). Probes one byte per page
// under the reader's fault guard (which never crashes on an unmapped/guard page), stopping at the
// first unreadable page - mirroring SOS's SafeReadMemory page probe. The result is capped at
// reserved. Used by both backends for the non-GC heaps whose block/Traverse* APIs only expose a
// reserved block size. Returns 0 for a null/zero region.
uint64_t ProbeCommittedBytes(IMemoryReader& reader, uintptr_t base, uint64_t reserved);
} // namespace eeheap
