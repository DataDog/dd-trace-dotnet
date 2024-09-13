// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CpuProfilerDisableScope.h"

#include "ManagedThreadInfo.h"

#include <sys/syscall.h>
#include <unistd.h>

CpuProfilerDisableScope::CpuProfilerDisableScope(ManagedThreadInfo* threadInfo)
    : _timerId{-1}, _oldPeriod{0}
{
    if (threadInfo == nullptr) [[unlikely]]
        return;

    _timerId = threadInfo->GetTimerId();

    if (_timerId != -1)
    {
        struct itimerspec ts;
        ts.it_interval.tv_sec = 0;
        ts.it_interval.tv_nsec = 0;
        ts.it_value = ts.it_interval;
        // disarm the timer so this is not accounted for the managed thread cpu usage
        syscall(__NR_timer_settime, _timerId, 0, &ts, &_oldPeriod);
    }
}

CpuProfilerDisableScope::~CpuProfilerDisableScope()
{
   if (_timerId != -1)
   {
       // re-arm the timer
       syscall(__NR_timer_settime, _timerId, 0, &_oldPeriod, nullptr);
   }
}
