#include "CrashReporting.h"

#include "FfiHelper.h"

#include <iostream>

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
    stackTrace.ptr = stackFrames;

    for (int i = 0; i < count; i++)
    {
        auto names = new ddog_prof_StackFrameNames();

        names->name = {frames[i], std::strlen(frames[i])};

        stackFrames[i] = ddog_prof_StackFrame{
            .ip = 1,
            .module_base_address = 2,
            .names{
                .ptr = names,
                .len = 1,
            },
            .sp = 3,
            .symbol_address = 4,
        };
    }

    auto result = ddog_crashinfo_set_stacktrace(crashInfo, {threadId, std::strlen(threadId)}, stackTrace);

    if (result.tag == DDOG_PROF_CRASHTRACKER_RESULT_ERR)
    {
        std::cout << "Error: " << libdatadog::FfiHelper::GetErrorMessage(result.err);
    }
    else
    {
        std::cout << "Success";
    }

    for (int i = 0; i < count; i++)
    {
        delete[] stackFrames[i].names.ptr;
    }

    delete[] stackFrames;

    ddog_crashinfo_drop(crashInfo);
}
