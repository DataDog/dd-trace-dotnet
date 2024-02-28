// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <string>

enum class SkipProfileHeuristicType
{
    Unknown = 0,
    ShortLived = 1,
    NoSpan = 2
};

enum class DeploymentMode
{
    Unknown = 0,
    Manual = 1,
    SingleStepInstrumentation = 2
};

class IProfilerTelemetry
{
public:
    // send metrics
    virtual void ProcessStart(DeploymentMode deployment) = 0;
    virtual void ProcessEnd(uint64_t duration) = 0;
    virtual void SentProfile() = 0;
    virtual void SkippedProfile(SkipProfileHeuristicType heuristic) = 0;

    virtual ~IProfilerTelemetry() = default;
};
