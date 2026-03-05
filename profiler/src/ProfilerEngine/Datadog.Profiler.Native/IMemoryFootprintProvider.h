// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstddef>

/// <summary>
/// Interface for components that can report their memory footprint
/// </summary>
class IMemoryFootprintProvider
{
public:
    virtual ~IMemoryFootprintProvider() = default;

    /// <summary>
    /// Get the total memory size in bytes consumed by this component
    /// </summary>
    /// <returns>Total memory size in bytes</returns>
    virtual size_t GetMemorySize() const = 0;

    /// <summary>
    /// Log a detailed breakdown of memory consumption to the debug log
    /// </summary>
    virtual void LogMemoryBreakdown() const = 0;
};
