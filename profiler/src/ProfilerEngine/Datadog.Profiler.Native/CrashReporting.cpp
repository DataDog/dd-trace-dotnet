#include "CrashReporting.h"

#include "unknwn.h"
#include "FfiHelper.h"

#include <iostream>
#include <cstring>

#include <execinfo.h>
#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

extern "C" void* STDMETHODCALLTYPE CreateCrashReport(int32_t pid)
{
#ifdef _WIN32
    std::cout << "CreateCrashReport not implemented on Windows" << std::endl;
    return 1;
#else
    auto instance = CrashReporting::Create(pid);
    instance->AddRef();
    return (IUnknown*)instance;
#endif
}

CrashReporting::CrashReporting(int32_t pid)
        : _pid(pid)
{
    auto crashInfoResult = ddog_crashinfo_new();

    if (crashInfoResult.tag != DDOG_PROF_CRASH_INFO_NEW_RESULT_OK)
    {
        std::cout << "ddog_crashinfo_new failed\n";
    }

    _crashInfo = crashInfoResult.ok;

    AddTag("crashreport", "crashreport");
    AddTag("runtime_name", ".NET");
    
}

CrashReporting::~CrashReporting()
{
    ddog_crashinfo_drop(&_crashInfo);
}

STDMETHODCALLTYPE HRESULT CrashReporting::QueryInterface(REFIID riid, void** ppvObject)
{
    if (riid == __uuidof(IUnknown) || riid == __uuidof(ICrashReporting))
    {
        *ppvObject = (ICrashReporting*)this;
        AddRef();
        return S_OK;
    }

    return E_NOINTERFACE;
}

STDMETHODCALLTYPE ULONG CrashReporting::AddRef()
{
    return ++_refCount;
}

STDMETHODCALLTYPE ULONG CrashReporting::Release()
{
    auto newCount = --_refCount;

    if (newCount == 0)
    {
        delete(this);
    }

    return newCount;
}

STDMETHODCALLTYPE int32_t CrashReporting::AddTag(const char* key, const char* value)
{
    auto result = ddog_crashinfo_add_tag(&_crashInfo, libdatadog::FfiHelper::StringToCharSlice(std::string_view(key)), libdatadog::FfiHelper::StringToCharSlice(std::string_view(value)));

    if (result.tag == DDOG_PROF_CRASHTRACKER_RESULT_ERR)
    {
        return 1;
    }

    return 0;
}

STDMETHODCALLTYPE int32_t CrashReporting::SetSignalInfo(int32_t signal, const char* description)
{
    std::string signalInfo;

    if (description == nullptr)
    {
        // If no signal description is provided, try to populate it
        signalInfo = GetSignalInfo(signal);
    }
    else
    {
        signalInfo = std::string(description);
    }

    ddog_crashinfo_set_siginfo(&_crashInfo, { (int64_t)signal, libdatadog::FfiHelper::StringToCharSlice(signalInfo) });

    return 0;
}

