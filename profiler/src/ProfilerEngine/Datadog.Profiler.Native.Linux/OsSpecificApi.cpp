// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// OsSpecificApi for LINUX

#include <fstream>
#include <string>

#include <sys/syscall.h>
#include "OsSpecificApi.h"
#include "OpSysTools.h"

#include "Log.h"
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
static bool firstError = true;

bool GetCpuInfo(pid_t tid, bool& isRunning, uint64_t& cpuTime)
{
    char statPath[64];
    snprintf(statPath, sizeof(statPath), "/proc/self/task/%d/stat", tid);

    // load the line to be able to parse it in memory
    std::ifstream file;
    file.open(statPath);
    std::string sline;
    std::getline(file, sline);
    file.close();
    if (sline.empty())
    {
        return false;
    }

    char state = ' ';
    int userTime = 0;
    int kernelTime = 0;
    bool success = OpSysTools::ParseThreadInfo(sline, state, userTime, kernelTime);
    if (!success)
    {
        // log the first error to be able to analyze unexpected string format
        if (firstError)
        {
            firstError = false;
            Log::Error("Unexpected /proc/self/task/", tid, "/stat: ", sline);
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