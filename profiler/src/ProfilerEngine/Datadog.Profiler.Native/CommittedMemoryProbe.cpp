// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CommittedMemoryProbe.h"

#include "IMemoryReader.h"

#ifdef _WINDOWS
#include <windows.h>
#else
#include <cstdio>
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

namespace
{
// Safety cap on the number of OS regions walked, in case the region map is corrupt or pathological.
constexpr uint64_t MaxQueriedRegions = 1ull << 20;
} // namespace

#ifdef _WINDOWS
uint64_t QueryCommittedBytes(uintptr_t base, uint64_t reserved)
{
    if (base == 0 || reserved == 0)
    {
        return 0;
    }

    const uintptr_t end = base + static_cast<uintptr_t>(reserved);
    uint64_t committed = 0;
    uintptr_t addr = base;
    uint64_t regions = 0;

    // VirtualQuery returns one MEMORY_BASIC_INFORMATION per contiguous run of same-state pages, so we
    // advance run-by-run (O(#regions)) rather than page-by-page. Every page in the range belongs to
    // the GC's single reservation, so any MEM_COMMIT run is bookkeeping - we count it regardless of
    // protection (the card table is reserved with MEM_WRITE_WATCH, which VirtualQuery still reports).
    while (addr < end && regions++ < MaxQueriedRegions)
    {
        MEMORY_BASIC_INFORMATION mbi{};
        if (VirtualQuery(reinterpret_cast<LPCVOID>(addr), &mbi, sizeof(mbi)) == 0)
        {
            break;
        }

        const uintptr_t regionEnd = reinterpret_cast<uintptr_t>(mbi.BaseAddress) + mbi.RegionSize;
        const uintptr_t clampedEnd = regionEnd < end ? regionEnd : end;
        if (clampedEnd <= addr)
        {
            break; // no forward progress -> avoid spinning
        }

        if (mbi.State == MEM_COMMIT)
        {
            committed += static_cast<uint64_t>(clampedEnd - addr);
        }

        addr = clampedEnd;
    }

    return committed > reserved ? reserved : committed;
}
#else
uint64_t QueryCommittedBytes(uintptr_t base, uint64_t reserved)
{
    if (base == 0 || reserved == 0)
    {
        return 0;
    }

    FILE* maps = fopen("/proc/self/maps", "r");
    if (maps == nullptr)
    {
        return 0;
    }

    const uintptr_t end = base + static_cast<uintptr_t>(reserved);
    uint64_t committed = 0;
    uint64_t regions = 0;
    char line[512];

    // The GC reserves with mmap(PROT_NONE) and commits with mprotect(PROT_READ|WRITE), which splits the
    // VMA, so committed runs appear as accessible (e.g. "rw-p") lines and reserved gaps as "---p". We sum
    // the overlap of every accessible VMA with [base, end). /proc/self/maps is address-sorted, so we can
    // stop as soon as a VMA starts at/after end.
    while (fgets(line, sizeof(line), maps) != nullptr && regions++ < MaxQueriedRegions)
    {
        unsigned long long start = 0;
        unsigned long long stop = 0;
        char perms[8] = {0};
        if (sscanf(line, "%llx-%llx %7s", &start, &stop, perms) != 3)
        {
            continue;
        }

        if (start >= static_cast<unsigned long long>(end))
        {
            break; // sorted -> nothing further can overlap
        }
        if (stop <= static_cast<unsigned long long>(base))
        {
            continue; // entirely before our range
        }

        // PROT_NONE ("---p") is reserved-only; anything readable/writable/executable is committed.
        if (perms[0] == '-' && perms[1] == '-' && perms[2] == '-')
        {
            continue;
        }

        const uintptr_t s = static_cast<uintptr_t>(start) > base ? static_cast<uintptr_t>(start) : base;
        const uintptr_t e = static_cast<uintptr_t>(stop) < end ? static_cast<uintptr_t>(stop) : end;
        if (e > s)
        {
            committed += static_cast<uint64_t>(e - s);
        }
    }

    fclose(maps);
    return committed > reserved ? reserved : committed;
}
#endif
} // namespace eeheap
