// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "StackFramesCollectorBase.h"

#include <condition_variable>
#include <memory>
#include <mutex>
#include <signal.h>

class LinuxStackFramesCollector : public StackFramesCollectorBase
{
public:
    explicit LinuxStackFramesCollector(ICorProfilerInfo4* const _pCorProfilerInfo);
    ~LinuxStackFramesCollector() override;
    LinuxStackFramesCollector(LinuxStackFramesCollector const&) = delete;
    LinuxStackFramesCollector& operator=(LinuxStackFramesCollector const&) = delete;

protected:
    // Linux collector is different from Windows:
    // There is no notion to Suspend/Resume a thread and to have an external thread walk the suspended thread.
    // The thread that will call CollectStackSample(..), will send a signal to the target thread and then
    // wait until the target thread finished walking its callstack.
    // So, for ResumeThread and SuspendThread are No Ops for this collector, and we defer to the respective baseclass No-Op methods.

    StackSnapshotResultBuffer* CollectStackSampleImplementation(ManagedThreadInfo* pThreadInfo,
                                                                uint32_t* pHR) override;

private:
    bool SetupSignalHandler();
    void NotifyStackWalkCompleted(std::int32_t resultErrorCode);

    int _signalToSend;
    bool _isSignalHandlerSetup;

    std::int32_t _lastStackWalkErrorCode;
    std::condition_variable _stackWalkInProgressWaiter;

    ICorProfilerInfo4* const _pCorProfilerInfo;

private:
    static bool TrySetHandlerForSignal(int signal, struct sigaction& action);
    static char const* ErrorCodeToString(int errorCode);

    static void CollectStackSampleSignalHandler(int signal);

    static std::mutex s_stackWalkInProgressMutex;
    static LinuxStackFramesCollector* s_pInstanceCurrentlyStackWalking;
};
