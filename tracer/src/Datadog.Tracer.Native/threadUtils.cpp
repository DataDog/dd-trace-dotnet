
#include "threadUtils.h"

#ifdef __linux__
#include <sys/prctl.h>
#elif __APPLE__
#include <pthread.h>
#endif

#ifdef _WIN32

bool Threads::s_isRunTimeLinkingThreadDescriptionDone = false;
Threads::SetThreadDescriptionDelegate_t Threads::s_setThreadDescriptionDelegate = nullptr;

void Threads::InitDelegates_GetSetThreadDescription()
{
    if (s_isRunTimeLinkingThreadDescriptionDone)
    {
        return;
    }

    HMODULE moduleHandle = GetModuleHandle(WStr("KernelBase.dll"));

    if (NULL == moduleHandle)
    {
        moduleHandle = LoadLibrary(WStr("KernelBase.dll"));
    }

    if (NULL != moduleHandle)
    {
        s_setThreadDescriptionDelegate = reinterpret_cast<SetThreadDescriptionDelegate_t>(GetProcAddress(moduleHandle, "SetThreadDescription"));
    }

    s_isRunTimeLinkingThreadDescriptionDone = true;
}

Threads::SetThreadDescriptionDelegate_t Threads::GetDelegate_SetThreadDescription()
{
    SetThreadDescriptionDelegate_t setThreadDescriptionDelegate = s_setThreadDescriptionDelegate;
    if (nullptr == setThreadDescriptionDelegate)
    {
        InitDelegates_GetSetThreadDescription();
        setThreadDescriptionDelegate = s_setThreadDescriptionDelegate;
    }

    return setThreadDescriptionDelegate;
}

#endif // #ifdef _WINDOWS

bool Threads::SetNativeThreadName(const WCHAR* description)
{
#ifdef _WIN32
    // The SetThreadDescription(..) API is only available on recent Windows versions and must be called dynamically.
    // We attempt to link to it at runtime, and if we do not succeed, this operation is a No-Op.
    // https://docs.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-setthreaddescription#remarks

    SetThreadDescriptionDelegate_t setThreadDescriptionDelegate = GetDelegate_SetThreadDescription();
    if (nullptr == setThreadDescriptionDelegate)
    {
        return false;
    }

    HRESULT hr = setThreadDescriptionDelegate(GetCurrentThread(), description);
    return SUCCEEDED(hr);
#elif __linux__
    const auto name = shared::ToString(description);
    prctl(PR_SET_NAME, name.data(), 0, 0, 0);
    return true;
#else
    const auto name = shared::ToString(description);
    pthread_setname_np(name.data());
    return true;
#endif
}
