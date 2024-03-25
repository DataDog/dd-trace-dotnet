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
    auto crashReporting = CrashReporting::Create();
    crashReporting->ReportCrash(pid, resolveCallback);
#endif
}

CrashReporting::CrashReporting()
{
}

CrashReporting::~CrashReporting()
{
}

void CrashReporting::ReportCrash(int32_t pid, ResolveManagedMethod resolveCallback)
{
    auto crashInfoResult = ddog_crashinfo_new();

    if (crashInfoResult.tag == DDOG_PROF_CRASH_INFO_NEW_RESULT_ERR)
    {
        return;
    }

    auto* crashInfo = &crashInfoResult.ok;

    auto threads = GetThreads(pid);

    for (auto threadId : threads)
    {
        auto frames = GetThreadFrames(pid, threadId, resolveCallback);

        ddog_prof_Slice_StackFrame stackTrace;

        auto count = frames.size();

        stackTrace.len = count;

        auto stackFrames = new ddog_prof_StackFrame[count];
        auto stackFrameNames = new ddog_prof_StackFrameNames[count];
        auto strings = new std::string[count];

        stackTrace.ptr = stackFrames;

        for (int i = 0; i < count; i++)
        {
            strings[i] = frames.at(i).second;

            stackFrameNames[i] = ddog_prof_StackFrameNames{
                .colno = { DDOG_PROF_OPTION_U32_NONE_U32, 0},
                .filename = {nullptr, 0},
                .lineno = { DDOG_PROF_OPTION_U32_NONE_U32, 0},
                .name = libdatadog::FfiHelper::StringToCharSlice(strings[i])
            };

            stackFrames[i] = ddog_prof_StackFrame{
                .ip = frames.at(i).first,
                .module_base_address = 2,
                .names{
                    .ptr = &stackFrameNames[i],
                    .len = 1,
                },
                .sp = 3,
                .symbol_address = 4,
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

    std::cout << "Crash uploaded to endpoint" << std::endl;

    ddog_crashinfo_drop(crashInfo);
}
