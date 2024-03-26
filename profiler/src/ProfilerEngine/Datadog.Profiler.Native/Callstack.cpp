// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Callstack.h"

#include "CallstackPool.h"

#include <cassert>
#include <utility>

Callstack::Callstack() :
    Callstack(nullptr)
{
}

Callstack::Callstack(CallstackPool* pool) :
    _pool{pool},
    _buffer{},
    _count{0}
{
    if (_pool != nullptr)
    {
        _buffer = _pool->Acquire();
    }
}

Callstack::~Callstack()
{
    if (_pool != nullptr)
    {
        _pool->Release(std::exchange(_buffer, {}));
    }
}

Callstack::Callstack(Callstack&& other) noexcept :
    _pool{nullptr},
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

    std::swap(_pool, other._pool);
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
