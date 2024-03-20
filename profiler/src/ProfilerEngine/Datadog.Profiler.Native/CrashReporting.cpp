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
        std::cout << "Error setting stacktrace: " << libdatadog::FfiHelper::GetErrorMessage(result.err) << std::endl;
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

    std::cout << "Called ddog_crashinfo_drop" << std::endl;

    for (int i = 0; i < count; i++)
    {
        delete[] stackFrames[i].names.ptr;
    }

    delete[] stackFrames;
    delete[] stackFrameNames;
}
