// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>
#include <thread>

#ifdef _WINDOWS
#include <atlbase.h>
#else
#include "pal.h"
#include "pal_mstypes.h"
#endif

class OpSysTools final
{
public:
    static int GetProcId();
    static int GetThreadId();
    // static std::string UnicodeToAnsi(const WCHAR* str);

    /// <summary>
    /// Gets nanoseconds since some arbitrary origin from a high precision system timer.
    /// This is not meant for time calculations, but for only for durations.
    /// <i>InitHighPrecisionTimer(..)</i> must be called at least once in the process before calling
    ///  this function.
    /// This function will return non-high-precision values or work slowly if a high precision timer
    /// is not available on the system or if <i>InitHighPrecisionTimer()</i> was not called.
    /// </summary>
    static inline std::int64_t GetHighPrecisionNanoseconds(void);

    static bool InitHighPrecisionTimer(void);

    static inline bool QueryThreadCycleTime(HANDLE handle, PULONG64 cycleTime);
    static inline HANDLE GetCurrentProcess();

    static bool SetNativeThreadName(std::thread* pNativeThread, const WCHAR* description);
    static bool GetNativeThreadName(HANDLE windowsThreadHandle, WCHAR* pThreadDescrBuff, const std::uint32_t threadDescrBuffSize);

    static bool GetModuleHandleFromInstructionPointer(void* nativeIP, std::uint64_t* pModuleHandle);
    static std::string GetModuleName(void* nativeIP);

    static void* AlignedMAlloc(size_t alignment, size_t size);

    static void MemoryBarrierProcessWide(void);

    static std::string GetHostname();
    static std::string GetProcessName();

    static bool ParseThreadInfo(std::string line, char& state, int& userTime, int& kernelTime)
    {
        // based on https://linux.die.net/man/5/proc
        // state  = 3rd position  and 'R' for Running
        // user   = 14th position in clock ticks
        // kernel = 15th position in clock ticks

        // The thread name is in second position and wrapped by ()
        // Since the name can contain SPACE and () characters, skip it before scanning the values
        auto pos = line.find_last_of(")");
        const char* pEnd = line.c_str() + pos + 1;

#ifdef _WINDOWS
        bool result = sscanf_s(pEnd, " %c %*s %*s %*s %*s %*s %*s %*s %*s %*s %*s %d %d", &state, 1, &userTime, &kernelTime) == 3;
#else
        bool result = sscanf(pEnd, " %c %*s %*s %*s %*s %*s %*s %*s %*s %*s %*s %d %d", &state, &userTime, &kernelTime) == 3;
#endif

        return result;
    }

private:
    static constexpr std::int64_t NanosecondsPerSecond = 1000000000;

    static std::int64_t s_nanosecondsPerHighPrecisionTimerTick;
    static std::int64_t s_highPrecisionTimerTicksPerNanosecond;

    static std::int64_t GetHighPrecisionNanosecondsFallback(void);

#ifdef _WINDOWS
    typedef HRESULT(__stdcall* SetThreadDescriptionDelegate_t)(HANDLE threadHandle, PCWSTR pThreadDescription);
    typedef HRESULT(__stdcall* GetThreadDescriptionDelegate_t)(HANDLE hThread, PWSTR* ppThreadDescription);

    static bool s_isRunTimeLinkingThreadDescriptionDone;
    static SetThreadDescriptionDelegate_t s_setThreadDescriptionDelegate;
    static GetThreadDescriptionDelegate_t s_getThreadDescriptionDelegate;

    static void InitDelegates_GetSetThreadDescription(void);
    static SetThreadDescriptionDelegate_t GetDelegate_SetThreadDescription(void);
    static GetThreadDescriptionDelegate_t GetDelegate_GetThreadDescription(void);
#endif
};

inline std::int64_t OpSysTools::GetHighPrecisionNanoseconds(void)
{
#ifdef _WINDOWS
    if (0 != s_nanosecondsPerHighPrecisionTimerTick || 0 != s_highPrecisionTimerTicksPerNanosecond)
    {
        LARGE_INTEGER timerTicks;

        bool canQueryTimer = QueryPerformanceCounter(&timerTicks);
        if (canQueryTimer)
        {
            if (0 != s_nanosecondsPerHighPrecisionTimerTick)
            {
                std::uint64_t nanosecs = timerTicks.QuadPart * s_nanosecondsPerHighPrecisionTimerTick;
                return nanosecs;
            }

            if (0 != s_highPrecisionTimerTicksPerNanosecond)
            {
                std::uint64_t nanosecs = timerTicks.QuadPart / s_highPrecisionTimerTicksPerNanosecond;
                return nanosecs;
            }
        }
    }

    return OpSysTools::GetHighPrecisionNanosecondsFallback();
#else
    // Need to implement this for Linux!
    return OpSysTools::GetHighPrecisionNanosecondsFallback();
#endif
}

inline bool OpSysTools::QueryThreadCycleTime(HANDLE handle, PULONG64 cycleTime)
{
#ifdef _WINDOWS
    BOOL result = ::QueryThreadCycleTime(handle, cycleTime);
    return (result != 0);
#else
    return FALSE;
#endif
}

inline HANDLE OpSysTools::GetCurrentProcess()
{
#ifdef _WINDOWS
    return ::GetCurrentProcess();
#else
    return nullptr;
#endif
}