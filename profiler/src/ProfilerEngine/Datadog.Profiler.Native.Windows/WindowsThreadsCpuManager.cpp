// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ThreadsCpuManager.h"
#include "shared/src/native-src/string.h"

#include "Log.h"
#include "string.h"
#include "SystemTime.h"
#include <iomanip>
#include <tlhelp32.h>

void ThreadsCpuManager::LogCpuTimes()
{
    auto currentPID = ::GetCurrentProcessId();

    // How to list a process threads:  https://docs.microsoft.com/en-us/windows/win32/toolhelp/traversing-the-thread-list
    HANDLE hThreadSnap = INVALID_HANDLE_VALUE;
    THREADENTRY32 threadEntry;

    // even if 0 (or currentPID) is passed as process ID, the filter does not apply
    // as explained in https://docs.microsoft.com/en-us/windows/win32/api/tlhelp32/nf-tlhelp32-createtoolhelp32snapshot
    hThreadSnap = ::CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
    if (hThreadSnap == INVALID_HANDLE_VALUE)
    {
        return;
    }

    threadEntry.dwSize = sizeof(THREADENTRY32);

    // Retrieve information about the first thread,
    // and exit if unsuccessful
    if (!::Thread32First(hThreadSnap, &threadEntry))
    {
        ::CloseHandle(hThreadSnap);
        return;
    }

    // get process time to compute % per thread
    FILETIME processCreationTime, processExitTime, processKernelTime, processUserTime, currentTime;
    ::GetSystemTimePreciseAsFileTime(&currentTime);

    // I don't see why this API would fail for the current process...
    bool isProcessTimesAvailable = ::GetProcessTimes(::GetCurrentProcess(), &processCreationTime, &processExitTime, &processKernelTime, &processUserTime);
    DWORD64 processLifetimeMs = GetTotalMilliseconds(currentTime) - GetTotalMilliseconds(processCreationTime);

    DWORD64 processCpuTimeMs = 0;
    if (isProcessTimesAvailable)
    {
        processCpuTimeMs = GetTotalMilliseconds(processUserTime) + GetTotalMilliseconds(processKernelTime);
    }

    std::stringstream builder;

    builder << "\r\n";
    builder << "Process lifetime = " << processLifetimeMs << " ms";
    builder << "\r\n";
    builder << "   TID |    CPU Time      CPU %      ms/sec   Name"
            << "\r\n";
    builder << "--------------------------------------------------"
            << "\r\n";

    std::lock_guard<std::recursive_mutex> lock(_lockThreads);
    do
    {
        if (threadEntry.th32OwnerProcessID != currentPID)
        {
            continue;
        }

        FILETIME creationTime, exitTime, kernelTime, userTime;
        HANDLE hThread;
        hThread = ::OpenThread(THREAD_QUERY_LIMITED_INFORMATION, FALSE, threadEntry.th32ThreadID);
        if ((hThread == 0) || (!::GetThreadTimes(hThread, &creationTime, &exitTime, &kernelTime, &userTime)))
        {
            builder << std::setw(6) << threadEntry.th32ThreadID << " | ???\r\n";
        }
        else
        {
            auto threadMS = GetTotalMilliseconds(userTime) + GetTotalMilliseconds(kernelTime);
            float percent = -1; // in case GetProcessTimes fails
            float msPerSec = (float)threadMS * 1000 / processLifetimeMs;
            if (processCpuTimeMs != 0)
                percent = ((static_cast<float>(threadMS) * 100) / processCpuTimeMs);

            builder << std::setw(6) << threadEntry.th32ThreadID << " | ";
            builder << std::setw(8) << threadMS << " ms  [";
            builder << std::setw(5) << std::setprecision(2) << percent << " %]   [";
            builder << std::setw(7) << std::setprecision(3) << msPerSec << "]";

            // show the thread name if any
            auto element = _threads.find(threadEntry.th32ThreadID);
            if (element != _threads.end())
            {
                builder << "  ";
                auto name = *element->second->GetName();
                builder << shared::ToString(name);
            }
            builder << "\r\n";
        }

        if (hThread != 0)
        {
            ::CloseHandle(hThread);
        }

    } while (::Thread32Next(hThreadSnap, &threadEntry));

    builder << "\r\n";
    auto output = builder.str();
    Log::Debug(output);

    ::CloseHandle(hThreadSnap);
}
