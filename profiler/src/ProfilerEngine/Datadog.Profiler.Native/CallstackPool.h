// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "Callstack.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

#include "shared/src/native-src/string.h"
#include "shared/src/native-src/util.h"

#include <atomic>
#include <memory>


class CallstackPool
{
public:
    CallstackPool(shared::pmr::memory_resource* memory_resource);

    ~CallstackPool() = default;

    Callstack Get();

    CallstackPool(CallstackPool const&) = delete;
    CallstackPool& operator=(CallstackPool const&) = delete;

    CallstackPool(CallstackPool&& other) noexcept;
    CallstackPool& operator=(CallstackPool&& other) noexcept;

private:
    friend Callstack;
    shared::span<std::uintptr_t> Acquire();
    void Release(shared::span<std::uintptr_t> buffer);

    void* allocateCallStack();

    shared::pmr::memory_resource* _memory_resource;
};
