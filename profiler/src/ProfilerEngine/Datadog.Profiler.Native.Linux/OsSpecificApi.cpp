// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// OsSpecificApi for LINUX

#include <fstream>
#include <string>

#ifdef LINUX
#include <unistd.h>
#include <sys/types.h>
#include <sys/stat.h>
#include <fcntl.h>
#endif

#include <sys/syscall.h>
#include "OsSpecificApi.h"
#include "OpSysTools.h"
#include "ScopeFinalizer.h"

#include "IConfiguration.h"
#include "Log.h"
#include "LinuxStackFramesCollector.h"
#include "ProfilerSignalManager.h"
#include "StackFramesCollectorBase.h"
#include "shared/src/native-src/loader.h"


namespace OsSpecificApi {
std::unique_ptr<StackFramesCollectorBase> CreateNewStackFramesCollectorInstance(ICorProfilerInfo4* pCorProfilerInfo, IConfiguration const* const pConfiguration)
{
    return std::make_unique<LinuxStackFramesCollector>(ProfilerSignalManager::Get(), pConfiguration);
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
    char statPath[64] = {'\0'};
    snprintf(statPath, sizeof(statPath), "/proc/self/task/%d/stat", tid);

    auto fd = open(statPath, O_RDONLY);

    if (fd == -1)
    {
        return false;
    }

    on_leave { close(fd); };

    // 1023 + 1 to ensure that the last char is a null one
    // initialize the whole array slots to 0
    char line[1024] = { 0 };

    auto length = read(fd, line, sizeof(line) - 1);
    if (length <= 0)
    {
        return false;
    }

    char state = ' ';
    int32_t userTime = 0;
    int32_t kernelTime = 0;
    bool success = OpSysTools::ParseThreadInfo(line, state, userTime, kernelTime);
    if (!success)
    {
        static bool firstError = true;
        // log the first error to be able to analyze unexpected string format
        if (firstError)
        {
            firstError = false;
            Log::Info("Unexpected line format in ", statPath, ": ", line);
        }

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