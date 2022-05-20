// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// OsSpecificApi for LINUX

#include <sys/syscall.h>
#include "OsSpecificApi.h"

#include "LinuxStackFramesCollector.h"
#include "StackFramesCollectorBase.h"
#include "shared/src/native-src/loader.h"

namespace OsSpecificApi {

std::unique_ptr<StackFramesCollectorBase> CreateNewStackFramesCollectorInstance(ICorProfilerInfo4* pCorProfilerInfo)
{
    return std::make_unique<LinuxStackFramesCollector>(const_cast<ICorProfilerInfo4* const>(pCorProfilerInfo));
}

// https://linux.die.net/man/5/proc
//
// the third field is the Status:  (Running = R, D or W)
//   state %c
// (3) One character from the string "RSDZTW" where:
//      R is running,
//      S is sleeping in an interruptible wait,
//      D is waiting in uninterruptible disk sleep,
//      Z is zombie,
//      T is traced or stopped(on a signal),
//      W is paging.
//
// and fields 14 and 15 should contain the user/kernel cpu usage
//
// (14) Amount of time that this process has been scheduled in user mode, measured in clock ticks(divide by sysconf(_SC_CLK_TCK)).
//      This includes guest time, guest_time(time spent running a virtual CPU, see below), so that applications that are not aware
//      of the guest time field do not lose that time from their calculations.
//      stime %lu
// (15) Amount of time that this process has been scheduled in kernel mode, measured in clock ticks(divide by sysconf(_SC_CLK_TCK)).
//      cutime %ld
//
// Another solution would be to use clock_gettime but without the Running status available
//    pthread_getcpuclockid(pthread_self(), &clockid);
//    if (clock_gettime(clockid, &cpu_time)) { ... }
//

bool GetCpuInfo(pid_t tid, bool& isRunning, uint64_t& cpuTime)
{
    char statPath[64];
    snprintf(statPath, sizeof(statPath), "/proc/self/task/%d/stat", tid);
    FILE* file = fopen(statPath, "r");
    if (file == nullptr)
    {
        return false;
    }

    // based on https://linux.die.net/man/5/proc
    char state = ' ';   // 3rd position  and 'R' for Running
    int userTime = 0;   // 14th position in clock ticks
    int kernelTime = 0; // 15th position in clock ticks
    bool success =
        fscanf(file, "%*s %*s %c %*s %*s %*s %*s %*s %*s %*s %*s %*s %*s %d %d",
               &state, &userTime, &kernelTime) == 3;
    fclose(file);
    if (!success)
    {
        return false;
    }

    cpuTime = ((userTime + kernelTime) * 1000) / sysconf(_SC_CLK_TCK);
    isRunning = (state == 'R') || (state == 'D') || (state == 'W');
    return true;
}

uint64_t GetThreadCpuTime(ManagedThreadInfo* pThreadInfo)
{
    bool isRunning = false;
    uint64_t cpuTime = 0;
    if (!GetCpuInfo(pThreadInfo->GetOsThreadId(), isRunning, cpuTime))
    {
        return 0;
    }

    return cpuTime;
}

bool IsRunning(ManagedThreadInfo* pThreadInfo, uint64_t& cpuTime)
{
    bool isRunning = false;
    if (!GetCpuInfo(pThreadInfo->GetOsThreadId(), isRunning, cpuTime))
    {
        cpuTime = 0;
        return false;
    }

    return isRunning;
}

} // namespace OsSpecificApi