// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "Callstack.h"

#include "shared/src/native-src/string.h"
#include "shared/src/native-src/util.h"

#include <atomic>
#include <memory>

class CallstackPool
{
public:
    CallstackPool(std::size_t nbCallstacks);

    ~CallstackPool() = default;

    Callstack Get();

    CallstackPool(CallstackPool const&) = delete;
    CallstackPool& operator=(CallstackPool const&) = delete;

    CallstackPool(CallstackPool&& other) noexcept;
    CallstackPool& operator=(CallstackPool&& other) noexcept;

private:
    struct CallstackHeader
    {
        std::atomic<std::uint8_t> _lock;
    };

    struct CallstackLayout
    {
        CallstackHeader _header;
        std::uintptr_t _frames[Callstack::MaxFrames];
    };

    friend Callstack;
    shared::span<std::uintptr_t> Acquire();
    void Release(shared::span<std::uintptr_t> buffer);

    template <typename T>
    static constexpr std::size_t ComputeAlignedSize()
    {
        constexpr auto x = sizeof(T);
        constexpr std::size_t bufferAlignement = 8;

        constexpr auto value = ((x - 1) | (bufferAlignement - 1)) + 1;
        return value;
    }

    static constexpr std::uint8_t MaxRetry = 3;

    std::size_t _nbCallstacks;
    std::unique_ptr<std::uint8_t[]> _callstacks;
    std::atomic<std::uint64_t> _current;
};
