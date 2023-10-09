// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "StackFramesCollectorBase.h"

#include <atomic>
#include <cstdint>
#include <mutex>
#include <signal.h>

// forward declarations
class IConfiguration;
class ICorProfilerInfo4;
class IManagedThreadList;
class ManagedThreadInfo;
class ProfilerSignalManager;
class StackSnapshotResultBuffer;

class LinuxClrBasedStackFramesCollector : public StackFramesCollectorBase
{
public:
    explicit LinuxClrBasedStackFramesCollector(ICorProfilerInfo4* info, ProfilerSignalManager* signalManager);
    ~LinuxClrBasedStackFramesCollector() override;
    LinuxClrBasedStackFramesCollector(LinuxClrBasedStackFramesCollector const&) = delete;
    LinuxClrBasedStackFramesCollector& operator=(LinuxClrBasedStackFramesCollector const&) = delete;

protected:
    // Linux collector is different from Windows:
    // There is no notion to Suspend/Resume a thread and to have an external thread walk the suspended thread.
    // The thread that will call CollectStackSample(..), will send a signal to the target thread and then
    // wait until the target thread finished walking its callstack.
    // So, for ResumeThread and SuspendThread are No Ops for this collector, and we defer to the respective baseclass No-Op methods.

    StackSnapshotResultBuffer* CollectStackSampleImplementation(ManagedThreadInfo* pThreadInfo,
                                                                uint32_t* pHR,
                                                                bool selfCollect) override;

    std::int32_t CollectCallStackCurrentThread();

    bool CanCollect(int32_t threadId, pid_t processId) const;

    void NotifyStackWalkCompleted(std::int32_t resultErrorCode);

    static bool CollectStackSampleSignalHandler(int signal, siginfo_t* info, void* context);

private:
    friend HRESULT STDMETHODCALLTYPE LinuxStackSnapshotCallback(FunctionID funcId, UINT_PTR ip, COR_PRF_FRAME_INFO frameInfo, ULONG32 contextSize, BYTE context[], void* clientData);

    ICorProfilerInfo4* _info;
    ProfilerSignalManager* _signalManager;
    void MarkAsInterrupted();

    std::int32_t _lastStackWalkErrorCode;
    std::condition_variable _stackWalkInProgressWaiter;
    // since we wait for a specific amount of time, if a call to notify_one
    // is done while we are not waiting, we will miss it and
    // we will block for ever.
    // This flag is used to prevent blocking on successfull (but long) stackwalking
    std::atomic<bool> _stackWalkFinished;
    pid_t _processId;

    static std::mutex s_stackWalkInProgressMutex;

    static LinuxClrBasedStackFramesCollector* s_pInstanceCurrentlyStackWalking;
};
