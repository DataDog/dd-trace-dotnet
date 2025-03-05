// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "shared/src/native-src/string.h"

enum CpuProfilerType : int
{
    ManualCpuTime,
#ifdef LINUX
    TimerCreate
#endif
};

inline bool convert_to(shared::WSTRING const& s, CpuProfilerType& profilerType)
{
    if (shared::string_iequal(s, WStr("ManualCpuTime")))
    {
        profilerType = CpuProfilerType::ManualCpuTime;
        return true;
    }
#ifdef LINUX
    else if (shared::string_iequal(s, WStr("TimerCreate")))
    {
        profilerType = CpuProfilerType::TimerCreate;
        return true;
    }
#endif
    return false;
}