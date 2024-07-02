// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include "LibrariesInfoCache.h"
#include "StackFramesCollectorBase.h"

#include <atomic>
#include <condition_variable>
#include <memory>
#include <mutex>
#include <signal.h>
#include <unordered_map>

class IManagedThreadList;
class ProfilerSignalManager;
class ProfilerSignalManager;
class IConfiguration;
class CallstackProvider;
class LibrariesInfoCache;

class LinuxStackFramesCollector : public StackFramesCollectorBase
{
public:
    explicit LinuxStackFramesCollector(ProfilerSignalManager* signalManager, IConfiguration const* configuration, CallstackProvider* callstackProvider, LibrariesInfoCache* librariesCacheInfo);
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
                                                                uint32_t* pHR,
                                                                bool selfCollect) override;

private:
    class ErrorStatistics
    {
    public:
        void Add(std::int32_t errorCode);
        void Log();

    private:
        //                 v- error code v- # of errors
        std::unordered_map<std::int32_t, std::int32_t> _stats;
    };

private:
    void NotifyStackWalkCompleted(std::int32_t resultErrorCode);
    void UpdateErrorStats(std::int32_t errorCode);
    static bool ShouldLogStats();
    bool CanCollect(int32_t threadId, pid_t processId) const;
    std::int32_t CollectStackManually(void* ctx);
    std::int32_t CollectStackWithBacktrace2(void* ctx);
    void MarkAsInterrupted();

    std::int32_t _lastStackWalkErrorCode;
    std::condition_variable _stackWalkInProgressWaiter;
    // since we wait for a specific amount of time, if a call to notify_one
    // is done while we are not waiting, we will miss it and
    // we will block for ever.
    // This flag is used to prevent blocking on successfull (but long) stackwalking
    std::atomic<bool> _stackWalkFinished;
    pid_t _processId;
    ProfilerSignalManager* _signalManager;


private:
    static bool CollectStackSampleSignalHandler(int sig, siginfo_t* info, void* ucontext);

    static char const* ErrorCodeToString(int32_t errorCode);
    static std::mutex s_stackWalkInProgressMutex;

    static LinuxStackFramesCollector* s_pInstanceCurrentlyStackWalking;

    std::int32_t CollectCallStackCurrentThread(void* ucontext);

    ErrorStatistics _errorStatistics;
    bool _useBacktrace2;
    LibrariesInfoCache* _plibrariesInfo;
};