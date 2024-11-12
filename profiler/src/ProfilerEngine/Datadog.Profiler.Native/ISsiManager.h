// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "SkipProfileHeuristicType.h"
#include "DeploymentMode.h"

#include <cstdint>
#include <string>
#include <string_view>

class ISsiManager
{
public:
    virtual ~ISsiManager() = default;

    virtual void OnSpanCreated() = 0;
    virtual bool IsSpanCreated() const = 0;
    virtual bool IsLongLived() const = 0;

    // enabled by configuration (DD_PROFILING_ENABLED=true) / SSI (DD_INJECTION_ENABLED contains "profiler")
    virtual bool IsProfilerEnabled() = 0;

    // started if manually enabled (DD_PROFILING_ENABLED=true) or enabled by SII and triggered heuristics (long lived + spans)
    virtual bool IsProfilerStarted() = 0;

    // send metrics
    virtual void ProcessStart() = 0;
    virtual void ProcessEnd() = 0;

    // heuristics state
    virtual SkipProfileHeuristicType GetSkipProfileHeuristic() const = 0;

    virtual DeploymentMode GetDeploymentMode() const = 0;
};
