// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "EnabledProfilers.h"
#include "IConfiguration.h"

EnabledProfilers::EnabledProfilers(IConfiguration* pConfiguration, bool isListeningToClrEvents)
{
    _enabledProfilers = RuntimeProfiler::None;

    if (pConfiguration->IsWallTimeProfilingEnabled())
    {
        _enabledProfilers |= RuntimeProfiler::WallTime;
    }
    if (pConfiguration->IsCpuProfilingEnabled())
    {
        _enabledProfilers |= RuntimeProfiler::Cpu;
    }
    if (pConfiguration->IsExceptionProfilingEnabled())
    {
        _enabledProfilers |= RuntimeProfiler::Exceptions;
    }

    // CLR events driven profilers
    if (isListeningToClrEvents)
    {
        if (pConfiguration->IsAllocationProfilingEnabled())
        {
            _enabledProfilers |= RuntimeProfiler::Allocations;
        }

        // TODO: add new CLR event driven profilers
    }
}

bool EnabledProfilers::IsEnabled(RuntimeProfiler profiler) const
{
    return ((_enabledProfilers & profiler) == profiler);
}
