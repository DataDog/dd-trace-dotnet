// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <cstdint>
#include <time.h>

struct ManagedThreadInfo;

struct CpuProfilerDisableScope
{
public:
    explicit CpuProfilerDisableScope(ManagedThreadInfo* threadInfo);
    ~CpuProfilerDisableScope();

    CpuProfilerDisableScope(CpuProfilerDisableScope const&) = delete;
    CpuProfilerDisableScope& operator=(CpuProfilerDisableScope const&) = delete;
    CpuProfilerDisableScope(CpuProfilerDisableScope&&) = delete;
    CpuProfilerDisableScope operator=(CpuProfilerDisableScope&&) = delete;

private:
    std::int32_t _timerId;
    struct itimerspec _oldPeriod;
};