STDMETHODCALLTYPE int32_t CrashReporting::ResolveStacks(int32_t crashingThreadId, ResolveManagedMethod resolveCallback)
{
    auto threads = GetThreads();

    std::cout << "Inspecting " << threads.size() << " threads...\n";

    for (auto threadId : threads)
    {
        auto frames = GetThreadFrames(threadId, resolveCallback);

        ddog_prof_Slice_StackFrame stackTrace;

        auto count = frames.size();

        stackTrace.len = count;

        auto stackFrames = std::make_unique<ddog_prof_StackFrame[]>(count);
        auto stackFrameNames = std::make_unique<ddog_prof_StackFrameNames[]>(count);
        auto strings = std::make_unique<std::string[]>(count);

        stackTrace.ptr = stackFrames.get();

        for (int i = 0; i < count; i++)
        {
            auto frame = frames.at(i);

            strings[i] = frame.method;

            stackFrameNames[i] = ddog_prof_StackFrameNames{
                .colno = { DDOG_PROF_OPTION_U32_NONE_U32, 0},
                .filename = {nullptr, 0},
                .lineno = { DDOG_PROF_OPTION_U32_NONE_U32, 0},
                .name = libdatadog::FfiHelper::StringToCharSlice(strings[i])
            };

            auto ip = static_cast<uintptr_t>(frame.ip);
            auto moduleAddress = static_cast<uintptr_t>(frame.moduleAddress);
            auto symbolAddress = static_cast<uintptr_t>(frame.symbolAddress);

            stackFrames[i] = ddog_prof_StackFrame{
                .ip = ip,
                .module_base_address = moduleAddress,
                .names{
                    .ptr = &stackFrameNames[i],
                    .len = 1,
                },
                .sp = 0,
                .symbol_address = symbolAddress,
            };
        }

        auto threadIdStr = std::to_string(threadId);

        auto result = ddog_crashinfo_set_stacktrace(&_crashInfo, { threadIdStr.c_str(), threadIdStr.length() }, stackTrace);

        if (result.tag == DDOG_PROF_CRASHTRACKER_RESULT_ERR)
        {
            std::cout << "Error setting stacktrace: " << libdatadog::FfiHelper::GetErrorMessage(result.err) << std::endl;
            continue;
        }

        if (threadId == crashingThreadId)
        {
            // Setting the default stacktrace
            result = ddog_crashinfo_set_stacktrace(&_crashInfo, { nullptr, 0 }, stackTrace);

            if (result.tag == DDOG_PROF_CRASHTRACKER_RESULT_ERR)
            {
                std::cout << "Error setting default stacktrace: " << libdatadog::FfiHelper::GetErrorMessage(result.err) << std::endl;
                continue;
            }
        }
    }

    return 0;
}

STDMETHODCALLTYPE int32_t CrashReporting::Send()
{
    auto libName = "testCrashTracking";
    auto libVersion = "1.0.0";
    auto family = "csharp";

    const ddog_prof_CrashtrackerMetadata metadata = {
        .profiling_library_name = { libName, std::strlen(libName) },
        .profiling_library_version = { libVersion, std::strlen(libVersion) },
        .family = { family, std::strlen(family) },
    };

    auto result = ddog_crashinfo_set_metadata(&_crashInfo, metadata);

    if (result.tag == DDOG_PROF_CRASHTRACKER_RESULT_ERR)
    {
        std::cout << "Error setting metadata: " << libdatadog::FfiHelper::GetErrorMessage(result.err) << std::endl;
    }

    const std::string endpointUrl = "file://tmp/crash.txt";
    auto endpoint = ddog_Endpoint_file(libdatadog::FfiHelper::StringToCharSlice(endpointUrl));

    result = ddog_crashinfo_upload_to_endpoint(&_crashInfo, endpoint, 30);

    int32_t statusCode = 0;

    if (result.tag == DDOG_PROF_CRASHTRACKER_RESULT_ERR)
    {
        std::cout << "Error uploading to endpoint: " << libdatadog::FfiHelper::GetErrorMessage(result.err) << std::endl;
        statusCode = 1;
    }

    std::cout << "Crash info written in /tmp/crash.txt" << std::endl;

/*
    std::string url = "http://172.30.64.1:8126/";
    ddog_prof_CrashtrackerConfiguration config;
    config.endpoint = ddog_prof_Endpoint_agent(DDOG_CHARSLICE_C("http://172.30.64.1:8126/")),
    config.path_to_receiver_binary = DDOG_CHARSLICE_C("FIXME - point me to receiver binary path");

    result = ddog_crashinfo_upload_to_telemetry(crashInfo, config);

    if (result.tag == DDOG_PROF_CRASHTRACKER_RESULT_ERR)
    {
        std::cout << "Error uploading to endpoint: " << libdatadog::FfiHelper::GetErrorMessage(result.err) << std::endl;
        return;
    }

    std::cout << "Crash info uploaded to Datadog" << std::endl;
*/
    return statusCode;
}