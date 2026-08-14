// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "Log.h"

#include <atomic>

#ifdef _WINDOWS
#include <Windows.h>
#else
#include <csetjmp>
#include <csignal>
#include <mutex>
#include <pthread.h>

#include "ProfilerSignalManager.h"
#endif

// Recovers from the memory access faults raised by reading memory the profiler does not
// own and cannot fully validate: the object graph walked during a heap dump, and the
// MethodTable of a ClassID that was captured during a dump and is resolved after it.
//
// IMPORTANT: guarded regions must not nest on a given thread. The Linux path keeps a
// single jump buffer per thread, so an inner region would overwrite the outer one's and
// a later fault would jump into a dead frame.
namespace MemoryFaultGuard
{
enum class RunResult
{
    Completed,
    Faulted,
    Unavailable
};

#ifdef DD_TEST
inline std::atomic<bool> s_forceUnavailableForTests{false};

inline void SetUnavailableForTests(bool unavailable)
{
    s_forceUnavailableForTests.store(unavailable, std::memory_order_release);
}
#endif

#ifdef _WINDOWS
namespace details
{
// Only the faults a raw memory read can actually produce are recovered from.
// EXCEPTION_EXECUTE_HANDLER would also swallow stack overflow (leaving the guard page
// unreset for the rest of the process), CLR exceptions and C++ exceptions such as
// std::bad_alloc -- none of which mean "that address was unreadable", and all of which
// the callers would otherwise treat as a recovered fault.
inline int MemoryFaultFilter(DWORD exceptionCode)
{
    switch (exceptionCode)
    {
        case EXCEPTION_ACCESS_VIOLATION:
        case EXCEPTION_DATATYPE_MISALIGNMENT:
        case EXCEPTION_IN_PAGE_ERROR:
            return EXCEPTION_EXECUTE_HANDLER;

        default:
            return EXCEPTION_CONTINUE_SEARCH;
    }
}
} // namespace details

#else

// NOTE (macOS): macOS is not a supported profiler build today
// (profiler/src/CMakeLists.txt fails with "MACOS builds are not supported yet").
// If it is ever enabled, this guard needs a macOS path because ProfilerSignalManager
// lives in the Linux-only project and does not exist there. A macOS port would:
//   - install its own sigaction() for SIGSEGV and SIGBUS (saving the previous actions),
//   - in the handler, siglongjmp when t_inGuardedRegion is set, otherwise manually
//     chain to the saved previous sa_sigaction/sa_handler (or restore SIG_DFL + re-raise
//     when there was none) so real faults keep their original crash semantics,
//   - register once (see EnsureInstalled) so re-creating a component that uses the guard
//     does not save our own handler as the "previous" one.
// The TLS recovery machinery (t_jmpBuf / t_inGuardedRegion / sigsetjmp in Run) is
// portable and would be shared as-is.

namespace details
{
inline thread_local sigjmp_buf t_jmpBuf;
inline thread_local volatile sig_atomic_t t_inGuardedRegion = 0;

// Clears the flag on every way out of the guarded body that still unwinds C++ frames:
// a normal return and, crucially, an escaping exception. Left set, the flag would arm
// the handler for the whole life of the thread, so the next SIGSEGV -- including one
// the CLR would have handled itself -- would siglongjmp into a dead frame.
// The siglongjmp path skips destructors, so the recovery branch clears it by hand.
struct InGuardedRegionScope
{
    InGuardedRegionScope()
    {
        t_inGuardedRegion = 1;
    }

