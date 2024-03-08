#include "CrashReporting.h"

#include "FfiHelper.h"

#include <iostream>
#include <cstring>

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
#include "datadog/telemetry.h"
}

extern "C" void __stdcall ReportCrash(char** frames, int count, char* threadId)
{
    CrashReporting::ReportCrash(frames, count, threadId);
}

void CrashReporting::ReportCrash(char** frames, int count, char* threadId)
{
    auto crashInfoResult = ddog_crashinfo_new();

    if (crashInfoResult.tag == DDOG_PROF_CRASH_INFO_NEW_RESULT_ERR)
    {
        return;
    }

    auto* crashInfo = &crashInfoResult.ok;

    ddog_prof_Slice_StackFrame stackTrace;

    stackTrace.len = count;

    auto stackFrames = new ddog_prof_StackFrame[count];
    auto stackFrameNames = new ddog_prof_StackFrameNames[count];
    auto strings = new std::string[count];

    stackTrace.ptr = stackFrames;

    for (int i = 0; i < count; i++)
    {
        strings[i] = frames[i];

        stackFrameNames[i] = ddog_prof_StackFrameNames{
            .colno = { DDOG_PROF_OPTION_U32_NONE_U32, 0},
            .filename = {nullptr, 0},
            .lineno = { DDOG_PROF_OPTION_U32_NONE_U32, 0},
            .name = libdatadog::FfiHelper::StringToCharSlice(strings[i])
        };

        stackFrames[i] = ddog_prof_StackFrame{
            .ip = 1,
            .module_base_address = 2,
            .names{
                .ptr = &stackFrameNames[i],
                .len = 1,
            },
            .sp = 3,
            .symbol_address = 4,
        };
    }

    auto result = ddog_crashinfo_set_stacktrace(crashInfo, { threadId, std::strlen(threadId) }, stackTrace);

    if (result.tag == DDOG_PROF_CRASHTRACKER_RESULT_ERR)
    {
        std::cout << "Error setting stacktrace: " << libdatadog::FfiHelper::GetErrorMessage(result.err);
        return;
    }

    const std::string endpointUrl = "file:///tmp/crash.txt";

    auto endpoint = ddog_endpoint_from_url(libdatadog::FfiHelper::StringToCharSlice(endpointUrl));

    const std::string stdErr = "/tmp/crash_stderr.txt";
    const std::string stdOut = "/tmp/crash_stdout.txt";

    ddog_prof_CrashtrackerConfiguration config = {
        .collect_stacktrace = false,
        .create_alt_stack = false,
        .endpoint = *endpoint,
        .optional_stderr_filename = libdatadog::FfiHelper::StringToCharSlice(stdErr),
        .optional_stdout_filename = libdatadog::FfiHelper::StringToCharSlice(stdOut),
        .path_to_receiver_binary = {nullptr, 0},
        .resolve_frames = DDOG_PROF_CRASHTRACKER_RESOLVE_FRAMES_NEVER,
        .timeout_secs = 30
    };

    result = ddog_crashinfo_upload_to_endpoint(crashInfo, config);

    if (result.tag == DDOG_PROF_CRASHTRACKER_RESULT_ERR)
    {
        std::cout << "Error uploading to endpoint: " << libdatadog::FfiHelper::GetErrorMessage(result.err);
        return;
    }

    for (int i = 0; i < count; i++)
    {
        delete[] stackFrames[i].names.ptr;
    }

    delete[] stackFrames;
    delete[] stackFrameNames;

    ddog_endpoint_drop(endpoint);
    ddog_crashinfo_drop(crashInfo);
}
