// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>


enum class SkipProfileHeuristicType
{
    ShortLived,
    NoSpan
};

class IProfilerTelemetry
{
public:
    virtual void OnSpanCreated() = 0;
    virtual bool IsSpanCreated() = 0;

    // send metrics
    virtual void ProcessStart() = 0;
    virtual void ProcessEnd() = 0;
    virtual void SentProfile() = 0;
    virtual void SkippedProfile(SkipProfileHeuristicType heuristic) = 0;

    virtual ~IProfilerTelemetry() = default;
};
