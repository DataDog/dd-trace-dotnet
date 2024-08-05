// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// OsSpecificApi for LINUX

#include "OsSpecificApi.h"

#include <cstdlib>
#include <iostream>
#include <chrono>
#include <fstream>
#include <string>
#include <sstream>

#include <errno.h>
#include <fcntl.h>
#include <mutex>
#include <string.h>
#include <sys/stat.h>
#include <sys/syscall.h>
#include <sys/sysinfo.h>
#include <sys/types.h>
#include <unistd.h>

#include "OpSysTools.h"
#include "ScopeFinalizer.h"

#include "IConfiguration.h"
#include "IThreadInfo.h"
#include "LibrariesInfoCache.h"
#include "LinuxStackFramesCollector.h"
#include "LinuxThreadInfo.h"
#include "Log.h"
#include "OpSysTools.h"
#include "ProfilerSignalManager.h"
#include "StackFramesCollectorBase.h"
#include "shared/src/native-src/loader.h"

class CallstackProvider;

namespace OsSpecificApi {

using namespace std::chrono_literals;

// it's safe to cache it. According to the man sysconf, this value does
// not change during the lifetime of the process.
static auto ticks_per_second = sysconf(_SC_CLK_TCK);

std::pair<DWORD, std::string> GetLastErrorMessage()
{
    DWORD errorCode = errno;
    std::stringstream builder;
    builder << "(error code = 0x" << std::dec << errorCode << ")";
    builder << ": " << strerror(errorCode);

    std::string message = builder.str();
    return std::make_pair(errorCode, message);
}


std::unique_ptr<StackFramesCollectorBase> CreateNewStackFramesCollectorInstance(
    ICorProfilerInfo4* pCorProfilerInfo,
    IConfiguration const* const pConfiguration,
    CallstackProvider* callstackProvider)
{
    return std::make_unique<LinuxStackFramesCollector>(ProfilerSignalManager::Get(SIGUSR1), pConfiguration, callstackProvider, LibrariesInfoCache::Get());
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

bool BuildThreadStatPath(pid_t tid, char* statPath, int capacity)
{
    strncpy(statPath, "/proc/self/task/", 16);
    int base = 1000000000;

    // Adjust the base
    while (base > tid)
    {
        base /= 10;
    }

    int offset = 16;
    // Write each number to the string
    while (base > 0 && offset < 64)
    {
        statPath[offset++] = (tid / base) + '0';
        tid %= base;
        base /= 10;
    }

    // check in case of misusage
    if (offset >= capacity || offset + 5 >= capacity)
    {
        return false;
    }

    strncpy(statPath + offset, "/stat", 5);

    return true;
}

bool GetCpuInfo(pid_t tid, bool& isRunning, uint64_t& cpuTime)
{
    char statPath[64] = {0};

    if (!BuildThreadStatPath(tid, statPath, 64))
    {
        return false;
    }

    auto fd = open(statPath, O_RDONLY);

    if (fd == -1)
    {
        return false;
    }

    on_leave { close(fd); };

    // 1023 + 1 to ensure that the last char is a null one
    // initialize the whole array slots to 0
    char line[1024] = {0};

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

    cpuTime = ((userTime + kernelTime) * 1000) / ticks_per_second;
    isRunning = (state == 'R') || (state == 'D') || (state == 'W');
    return true;
}

uint64_t GetThreadCpuTime(IThreadInfo* pThreadInfo)
{
    bool isRunning = false;
    uint64_t cpuTime = 0;
    if (!GetCpuInfo(pThreadInfo->GetOsThreadId(), isRunning, cpuTime))
    {
        return 0;
    }

    return cpuTime;
}

bool IsRunning(IThreadInfo* pThreadInfo, uint64_t& cpuTime, bool& failed)
{
    bool isRunning = false;
    if (!GetCpuInfo(pThreadInfo->GetOsThreadId(), isRunning, cpuTime))
    {
        cpuTime = 0;
        failed = true;
        return false;
    }

    failed = false;
    return isRunning;
}

// from https://linux.die.net/man/3/get_nprocs
//
int32_t GetProcessorCount()
{
    return get_nprocs();
}

std::vector<int32_t> GetProcessThreads(int32_t pid)
{
    DIR* proc_dir;
    char dirname[100];
    std::string pidPath = (pid == -1) ? "self" : std::to_string(pid);

    snprintf(dirname, sizeof(dirname), "/proc/%s/task", pidPath.c_str());

    proc_dir = opendir(dirname);

    std::vector<int32_t> threads;

    if (proc_dir != nullptr)
    {
        on_leave{ closedir(proc_dir); };
        threads.reserve(512);

        /* /proc available, iterate through tasks... */
        struct dirent* entry;
        while ((entry = readdir(proc_dir)) != nullptr)
        {
            if (entry->d_name[0] == '.')
                continue;
            auto threadId = atoi(entry->d_name);
            threads.push_back(threadId);
        }
    }
    else
    {
        static bool alreadyLogged = false;
        if (!alreadyLogged)
        {
            alreadyLogged = true;
            auto errorNumber = errno;
            Log::Error("Failed at opendir ", dirname, " error: ", strerror(errorNumber));
        }
    }

    return threads;
}

std::vector<std::shared_ptr<IThreadInfo>> GetProcessThreads()
{
    DIR* proc_dir;
    char dirname[100] = "/proc/self/task";
    proc_dir = opendir(dirname);

    std::vector<std::shared_ptr<IThreadInfo>> threads;

    if (proc_dir != nullptr)
    {
        on_leave { closedir(proc_dir); };
        threads.reserve(512);

        /* /proc available, iterate through tasks... */
        struct dirent* entry;
        while ((entry = readdir(proc_dir)) != nullptr)
        {
            if (entry->d_name[0] == '.')
                continue;
            auto threadId = atoi(entry->d_name);
            threads.push_back(std::make_shared<LinuxThreadInfo>(threadId, OpSysTools::GetNativeThreadName(threadId)));
        }
    }
    else
    {
        static bool alreadyLogged = false;
        if (!alreadyLogged)
        {
            alreadyLogged = true;
            auto errorNumber = errno;
            Log::Error("Failed at opendir ", dirname, " error: ", strerror(errorNumber));
        }
    }

    return threads;
}

std::chrono::seconds GetMachineBootTime()
{
    char statPath[] = "/proc/stat";

    auto fd = open(statPath, O_RDONLY);

    if (fd == -1)
    {
        return -1s;
    }

    on_leave { close(fd); };

    // 1023 + 1 to ensure that the last char is a null one
    // initialize the whole array slots to 0
    char line[1024] = {0};
    std::int64_t machineBootTime = -1;
    std::int64_t length = 0;
    while ((length = read(fd, line, sizeof(line) - 1)) != 0)
    {
        auto sv = std::string_view(line, length);
        auto pos = sv.find("btime");
        if (std::string_view::npos != pos)
        {
            auto pos2 = strchr(sv.data() + pos, ' ') + 1;
            if (pos2 == nullptr)
                break;

            // skip whitespaces
            pos2 = pos2 + strspn(pos2, " ");
            machineBootTime = std::atoll(pos2);
            break;
        }
    }

    return std::chrono::seconds(machineBootTime);
}

std::chrono::seconds GetProcessStartTimeSinceBoot()
{
    char statPath[] = "/proc/self/stat";

    auto fd = open(statPath, O_RDONLY);

    if (fd == -1)
    {
        return -1s;
    }

    on_leave { close(fd); };

    // 1023 + 1 to ensure that the last char is a null one
    // initialize the whole array slots to 0
    char line[1024] = {0};

    auto length = read(fd, line, sizeof(line) - 1);
    if (length <= 0)
    {
        return -1s;
    }

    uint8_t nbEltToSkip = 21;
    uint8_t idx = 0;
    auto* pos = line;
    while (idx < nbEltToSkip)
    {
        pos = strchr(pos, ' ');
        if (pos == nullptr)
            break;

        // skip whitespaces
        pos = pos + strspn(pos, " ");

        idx++;
    }

    if (pos == nullptr)
    {
        return -1s;
    }

    auto startTimeSinceBoot = atoll(pos);
    return std::chrono::seconds(startTimeSinceBoot / ticks_per_second);
}

std::string GetProcessStartTime()
{
    // TODO see if we need to cache it later
    // This function is called once every minute.
    auto machineBootTime = GetMachineBootTime();
    if (machineBootTime == -1s)
    {
        return "";
    }

    auto processStartTimeSinceBoot = GetProcessStartTimeSinceBoot();

    if (processStartTimeSinceBoot == -1s)
    {
        return "";
    }

    auto time = (std::time_t)(machineBootTime.count() + processStartTimeSinceBoot.count());
    auto tm = *std::gmtime(&time);

    std::stringstream ss;
    // This format is equivalent to ISO 8601 date and time: %Y-%m-%dT%%H:%M:%SZ
    // Adding xxxxTxxxxZ to mimic what's done for windows. See OsSpecificAPI.cpp in windows folder
    ss << std::put_time(&tm, "%FT%TZ");
    return ss.str();
}

std::unique_ptr<IEtwEventsManager> CreateEtwEventsManager(
    IAllocationsListener* pAllocationListener,
    IContentionListener* pContentionListener,
    IGCSuspensionsListener* pGCSuspensionsListener,
    IConfiguration* pConfiguration)
{
    // No ETW implementation on Linux
    return nullptr;
}

double GetProcessLifetime()
{
    auto machineBootTime = GetMachineBootTime();
    if (machineBootTime == -1s)
    {
        return 0;
    }

    auto processStartTimeSinceBoot = GetProcessStartTimeSinceBoot();
    if (processStartTimeSinceBoot == -1s)
    {
        return 0;
    }

    std::chrono::seconds now = std::chrono::duration_cast<std::chrono::seconds>(std::chrono::system_clock::now().time_since_epoch());
    auto startTimeIsSeconds = now.count() - (machineBootTime.count() + processStartTimeSinceBoot.count());
    return startTimeIsSeconds;
}

} // namespace OsSpecificApi