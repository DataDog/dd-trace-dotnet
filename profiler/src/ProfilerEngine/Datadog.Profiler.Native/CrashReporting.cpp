#include "CrashReporting.h"

#include "unknwn.h"
#include "FfiHelper.h"

#include <iostream>
#include <cstring>

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

extern "C" void __stdcall ReportCrash(int32_t pid, int signal, ResolveManagedMethod resolveCallback)
{
#ifdef _WIN32
    std::cout << "CrashReporting::ReportCrash not implemented on Windows" << std::endl;
#else
    auto crashReporting = CrashReporting::Create(pid, signal);
    crashReporting->ReportCrash(resolveCallback);
#endif
}

CrashReporting::CrashReporting(int32_t pid, int32_t signal)
        : _pid(pid),
        _signal(signal)
{
}

CrashReporting::~CrashReporting()
{
}

void add_tag(ddog_prof_CrashInfo* crashInfo, const char* key, const char* value)
{
    auto result = ddog_crashinfo_add_tag(crashInfo, libdatadog::FfiHelper::StringToCharSlice(std::string_view(key)), libdatadog::FfiHelper::StringToCharSlice(std::string_view(value)));

    if (result.tag == DDOG_PROF_CRASHTRACKER_RESULT_ERR)
    {
        std::cout << "Error setting tag: " << libdatadog::FfiHelper::GetErrorMessage(result.err) << std::endl;
    }
}

void CrashReporting::ReportCrash(ResolveManagedMethod resolveCallback)
{
    auto crashInfoResult = ddog_crashinfo_new();

    if (crashInfoResult.tag == DDOG_PROF_CRASH_INFO_NEW_RESULT_ERR)
    {
        return;
    }

    auto* crashInfo = &crashInfoResult.ok;

    auto threads = GetThreads();

    std::cout << "Inspecting " << threads.size() << " threads...\n";

    for (auto threadId : threads)
    {
        std::cout << "---- Thread " << threadId << "\n";

        auto frames = GetThreadFrames(threadId, resolveCallback);

        ddog_prof_Slice_StackFrame stackTrace;

        auto count = frames.size();

        stackTrace.len = count;

        auto stackFrames = new ddog_prof_StackFrame[count];
        auto stackFrameNames = new ddog_prof_StackFrameNames[count];
        auto strings = new std::string[count];

        stackTrace.ptr = stackFrames;

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

            std::cout << " - " << strings[i] << "\n";

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

        auto result = ddog_crashinfo_set_stacktrace(crashInfo, { threadIdStr.c_str(), threadIdStr.length() }, stackTrace);

        if (result.tag == DDOG_PROF_CRASHTRACKER_RESULT_ERR)
        {
            std::cout << "Error setting stacktrace: " << libdatadog::FfiHelper::GetErrorMessage(result.err) << std::endl;
            return;
        }

        delete[] stackFrames;
        delete[] stackFrameNames;
        delete[] strings;
    }

    //auto sigInfo = "NullReferenceException";
    std::string signalInfo = GetSignalInfo();

    if (!signalInfo.empty())
    {
        ddog_crashinfo_set_siginfo(crashInfo, { _signal, libdatadog::FfiHelper::StringToCharSlice(signalInfo) });
    }

    auto libName = "testCrashTracking";
    auto libVersion = "1.0.0";
    auto family = "csharp";

    const ddog_prof_CrashtrackerMetadata metadata = {
        .profiling_library_name = { libName, std::strlen(libName) },
        .profiling_library_version = { libVersion, std::strlen(libVersion) },
        .family = { family, std::strlen(family) },
    };

    auto result = ddog_crashinfo_set_metadata(crashInfo, metadata);

    if (result.tag == DDOG_PROF_CRASHTRACKER_RESULT_ERR)
    {
        std::cout << "Error setting metadata: " << libdatadog::FfiHelper::GetErrorMessage(result.err) << std::endl;
        return;
    }

    add_tag(crashInfo, "crashreport", "crashreport");
    add_tag(crashInfo, "runtime_name", ".NET");

/*
    const std::string endpointUrl = "file://tmp/crash.txt";
    auto endpoint = ddog_Endpoint_file(libdatadog::FfiHelper::StringToCharSlice(endpointUrl));

    result = ddog_crashinfo_upload_to_endpoint(crashInfo, endpoint, 30);

    if (result.tag == DDOG_PROF_CRASHTRACKER_RESULT_ERR)
    {
        std::cout << "Error uploading to endpoint: " << libdatadog::FfiHelper::GetErrorMessage(result.err) << std::endl;
        return;
    }

    std::cout << "Crash info written in /tmp/crash.txt" << std::endl;
*/

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

    ddog_crashinfo_drop(crashInfo);
}
