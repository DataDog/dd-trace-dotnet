// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Callstack.h"

#include <cassert>
#include <utility>

#include <string.h>

Callstack::Callstack() :
    Callstack(nullptr)
{
}

Callstack::Callstack(shared::pmr::memory_resource* memoryResource) :
    _memoryResource{memoryResource}, _buffer{}, _count{0}
{
    if (_memoryResource != nullptr)
    {
        auto* data = reinterpret_cast<std::uintptr_t*>(_memoryResource->allocate(MaxSize));
        _buffer = shared::span<std::uintptr_t>(data, data == nullptr ? 0 : MaxFrames);
    }
}

Callstack::~Callstack()
{
    if (_memoryResource != nullptr && _buffer.data() != nullptr)
    {
        auto old = std::exchange(_buffer, {});
        _memoryResource->deallocate(old.data(), MaxSize);
    }
}

Callstack::Callstack(Callstack&& other) noexcept :
    _memoryResource{nullptr},
    _buffer{},
    _count{0}
{
    *this = std::move(other);
}

Callstack& Callstack::operator=(Callstack&& other) noexcept
{
    if (this == &other)
    {
        return *this;
    }

    std::swap(_memoryResource, other._memoryResource);
    std::swap(_buffer, other._buffer);
    std::swap(_count, other._count);

    return *this;
}

bool Callstack::Add(std::uintptr_t ip)
{
    if (_count >= _buffer.size())
        return false;

    _buffer[_count++] = ip;
    return true;
}

shared::span<std::uintptr_t> Callstack::Data() const
{
    return _buffer;
}

void Callstack::SetCount(std::size_t count)
{
    _count = count;
}

std::size_t Callstack::Size() const
{
    return _count;
}

std::size_t Callstack::Capacity() const
{
    return _buffer.size();
}

std::uintptr_t* Callstack::begin() const
{
    return _buffer.data();
}

std::uintptr_t* Callstack::end() const
{
    return _buffer.data() + _count;
}

void Callstack::CopyFrom(Callstack const& other)
{
    memcpy(_buffer.data(), other._buffer.data(), other._count * sizeof(uintptr_t));
    _count = other._count;
}
