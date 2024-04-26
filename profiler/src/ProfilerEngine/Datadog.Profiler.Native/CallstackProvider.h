// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "Callstack.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

#include <memory>

class CallstackProvider
{
public:
    CallstackProvider();

    explicit CallstackProvider(shared::pmr::memory_resource* memoryResource);
    ~CallstackProvider();

    CallstackProvider(CallstackProvider const&) = delete;
    CallstackProvider& operator=(CallstackProvider const&) = delete;

    CallstackProvider(CallstackProvider&& other) noexcept;
    CallstackProvider& operator=(CallstackProvider&& other) noexcept;

    Callstack Get();

private:
    shared::pmr::memory_resource* _resource;
};
