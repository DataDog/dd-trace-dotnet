// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Callstack.h"

#include <cassert>

Callstack::Callstack() :
    _callstack{std::make_unique<std::uintptr_t[]>(MaxFrames)},
    _count{0}
{
}

bool Callstack::Add(std::uintptr_t ip)
{
    if (_count >= MaxFrames)
        return false;

    _callstack[_count++] = ip;
    return true;
}

shared::span<std::uintptr_t> Callstack::Data()
{
    return shared::span<std::uintptr_t>(_callstack.get(), MaxFrames);
}

void Callstack::SetCount(std::size_t count)
{
    _count = count;
}

std::size_t Callstack::size() const
{
    return _count;
}

std::uintptr_t* Callstack::begin() const
{
    return _callstack.get();
}

std::uintptr_t* Callstack::end() const
{
    return _callstack.get() + _count;
}
