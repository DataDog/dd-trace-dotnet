
#include "threadUtils.h"
#include "logger.h"

#ifdef __linux__
#include <sys/prctl.h>
#include <pthread.h>
#include <sys/resource.h>
#include <sys/time.h>
#include <errno.h>
#include <sys/syscall.h>
#include <unistd.h>
#elif __APPLE__
#include <pthread.h>
#include <sys/resource.h>
#include <sys/time.h>
#include <errno.h>
#elif _WIN32
#include <windows.h>
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

void Threads::RaiseThreadPriorityAboveNormal()
{
#ifdef _WIN32
    if (!::SetThreadPriority(::GetCurrentThread(), THREAD_PRIORITY_ABOVE_NORMAL))
    {
        trace::Logger::Warn("SetThreadPriority(ABOVE_NORMAL) failed – GLE=%d", ::GetLastError());
    }

#elif defined(__APPLE__)
    // One level above default (“Utility”) but below UI-critical.
    if (pthread_set_qos_class_self_np(QOS_CLASS_USER_INITIATED, 0) != 0)
    {
        trace::Logger::Warn("pthread_set_qos_class_self_np(USER_INITIATED) failed – errno=%d", errno);

        // Fallback: modest nice() boost for the whole process.
        if (setpriority(PRIO_PROCESS, 0, -5) != 0)
        {
            trace::Logger::Warn("setpriority(-5) fallback failed – errno=%d", errno);
        }
    }

#elif defined(__linux__)
    // true per-thread tweak: target the kernel thread ID (TID)
    const pid_t tid        = static_cast<pid_t>(::syscall(SYS_gettid));
    constexpr int kDelta   = -5;          // “above normal” without RT

    if (::setpriority(PRIO_PROCESS, tid, kDelta) != 0)
    {
        trace::Logger::Warn("setpriority(TID=%d, %d) failed – errno=%d", tid, kDelta, errno);
    }
#endif
}