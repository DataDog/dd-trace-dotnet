// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CommittedMemoryProbe.h"

#include "IAddressSpaceMap.h"
#include "IMemoryReader.h"
#include "OsSpecificApi.h"

#include <memory>

namespace eeheap
{
namespace
{
// Hard cap on the number of pages probed, so a corrupt/huge reserved size cannot stall enumeration.
constexpr uint64_t MaxProbedPages = 1ull << 20; // 4 GiB at 4 KiB pages
} // namespace

uint64_t ProbeCommittedBytes(IMemoryReader& reader, uintptr_t base, uint64_t reserved)
{
    if (base == 0 || reserved == 0)
    {
        return 0;
    }

    const uint64_t pageSize = static_cast<uint64_t>(OsSpecificApi::GetSystemPageSize());
    uint64_t committed = 0;
    uint64_t offset = 0;
    uint64_t pages = 0;

    while (offset < reserved && pages < MaxProbedPages)
    {
        uint8_t probe = 0;
        if (!reader.Read(base + static_cast<uintptr_t>(offset), probe))
        {
            break; // first uncommitted page -> stop
        }

        const uint64_t remaining = reserved - offset;
        const uint64_t step = remaining < pageSize ? remaining : pageSize;
        committed += step;
        offset += step;
        pages++;
    }

    return committed > reserved ? reserved : committed;
}

uint64_t QueryCommittedBytes(const IAddressSpaceMap* map, uintptr_t base, uint64_t reserved)
{
    if (base == 0 || reserved == 0)
    {
        return 0;
    }

    if (map != nullptr)
    {
        return map->GetCommittedBytes(base, reserved);
    }

    // No shared map: capture one on demand. This is the fallback used by standalone callers/tests; it
    // produces the same numbers the previous per-call OS walk did.
    auto captured = OsSpecificApi::CaptureAddressSpaceMap();
    return captured != nullptr ? captured->GetCommittedBytes(base, reserved) : 0;
}

uint64_t QueryCommittedBytes(uintptr_t base, uint64_t reserved)
{
    return QueryCommittedBytes(nullptr, base, reserved);
}
} // namespace eeheap
