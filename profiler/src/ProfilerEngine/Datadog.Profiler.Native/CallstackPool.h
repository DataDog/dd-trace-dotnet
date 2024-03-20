// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "Callstack.h"

#include <memory>
#include <atomic>

class CallstackPool
{
public:
    CallstackPool(std::size_t nbPools);

    Callstack Get();

private:

    static constexpr std::uint8_t MaxRetry =3;

    friend Callstack;

    shared::span<std::uintptr_t> Acquire();
    void Release(shared::span<std::uintptr_t> buffer);

    struct PoolHeader
    {
        // TODO create an aligned version of this struct
        std::atomic<std::uint8_t> _lock;
    };

    struct Pool
    {
        PoolHeader _header;
        std::uintptr_t _frames[Callstack::MaxFrames];
    };

    std::size_t _nbPools;
    std::unique_ptr<std::uint8_t[]> _pools;
    std::atomic<std::uint64_t> _current;
};
