// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// OsSpecificApi for LINUX

#include "OsSpecificApi.h"

#include "LinuxStackFramesCollector.h"
#include "StackFramesCollectorBase.h"
#include "shared/src/native-src/loader.h"

namespace OsSpecificApi {
void InitializeLoaderResourceMonikerIDs(shared::LoaderResourceMonikerIDs*)
{
}

StackFramesCollectorBase* CreateNewStackFramesCollectorInstance(ICorProfilerInfo4* pCorProfilerInfo)
{
    return new LinuxStackFramesCollector(const_cast<ICorProfilerInfo4* const>(pCorProfilerInfo));
}

// TODO: use /proc/<pid>/task/<tid>/stat
// https://linux.die.net/man/5/proc
//
// the third field is the Status:  (R for running)
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

uint64_t GetThreadCpuTime(ManagedThreadInfo* pThreadInfo)
{
    return 0;
}

bool IsRunning(ManagedThreadInfo* pThreadInfo, uint64_t& cpuTime)
{
    return false;
}

} // namespace OsSpecificApi