// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CallstackPool.h"

CallstackPool::CallstackPool(shared::pmr::memory_resource* memory_resource) :
    _memory_resource{memory_resource}

{
}

CallstackPool::CallstackPool(CallstackPool&& other) noexcept :
    _memory_resource{nullptr}
{
    *this = std::move(other);
}

CallstackPool& CallstackPool::operator=(CallstackPool&& other) noexcept
{
    if (this == &other)
        return *this;

    std::swap(other._memory_resource, _memory_resource);

    return *this;
}

Callstack CallstackPool::Get()
{
    return Callstack(this);
}

// TL;DR - This function is required to prevent the compiler from removing the null-check in Acquire
//
// On linux allocate has std::pmr::memory_resource::allocate function has the returns_nonnull attribute.
// This allows the compiler to optimize the code, by removing the null-check.
// We observed a segmentation fault in our CI with ASAN and UBSAN in the unit tests.
// We still want to keep the null-check, because when we will use (for some profilers) the
// ringbuffer-based memory_resource, it could return nullptr (when there is no more room)
inline void* allocateCallstack(shared::pmr::memory_resource* allocator, std::size_t size)
{
    return allocator->allocate(size);
}

shared::span<std::uintptr_t> CallstackPool::Acquire()
{
    auto* buffer = allocateCallstack(_memory_resource, Callstack::MaxSize);

    if (buffer == nullptr)
        return {};

    return {reinterpret_cast<std::uintptr_t*>(buffer), Callstack::MaxFrames};
}

void CallstackPool::Release(shared::span<std::uintptr_t> buffer)
{
    // buffer.data() can be null, and on Linux, the first parameter of deallocate function has non-null attribute.
    // The compiler can be aggressive and remove the null-check in the deallocate function.
    // To be safe, we check here if the buffer points to nullptr and call (or not) deallocate.
    if (buffer.data() != nullptr)
    {
        _memory_resource->deallocate(buffer.data(), Callstack::MaxSize);
    }
}
