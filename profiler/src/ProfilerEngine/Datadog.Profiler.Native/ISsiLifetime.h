// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// With Single Step Instrumentation, profiling start is delayed until heuristics are triggered.
// This interface is used to notify the profiler that it is time to start profiling.
class ISsiLifetime
{
public:
    virtual void OnStartDelayedProfiling() = 0;

    virtual ~ISsiLifetime() = default;
};