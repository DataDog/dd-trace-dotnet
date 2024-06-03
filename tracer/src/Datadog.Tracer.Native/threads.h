#pragma once

#ifdef _WIN32
#include <windows.h>
#endif

#include "../../../shared/src/native-src/string.h" // NOLINT

class Threads
{
public:
    static bool SetNativeThreadName(const WCHAR* description);

private:
#ifdef _WIN32
    typedef HRESULT(__stdcall* SetThreadDescriptionDelegate_t)(HANDLE threadHandle, PCWSTR pThreadDescription);

    static bool s_isRunTimeLinkingThreadDescriptionDone;
    static SetThreadDescriptionDelegate_t s_setThreadDescriptionDelegate;

    static void InitDelegates_GetSetThreadDescription();
    static SetThreadDescriptionDelegate_t GetDelegate_SetThreadDescription();
#endif
};