    ~InGuardedRegionScope()
    {
        t_inGuardedRegion = 0;
    }
};

// The guard is entered at least twice per traversed root, and sigsetjmp with
// savemask = 1 costs an rt_sigprocmask syscall every time. Using savemask = 0 instead
// moves that cost to the (rare) recovery path: undo the handler's mask change here so
// a later fault is still deliverable. ProfilerSignalManager installs its handlers
// without SA_NODEFER and with an sa_mask holding only the handled signal, so unblocking
// SIGSEGV and SIGBUS restores exactly what was blocked. This runs after the handler
// frame has been abandoned, i.e. in normal context, so pthread_sigmask is safe to call.
inline void UnblockFaultSignals()
{
    sigset_t faultSignals;
    sigemptyset(&faultSignals);
    sigaddset(&faultSignals, SIGSEGV);
    sigaddset(&faultSignals, SIGBUS);
    pthread_sigmask(SIG_UNBLOCK, &faultSignals, nullptr);
}

// ProfilerSignalManager: return false to chain to the CLR's previous SIGSEGV/SIGBUS
// handler. Inside a guarded region we siglongjmp and do not return.
inline bool FaultHandler(int /*signal*/, siginfo_t* /*info*/, void* /*context*/)
{
    if (t_inGuardedRegion != 0)
    {
        siglongjmp(t_jmpBuf, 1);
    }
    return false;
}
} // namespace details
#endif

// Installs the SIGSEGV/SIGBUS handlers the Linux guard relies on; always succeeds on
// Windows, where SEH needs no registration. Failed Linux registrations are retried by
// the next caller.
inline bool EnsureInstalled()
{
#ifdef DD_TEST
    if (s_forceUnavailableForTests.load(std::memory_order_acquire))
    {
        return false;
    }
#endif

#ifndef _WINDOWS
    static std::atomic<bool> installed{false};
    static std::mutex installLock;

    if (installed.load(std::memory_order_acquire))
    {
        return true;
    }

    std::lock_guard lock(installLock);
    if (installed.load(std::memory_order_relaxed))
    {
        return true;
    }

    auto* segv = ProfilerSignalManager::Get(SIGSEGV);
    bool segvInstalled = segv != nullptr && segv->RegisterHandler(&details::FaultHandler);
    if (!segvInstalled)
    {
        LogOnce(Error, "MemoryFaultGuard failed to register its SIGSEGV handler. "
                       "Reference-chain traversal cannot safely recover from memory access faults.");
    }

    auto* bus = ProfilerSignalManager::Get(SIGBUS);
    bool busInstalled = bus != nullptr && bus->RegisterHandler(&details::FaultHandler);
    if (!busInstalled)
    {
        LogOnce(Error, "MemoryFaultGuard failed to register its SIGBUS handler. "
                       "Reference-chain traversal cannot safely recover from memory access faults.");
    }

    bool success = segvInstalled && busInstalled;
    installed.store(success, std::memory_order_release);
    return success;
#else
    return true;
#endif
}

// Runs body under the platform memory access fault guard: SEH on Windows, a
// SIGSEGV/SIGBUS handler plus siglongjmp on Linux. The body is not invoked when the
// Linux handlers are unavailable.
//
// C++ exceptions are deliberately NOT caught here: they are not memory faults, and the
// callers do not treat them the same way.
//
// IMPORTANT: neither body nor anything it calls may own something that needs
// destruction, because nothing releases it on the fault path: SEH unwinding skips the
// destructors of intervening frames and siglongjmp does not unwind at all. Where that
// cannot be honoured, the caller must be able to live with leaking what the faulting
// call stack held.
template <typename TBody>
RunResult Run(TBody&& body)
{
    if (!EnsureInstalled())
    {
        return RunResult::Unavailable;
    }

    // This function must itself declare no local requiring C++ unwinding, on Windows
    // because MSVC rejects it in a function using __try, on Linux because siglongjmp
    // would skip it. (InGuardedRegionScope below is the one exception: it is there
    // precisely to cover the paths that DO unwind, and the recovery branch reproduces
    // its effect for the path that does not.)
#ifdef _WINDOWS
    __try
    {
        body();
    }
    __except (details::MemoryFaultFilter(GetExceptionCode()))
    {
        return RunResult::Faulted;
    }
#else
    if (sigsetjmp(details::t_jmpBuf, 0) != 0)
    {
        details::UnblockFaultSignals();
        details::t_inGuardedRegion = 0;
        return RunResult::Faulted;
    }

    details::InGuardedRegionScope guardScope;
    body();
#endif

    return RunResult::Completed;
}
} // namespace MemoryFaultGuard
