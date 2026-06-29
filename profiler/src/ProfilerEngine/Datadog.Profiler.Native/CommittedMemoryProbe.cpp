// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CommittedMemoryProbe.h"

#include "IMemoryReader.h"

#ifdef _WINDOWS
#include <windows.h>
#else
#include <unistd.h>
#endif

namespace eeheap
{
namespace
{
// OpSysTools::GetPageSize() throws on Windows, so resolve the page size from the OS directly here.
size_t GetSystemPageSize()
{
#ifdef _WINDOWS
    SYSTEM_INFO info{};
    GetSystemInfo(&info);
    return info.dwPageSize != 0 ? static_cast<size_t>(info.dwPageSize) : 4096;
#else
    long pageSize = sysconf(_SC_PAGESIZE);
    return pageSize > 0 ? static_cast<size_t>(pageSize) : 4096;
#endif
}

// Hard cap on the number of pages probed, so a corrupt/huge reserved size cannot stall enumeration.
constexpr uint64_t MaxProbedPages = 1ull << 20; // 4 GiB at 4 KiB pages
} // namespace

uint64_t ProbeCommittedBytes(IMemoryReader& reader, uintptr_t base, uint64_t reserved)
{
    if (base == 0 || reserved == 0)
    {
        return 0;
    }

    const uint64_t pageSize = static_cast<uint64_t>(GetSystemPageSize());
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
} // namespace eeheap
