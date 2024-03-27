// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <string>
#include <string_view>

class ISsiManager
{
public:
    virtual void OnSpanCreated() = 0;
    virtual bool IsSpanCreated() = 0;
    virtual bool IsLongLived() = 0;

    // enabled by configuration (DD_PROFILING_ENABLED=true) / SSI (DD_INJECTION_ENABLED contains "profiler")
    virtual bool IsProfilerEnabled() = 0;

    // activated manually (DD_PROFILING_ENABLED=true) or by heuristics (long lived + spans)
    virtual bool IsProfilerActivated() = 0;

    // send metrics
    virtual void ProcessStart() = 0;
    virtual void ProcessEnd() = 0;

    virtual ~ISsiManager() = default;
};
