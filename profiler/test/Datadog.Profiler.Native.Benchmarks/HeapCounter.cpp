// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// Replaces the global operator new/delete of the benchmark executable to count the
// heap bytes actually in use (malloc_usable_size, i.e. including the allocator
// rounding). Used by the memory benchmarks to compare the implementations.

#include "HeapCounter.h"

#include <algorithm>
#include <cstdlib>
#include <malloc.h>
#include <new>

namespace bench {
std::atomic<std::int64_t> g_liveHeapBytes{0};
}

namespace {

void* Allocate(std::size_t size)
{
    void* p = std::malloc(size == 0 ? 1 : size);
    if (p == nullptr)
    {
        throw std::bad_alloc();
    }
    bench::g_liveHeapBytes.fetch_add(static_cast<std::int64_t>(malloc_usable_size(p)), std::memory_order_relaxed);
    return p;
}

void* AllocateAligned(std::size_t size, std::align_val_t alignment)
{
    void* p = nullptr;
    auto align = std::max<std::size_t>(static_cast<std::size_t>(alignment), sizeof(void*));
    if (posix_memalign(&p, align, size == 0 ? 1 : size) != 0)
    {
        throw std::bad_alloc();
    }
    bench::g_liveHeapBytes.fetch_add(static_cast<std::int64_t>(malloc_usable_size(p)), std::memory_order_relaxed);
    return p;
}

void Deallocate(void* p) noexcept
{
    if (p == nullptr)
    {
        return;
    }
    bench::g_liveHeapBytes.fetch_sub(static_cast<std::int64_t>(malloc_usable_size(p)), std::memory_order_relaxed);
    std::free(p);
}

} // namespace

void* operator new(std::size_t size) { return Allocate(size); }
void* operator new[](std::size_t size) { return Allocate(size); }
void* operator new(std::size_t size, std::align_val_t alignment) { return AllocateAligned(size, alignment); }
void* operator new[](std::size_t size, std::align_val_t alignment) { return AllocateAligned(size, alignment); }
void operator delete(void* p) noexcept { Deallocate(p); }
void operator delete[](void* p) noexcept { Deallocate(p); }
void operator delete(void* p, std::size_t) noexcept { Deallocate(p); }
void operator delete[](void* p, std::size_t) noexcept { Deallocate(p); }
void operator delete(void* p, std::align_val_t) noexcept { Deallocate(p); }
void operator delete[](void* p, std::align_val_t) noexcept { Deallocate(p); }
void operator delete(void* p, std::size_t, std::align_val_t) noexcept { Deallocate(p); }
void operator delete[](void* p, std::size_t, std::align_val_t) noexcept { Deallocate(p); }
