// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "EnumHelpers.h"

ENUM_FLAGS(RuntimeProfiler, size_t)
{
    None = 0,
    WallTime = 1,
    Cpu = 2,
    Exceptions = 4,
    Allocations = 8,

    // Contentions = 16
    // Heap = 32
};

class IEnabledProfilers
{
public:
    virtual ~IEnabledProfilers() = default;
    virtual bool IsEnabled(RuntimeProfiler profiler) const = 0;
};