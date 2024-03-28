// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CallstackPool.h"

CallstackPool::CallstackPool(pmr::memory_resource* memory_resource) :
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

inline void* allocateCallstack(pmr::memory_resource* allocator, std::size_t size)
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
    _memory_resource->deallocate(buffer.data(), Callstack::MaxSize);
}
