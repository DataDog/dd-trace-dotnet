// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SystemCallsShield.h"

#include "IConfiguration.h"
#include "ManagedThreadInfo.h"

#include <memory>

#include <sys/syscall.h>
#include <unistd.h>

thread_local std::shared_ptr<ManagedThreadInfo> managedThreadInfo = nullptr;
SystemCallsShield* SystemCallsShield::Instance = nullptr;

extern "C" int (*volatile dd_set_shared_memory)(volatile int*) __attribute__((weak));

// check if this symbol is present to know if the wrapper is loaded
extern "C" unsigned long long dd_inside_wrapped_functions() __attribute__((weak));

SystemCallsShield::SystemCallsShield(IConfiguration* configuration) :
    _isEnabled{ShouldEnable(configuration)},
    _isStarted{false}
{
}

bool SystemCallsShield::ShouldEnable(IConfiguration* configuration)
{
    // Make sure the wrapper is present.
    // Walltime and CPU profilers are the only ones that could interrupt a system calls
    // (It might not be obvious, for the CPU profiler we could be in a race)
    return dd_inside_wrapped_functions != nullptr && (configuration->IsWallTimeProfilingEnabled() || configuration->IsCpuProfilingEnabled());
}

bool SystemCallsShield::Start()
{
    if (_isStarted)
    {
        return true;
    }
    _isStarted = true;

    if (_isEnabled)
    {
        Instance = this;
        dd_set_shared_memory = SystemCallsShield::SetSharedMemory;
    }

    return true;
}

bool SystemCallsShield::Stop()
{
    if (!_isStarted)
    {
        return false;
    }

    if (_isEnabled)
    {
        dd_set_shared_memory = nullptr;
        Instance = nullptr;
    }

    return true;
}

bool SystemCallsShield::IsStarted()
{
    return (_isStarted);
}

const char* SystemCallsShield::GetName()
{
    return "Linux System Calls Shield";
}

void SystemCallsShield::Register(std::shared_ptr<ManagedThreadInfo> const& threadInfo)
{
    if (_isEnabled)
    {
        managedThreadInfo = threadInfo;
    }
}

void SystemCallsShield::Unregister()
{
    if (_isEnabled)
    {
        managedThreadInfo.reset();
    }
}

int SystemCallsShield::SetSharedMemory(volatile int* state)
{
    auto current = Instance;
    if (current == nullptr)
    {
        return 0;
    }

    return current->SetSharedMemoryOnThreadInfo(state);
}

int SystemCallsShield::SetSharedMemoryOnThreadInfo(volatile int* state)
{
    auto& threadInfo = managedThreadInfo;

    if (threadInfo == nullptr)
    {
        return 0;
    }

    threadInfo->SetSharedMemory(state);

    return (state != nullptr) ? 1 : 0;
}
