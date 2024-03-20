// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>

#include "shared/src/native-src/dd_span.hpp"

class CallstackPool;

class Callstack
{
public:
    static constexpr std::uint16_t MaxFrames = 1024;

    Callstack();
    ~Callstack();

    Callstack(Callstack const&) = delete;
    Callstack& operator=(Callstack const&) = delete;

    Callstack(Callstack&& other) noexcept;
    Callstack& operator=(Callstack&& other) noexcept;

    bool Add(std::uintptr_t ip);

    shared::span<std::uintptr_t> Data() const;
    void SetCount(std::size_t count);

    std::size_t size() const;
    std::size_t capacity() const;

    // iterator
    std::uintptr_t* begin() const;
    std::uintptr_t* end() const;


private:
    friend CallstackPool;
    Callstack(CallstackPool* pool);

    CallstackPool* _pool;
    shared::span<std::uintptr_t> _buffer;
    std::size_t _count;
};
