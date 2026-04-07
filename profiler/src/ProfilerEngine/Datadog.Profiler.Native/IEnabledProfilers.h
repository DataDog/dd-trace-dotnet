// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstddef>
#include "EnumHelpers.h"

ENUM_FLAGS(RuntimeProfiler, size_t)
{
    None = 0,
    WallTime = 1,
    Cpu = 2,
    Exceptions = 4,
    Allocations = 8,
    LockContention = 16,
    GC = 32,
    Heap = 64,
    Network = 128, // TODO: should it be renamed "Http"?
    CpuGc = 256,
    ThreadsLifetime = 512,
    HeapSnapshot = 1024,
};

class IEnabledProfilers
{
public:
    virtual ~IEnabledProfilers() = default;
    virtual bool IsEnabled(RuntimeProfiler profiler) const = 0;
    virtual void Disable(RuntimeProfiler profiler) = 0;
};