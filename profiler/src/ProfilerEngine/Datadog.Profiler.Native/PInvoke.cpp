// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "PInvoke.h"
#include "CorProfilerCallback.h"
#include "IClrLifetime.h"
#include "Log.h"
#include "ManagedThreadList.h"
#include "ProfilerEngineStatus.h"
#include "ThreadsCpuManager.h"

#include "shared/src/native-src/loader.h"

// There is a race condition when dealing with CLR shutdown:
// it could happen AFTER the CorProfilerCallback::GetClrLifetime()->IsRunning() check
// In that case, exceptions could be thrown when ICorProfilerInfo methods are called
// These macros deal with that scenario by catching exceptions
//
#define PROTECT_ENTER \
    try               \
    {
#define PROTECT_LEAVE(name)                                \
    }                                                      \
    catch (...)                                            \
    {                                                      \
        Log::Info(name, " is called AFTER CLR shutdown."); \
    }                                                      \
    return FALSE;

//
// P/Invoke calls are "protected" against being called AFTER ICorProfilerCallback::Shutdown
// Error message is added to the log as Info and not Error so the CI is not blocked when
// this happens
//

extern "C" void __stdcall ThreadsCpuManager_Map(std::uint32_t threadId, const WCHAR* pName)
{
    auto profiler = CorProfilerCallback::GetInstance();
    if (profiler == nullptr)
    {
        Log::Error("ThreadsCpuManager_Map is called BEFORE CLR initialize");
        return;
    }

    profiler->GetThreadsCpuManager()->Map(threadId, pName);
}

extern "C" void* __stdcall GetNativeProfilerIsReadyPtr()
{
    if (!CorProfilerCallback::GetClrLifetime()->IsRunning())
    {
        return nullptr;
    }

    return (void*)ProfilerEngineStatus::GetReadPtrIsProfilerEngineActive();
}

extern "C" void* __stdcall GetPointerToNativeTraceContext()
{
    if (!CorProfilerCallback::GetClrLifetime()->IsRunning())
    {
        return nullptr;
    }

    // Engine is active. Get info for current thread.
    auto profiler = CorProfilerCallback::GetInstance();
    if (profiler == nullptr)
    {
        Log::Error("GetPointerToNativeTraceContext is called BEFORE CLR initialize");
        return nullptr;
    }

    ManagedThreadInfo* pCurrentThreadInfo;
    HRESULT hr = profiler->GetManagedThreadList()->TryGetCurrentThreadInfo(&pCurrentThreadInfo);
    if (FAILED(hr))
    {
        // There was an error looking up the current thread info:
        return nullptr;
    }

    if (S_FALSE == hr)
    {
        // There was no error looking up the current thread, but we are not tracking any info for this thread:
        return nullptr;
    }

    // Get pointers to the relevant fields within the thread info data structure.
    return pCurrentThreadInfo->GetTraceContextPointer();
}

extern "C" void __stdcall SetApplicationInfoForAppDomain(const char* runtimeId, const char* serviceName, const char* environment, const char* version)
{
    if (!CorProfilerCallback::GetClrLifetime()->IsRunning())
    {
        return;
    }

    // Engine is active. Get info for current thread.
    const auto profiler = CorProfilerCallback::GetInstance();

    if (profiler == nullptr)
    {
        Log::Error("SetApplicationInfo is called BEFORE CLR initialize");
        return;
    }

    profiler->GetApplicationStore()->SetApplicationInfo(
        runtimeId ? runtimeId : std::string(),
        serviceName ? serviceName : std::string(),
        environment ? environment : std::string(),
        version ? version : std::string());
}