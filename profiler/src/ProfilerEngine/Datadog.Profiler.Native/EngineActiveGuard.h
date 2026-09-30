// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <atomic>
#include <shared_mutex>

// This class checks the engine state and make sure that services are not not access during teardown
// or before the engine is initialized.
class EngineActiveGuard
{
public:
    EngineActiveGuard(std::atomic<bool> const& isInitialized, std::shared_mutex& mutex, bool const& isServicesShutdown) :
        _lock(mutex, std::try_to_lock),
        _isActive(isInitialized.load() && _lock.owns_lock() && !isServicesShutdown)
    {
    }

    ~EngineActiveGuard() = default;
    EngineActiveGuard(EngineActiveGuard const&) = delete;
    EngineActiveGuard& operator=(EngineActiveGuard const&) = delete;
    EngineActiveGuard(EngineActiveGuard&&) = delete;
    EngineActiveGuard& operator=(EngineActiveGuard&&) = delete;

    // True if the engine was confirmed initialized-and-not-yet-shut-down for the duration of this
    // guard's lifetime. Service pointers may only be used while this is true, and only for as
    // long as this guard object stays in scope.
    bool IsActive() const { return _isActive; }

private:
    std::shared_lock<std::shared_mutex> _lock;
    bool _isActive;
};
