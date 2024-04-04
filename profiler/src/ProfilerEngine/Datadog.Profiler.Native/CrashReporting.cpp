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

extern "C" void __stdcall ReportCrash(int32_t pid, ResolveManagedMethod resolveCallback)
{
#ifdef _WIN32
    std::cout << "CrashReporting::ReportCrash not implemented on Windows" << std::endl;
#else
    auto crashReporting = CrashReporting::Create(pid);
    crashReporting->ReportCrash(resolveCallback);
#endif
}

CrashReporting::CrashReporting(int32_t pid)
        : _pid(pid)
{
}

CrashReporting::~CrashReporting()
{
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

    //ddog_crashinfo_set_siginfo(crashInfo, { 139, { sigInfo, std::strlen(sigInfo)} });

    auto libName = "testCrashTracking";
    auto libVersion = "1.0.0";
    auto family = "csharp";
    auto tag1 = "tag1";
    auto value1 = "value1";
    auto tag2 = "tag2";
    auto value2 = "value2";

    const ddog_prof_CrashtrackerMetadata metadata = {
        .profiling_library_name = { libName, std::strlen(libName) },
        .profiling_library_version = { libVersion, std::strlen(libVersion) },
        .family = { family, std::strlen(family) }
    };

    auto result = ddog_crashinfo_set_metadata(crashInfo, metadata);

    if (result.tag == DDOG_PROF_CRASHTRACKER_RESULT_ERR)
    {
        std::cout << "Error setting metadata: " << libdatadog::FfiHelper::GetErrorMessage(result.err) << std::endl;
        return;
    }

    const std::string endpointUrl = "file://tmp/crash.txt";

    auto endpoint = ddog_Endpoint_file(libdatadog::FfiHelper::StringToCharSlice(endpointUrl));

    result = ddog_crashinfo_upload_to_endpoint(crashInfo, endpoint, 30);

    if (result.tag == DDOG_PROF_CRASHTRACKER_RESULT_ERR)
    {
        std::cout << "Error uploading to endpoint: " << libdatadog::FfiHelper::GetErrorMessage(result.err) << std::endl;
        return;
    }

    std::cout << "Crash info uploaded to Datadog" << std::endl;

    ddog_crashinfo_drop(crashInfo);
}
