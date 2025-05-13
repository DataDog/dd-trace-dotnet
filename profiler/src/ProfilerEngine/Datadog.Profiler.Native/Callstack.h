// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>

#include "shared/src/native-src/dd_span.hpp"
#include "shared/src/native-src/dd_memory_resource.hpp"

class Callstack
{
public:
    static constexpr std::uint8_t FrameSize = sizeof(std::uintptr_t);
    static constexpr std::uint16_t MaxFrames = 1024;
    static constexpr std::size_t MaxSize = MaxFrames * FrameSize;

    // default ctor is needed because there are instances of Callstack that can
    // be fields of classes.
    Callstack();
    explicit Callstack(shared::pmr::memory_resource* memoryResource);

    ~Callstack();

#ifdef DD_TEST
    Callstack(shared::span<std::uintptr_t> buffer)
    {
        _memoryResource = nullptr;
        _buffer = buffer;
        _count = buffer.size();
    }

    bool operator==(Callstack const& other) const
    {
        if (other._count != _count)
        {
            return false;
        }

        // in the test we do not care about equality of _pool member

        for (auto i = 0; i < _count; i++)
        {
            if (_buffer[i] != other._buffer[i])
            {
                return false;
            }
        }
        return true;
    }
#endif

    Callstack(Callstack const&) = delete;
    Callstack& operator=(Callstack const&) = delete;

    Callstack(Callstack&& other) noexcept;
    Callstack& operator=(Callstack&& other) noexcept;

    bool Add(std::uintptr_t ip);

    shared::span<std::uintptr_t> Data() const;
    void SetCount(std::size_t count);

    std::size_t Size() const;
    std::size_t Capacity() const;

    // iterator
    std::uintptr_t* begin() const;
    std::uintptr_t* end() const;

    void CopyFrom(Callstack const& other);

private:
    shared::pmr::memory_resource* _memoryResource;
    shared::span<std::uintptr_t> _buffer;
    std::size_t _count;
};
