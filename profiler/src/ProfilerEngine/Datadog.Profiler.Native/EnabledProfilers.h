// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IEnabledProfilers.h"


class IConfiguration;

class EnabledProfilers : public IEnabledProfilers
{
public:
    EnabledProfilers(IConfiguration* pConfiguration, bool isListeningToClrEvents, bool isHeapProfilingEnabled);
    bool IsEnabled(RuntimeProfiler profiler) const override;
    void Disable(RuntimeProfiler profiler) override;

private:
    RuntimeProfiler _enabledProfilers;
};
