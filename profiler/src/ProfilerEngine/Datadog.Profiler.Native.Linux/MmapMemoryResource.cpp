// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "MmapMemoryResource.h"

#include <sys/mman.h>
#include <unistd.h>

inline long get_page_size()
{
    static long page_size = 0;
    if (!page_size)
    {
        page_size = sysconf(_SC_PAGESIZE);
    }
    return page_size;
}

inline uint64_t align_to_page(uint64_t x)
{
    return ((x - 1) | (get_page_size() - 1)) + 1;
}

void* MmapMemoryResource::do_allocate(size_t _Bytes, size_t _Align)
{
    auto const total_length = align_to_page(_Bytes);
    void* region = mmap(nullptr, total_length, PROT_WRITE | PROT_READ, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
    if (MAP_FAILED == region || region == nullptr)
    {
        return nullptr;
    }

    return region;
}

void MmapMemoryResource::do_deallocate(void* _Ptr, size_t _Bytes, size_t _Align)
{
    auto const total_length = align_to_page(_Bytes);
    munmap(_Ptr, total_length);
}

bool MmapMemoryResource::do_is_equal(const memory_resource& _That) const noexcept
{
    return this == &_That;
}