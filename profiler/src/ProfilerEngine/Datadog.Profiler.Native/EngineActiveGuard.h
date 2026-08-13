// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <shared_mutex>

// Non-blocking guard for ICorProfilerCallback methods that read service pointers whose lifetime
// is tied to CorProfilerCallback::DisposeServices(). The CLR can invoke these callbacks
// concurrently with CorProfilerCallback::Shutdown()/DisposeInternal() tearing services down - with
// no interlock, a callback that reads "the engine looks alive" can still end up touching an
// already-destroyed (or mid-destruction) service afterward. A real crash was traced to exactly
// this: ThreadNameChanged read _isInitialized.load() == true, then CorProfilerCallback::
// DisposeInternal() destroyed ThreadsCpuManager on another thread before the callback reached its
// use of it - a stock std::atomic<bool> check has no way to close that gap, because there is
// nothing serializing "read the flag" against "the object is being destroyed right now".
//
// This class turns "is the engine alive" into a question that can only ever be answered while
// holding the same mutex the teardown path holds exclusively while it tears things down - never
// as a bare, lock-free flag read, which is what made the previous
// `if (false == _isInitialized.load()) return S_OK;` pattern racy in the first place.
//
// Usage in a callback:
//     EngineActiveGuard engineGuard(_engineLifetimeMutex, _isShutdown);
//     if (!engineGuard.IsActive())
//     {
//         return S_OK;
//     }
//     ... safe to use service pointers for engineGuard's lifetime ...
//
// Usage at teardown (CorProfilerCallback::DisposeInternal()):
//     {
//         std::unique_lock<std::shared_mutex> exclusiveLock(_engineLifetimeMutex);
//         _isShutdown = true;   // must be set before the lock is released below, not after
//         DisposeServices();
//     }   // lock releases here (normal RAII) - safe, because any callback that acquires the
//         // lock afterward is guaranteed - via the mutex's own synchronizes-with relationship -
//         // to observe _isShutdown == true, and will no-op before touching any service pointer.
//
// _isShutdown must never be read or written except while holding _engineLifetimeMutex (shared or
// exclusive). Reading it as a bare flag anywhere - even "just this once, for a quick check" -
// reintroduces the exact race this class exists to close.
class EngineActiveGuard
{
public:
    EngineActiveGuard(std::shared_mutex& mutex, bool const& isShutdown) :
        _lock(mutex, std::try_to_lock),
        _isActive(_lock.owns_lock() && !isShutdown)
    {
    }

    ~EngineActiveGuard() = default;
    EngineActiveGuard(EngineActiveGuard const&) = delete;
    EngineActiveGuard& operator=(EngineActiveGuard const&) = delete;
    EngineActiveGuard(EngineActiveGuard&&) = delete;
    EngineActiveGuard& operator=(EngineActiveGuard&&) = delete;

    // True if the engine was confirmed alive (lock acquired, not yet shut down) for the duration
    // of this guard's lifetime. Service pointers may only be used while this is true, and only
    // for as long as this guard object stays in scope.
    bool IsActive() const { return _isActive; }

private:
    std::shared_lock<std::shared_mutex> _lock;
    bool _isActive;
};
