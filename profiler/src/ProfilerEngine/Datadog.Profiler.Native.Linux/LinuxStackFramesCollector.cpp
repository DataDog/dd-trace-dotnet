// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LinuxStackFramesCollector.h"

#include <cassert>
#include <chrono>
#include <errno.h>
#include <iomanip>
#include <libunwind.h>
#include <mutex>
#include <signal.h>
#include <sys/syscall.h>
#include <unordered_map>

#include "Log.h"
#include "ManagedThreadInfo.h"
#include "OpSysTools.h"
#include "ScopeFinalizer.h"
#include "StackSnapshotResultReusableBuffer.h"

using namespace std::chrono_literals;

std::mutex LinuxStackFramesCollector::s_stackWalkInProgressMutex;
int32_t LinuxStackFramesCollector::s_signalToSend = SIGUSR1;
LinuxStackFramesCollector* LinuxStackFramesCollector::s_pInstanceCurrentlyStackWalking = nullptr;
struct sigaction LinuxStackFramesCollector::s_previousAction;

LinuxStackFramesCollector::LinuxStackFramesCollector() :
    _lastStackWalkErrorCode{0},
    _stackWalkFinished{false},
    _errorStatistics{},
    _processId{OpSysTools::GetProcId()},
    _canReplaceSignalHandler{true}
{
    SetupSignalHandler();
}
LinuxStackFramesCollector::~LinuxStackFramesCollector()
{
    sigaction(s_signalToSend, &s_previousAction, nullptr);
    _errorStatistics.Log();
}

bool IsThreadAlive(::pid_t processId, ::pid_t threadId)
{
    return syscall(SYS_tgkill, processId, threadId, 0) == 0;
}

bool LinuxStackFramesCollector::ShouldLogStats()
{
    static std::time_t PreviousPrintTimestamp = 0;
    static const std::int64_t TimeIntervalInSeconds = 600; // print stats every 10min

    time_t currentTime;
    time(&currentTime);

    if (currentTime == static_cast<time_t>(-1))
    {
        return false;
    }

    if (currentTime - PreviousPrintTimestamp < TimeIntervalInSeconds)
    {
        return false;
    }

    PreviousPrintTimestamp = currentTime;

    return true;
}

void LinuxStackFramesCollector::UpdateErrorStats(std::int32_t errorCode)
{
    if (Log::IsDebugEnabled())
    {
        _errorStatistics.Add(errorCode);
        if (ShouldLogStats())
        {
            _errorStatistics.Log();
        }
    }
}

bool LinuxStackFramesCollector::IsProfilerSignalHandlerInstalled()
{
    struct sigaction currentAction;

    sigaction(s_signalToSend, nullptr, &currentAction);

    return (currentAction.sa_flags & SA_SIGINFO) == SA_SIGINFO &&
           currentAction.sa_sigaction == LinuxStackFramesCollector::CollectStackSampleSignalHandler;
}

std::int64_t LinuxStackFramesCollector::SendSignal(pid_t threadId) const
{
    return syscall(SYS_tgkill, _processId, threadId, s_signalToSend);
}

bool LinuxStackFramesCollector::CheckSignalHandler()
{
    if (IsProfilerSignalHandlerInstalled())
    {
        return true;
    }

    if (!_canReplaceSignalHandler)
    {
        static bool alreadyLogged = false;
        if (alreadyLogged)
            return false;

        alreadyLogged = true;
        Log::Warn("Profiler signal handler was replaced again. As of now, we will stopped restoring it to avoid issues: the profiler is disabled.");
        return false;
    }

    Log::Debug("Profiler signal handler handler has been replaced. Restoring it.");

    // restore profiler handler
    if (!SetupSignalHandler())
    {
        Log::Warn("Fail to restore profiler signal handler.");
        return false;
    }

    _canReplaceSignalHandler = false;
    return true;
}

StackSnapshotResultBuffer* LinuxStackFramesCollector::CollectStackSampleImplementation(ManagedThreadInfo* pThreadInfo,
                                                                                       uint32_t* pHR,
                                                                                       bool selfCollect)
{
    long errorCode;

    if (selfCollect)
    {
        errorCode = CollectCallStackCurrentThread(nullptr);
    }
    else
    {
        if (!CheckSignalHandler())
        {
            Log::Debug("Profiler signal handler was replaced but we failed or stopped at restoring it. We won't be able to collect callstacks.");
            *pHR = E_FAIL;
            return GetStackSnapshotResult();
        }

        std::unique_lock<std::mutex> stackWalkInProgressLock(s_stackWalkInProgressMutex);

        const auto threadId = static_cast<::pid_t>(pThreadInfo->GetOsThreadId());

#ifndef NDEBUG
        Log::Debug("LinuxStackFramesCollector::CollectStackSampleImplementation: Sending signal ",
                   s_signalToSend, " to thread with threadId=", threadId, ".");
#endif
        s_pInstanceCurrentlyStackWalking = this;

        on_leave { s_pInstanceCurrentlyStackWalking = nullptr; };

        _stackWalkFinished = false;

        errorCode = SendSignal(threadId);

        if (errorCode == -1)
        {
            Log::Warn("LinuxStackFramesCollector::CollectStackSampleImplementation:"
                      " Unable to send signal USR1 to thread with threadId=",
                      threadId, ". Error code: ", strerror(errno));
        }
        else
        {
            // release the lock and wait for a notification or the 2s timeout
            auto status = _stackWalkInProgressWaiter.wait_for(stackWalkInProgressLock, 2s);

            // The lock is reacquired, but we might have faced an issue:
            // - the thread is dead and the lock released
            // - the profiler signal handler was replaced

            if (status == std::cv_status::timeout)
            {
                _lastStackWalkErrorCode = E_ABORT;
            }

            errorCode = _lastStackWalkErrorCode;
        }
    }

    // errorCode domain values
    // * < 0 : libunwind error codes
    // * > 0 : other errors (ex: failed to create frame while walking the stack)
    // * == 0 : success
    if (errorCode < 0)
    {
        UpdateErrorStats(errorCode);
    }

    *pHR = (errorCode == 0) ? S_OK : E_FAIL;

    return GetStackSnapshotResult();
}

void LinuxStackFramesCollector::NotifyStackWalkCompleted(std::int32_t resultErrorCode)
{
    _lastStackWalkErrorCode = resultErrorCode;
    _stackWalkInProgressWaiter.notify_one();
    _stackWalkFinished = true;
}

bool LinuxStackFramesCollector::SetupSignalHandler()
{
    struct sigaction sampleAction;
    sampleAction.sa_flags = SA_RESTART | SA_SIGINFO;
    sampleAction.sa_sigaction = LinuxStackFramesCollector::CollectStackSampleSignalHandler;
    sigemptyset(&sampleAction.sa_mask);
    sigaddset(&sampleAction.sa_mask, s_signalToSend);

    int32_t result = sigaction(s_signalToSend, &sampleAction, &s_previousAction);
    if (result != 0)
    {
        Log::Error("LinuxStackFramesCollector::SetupSignalHandler: Failed to setup signal handler for SIGUSR1 signals. Reason: ",
                   strerror(errno), ".");
        return false;
    }

    Log::Info("LinuxStackFramesCollector::SetupSignalHandler: Successfully setup signal handler for SIGUSR1 signal.");
    return true;
}

char const* LinuxStackFramesCollector::ErrorCodeToString(int32_t errorCode)
{
    switch (errorCode)
    {
        case -UNW_ESUCCESS:
            return "success (UNW_ESUCCESS)";
        case -UNW_EUNSPEC:
            return "unspecified (general) error (UNW_EUNSPEC)";
        case -UNW_ENOMEM:
            return "out of memory (UNW_ENOMEM)";
        case -UNW_EBADREG:
            return "bad register number (UNW_EBADREG)";
        case -UNW_EREADONLYREG:
            return "attempt to write read-only register (UNW_EREADONLYREG)";
        case -UNW_ESTOPUNWIND:
            return "stop unwinding (UNW_ESTOPUNWIND)";
        case -UNW_EINVALIDIP:
            return "invalid IP (UNW_EINVALIDIP)";
        case -UNW_EBADFRAME:
            return "bad frame (UNW_EBADFRAME)";
        case -UNW_EINVAL:
            return "unsupported operation or bad value (UNW_EINVAL)";
        case -UNW_EBADVERSION:
            return "unwind info has unsupported version (UNW_EBADVERSION)";
        case -UNW_ENOINFO:
            return "no unwind info found (UNW_ENOINFO)";

        default:
            return "Unknown libunwind error code";
    }
}

std::int32_t LinuxStackFramesCollector::CollectCallStackCurrentThread(void* ctx)
{
    try
    {
        std::int32_t resultErrorCode;
        // Collect data for TraceContext tracking:
        bool traceContextDataCollected = TryApplyTraceContextDataFromCurrentCollectionThreadToSnapshot();

        // if we are in the signal handler, ctx won't be null, so we will use the context
        // This will allow us to skip the syscall frame and start from the frame before the syscall.
        unw_cursor_t cursor;
        if (ctx != nullptr)
        {
            resultErrorCode = unw_init_local2(&cursor, reinterpret_cast<unw_context_t*>(ctx), UNW_INIT_SIGNAL_FRAME);
        }
        else
        {
            // not in signal handler. Get the context and initialize the cursor form here
            unw_context_t context;
            resultErrorCode = unw_getcontext(&context);
            if (resultErrorCode != 0)
            {
                return E_ABORT; // unw_getcontext does not return a specific error code. Only -1
            }

            resultErrorCode = unw_init_local(&cursor, &context);
        }

        if (resultErrorCode < 0)
        {
            return resultErrorCode;
        }

        do
        {
            // After every lib call that touches non-local state, check if the StackSamplerLoopManager requested this walk to abort:
            if (IsCurrentCollectionAbortRequested())
            {
                AddFakeFrame();
                return E_ABORT;
            }

            unw_word_t ip;
            resultErrorCode = unw_get_reg(&cursor, UNW_REG_IP, &ip);
            if (resultErrorCode != 0)
            {
                return resultErrorCode;
            }

            if (!AddFrame(ip))
            {
                return S_FALSE;
            }

            resultErrorCode = unw_step(&cursor);
        } while (resultErrorCode > 0);

        return resultErrorCode;
    }
    catch (...)
    {
        return E_ABORT;
    }
}

bool LinuxStackFramesCollector::CanCollect(int32_t threadId, pid_t processId) const
{
    // on OSX, processId can be equal to 0. https://sourcegraph.com/github.com/dotnet/runtime/-/blob/src/coreclr/pal/src/exception/signal.cpp?L818:5&subtree=true
    // Since the profiler does not run on OSX, we leave it like this.
    auto* currentThreadInfo = _pCurrentCollectionThreadInfo;
    return currentThreadInfo != nullptr && currentThreadInfo->GetOsThreadId() == threadId && processId == _processId;
}

void LinuxStackFramesCollector::CallOrignalHandler(int32_t signal, siginfo_t* info, void* context)
{
    static thread_local bool isExecuting = false;

    if (isExecuting)
        return;

    isExecuting = true;

    try
    {
        if ((s_previousAction.sa_flags & SA_SIGINFO) == SA_SIGINFO && s_previousAction.sa_sigaction != nullptr)
        {
            assert(s_previousAction.sa_sigaction != nullptr);
            s_previousAction.sa_sigaction(signal, info, context);
        }
        else
        {
            if (s_previousAction.sa_handler != SIG_DFL && s_previousAction.sa_handler != SIG_IGN)
            {
                assert(s_previousAction.sa_handler != nullptr);
                s_previousAction.sa_handler(signal);
            }
        }
    }
    catch (...)
    {
    }

    isExecuting = false;
}

void LinuxStackFramesCollector::CollectStackSampleSignalHandler(int signal, siginfo_t* info, void* context)
{
    LinuxStackFramesCollector* pCollectorInstance = s_pInstanceCurrentlyStackWalking;

    if (pCollectorInstance != nullptr)
    {
        std::unique_lock<std::mutex> stackWalkInProgressLock(s_stackWalkInProgressMutex);

        pCollectorInstance = s_pInstanceCurrentlyStackWalking;

        // sampling in progress
        if (pCollectorInstance != nullptr)
        {
            // There can be a race:
            // The sampling thread has sent the signal and is waiting, but another SIGUSR1 signal was sent
            // by another thread and is handled before the one sent by the sampling thread.
            if (pCollectorInstance->CanCollect(OpSysTools::GetThreadId(), info->si_pid))
            {
                // In case it's the thread we want to sample, just get its callstack
                auto resultErrorCode = pCollectorInstance->CollectCallStackCurrentThread(reinterpret_cast<unw_context_t*>(context));

                // release the lock
                stackWalkInProgressLock.unlock();
                pCollectorInstance->NotifyStackWalkCompleted(resultErrorCode);
                return;
            }
        }
        // no need to release the lock and notify. The sampling thread must wait until its signal is handled correctly
    }

    // if we were in a race, we just call the other handler
    CallOrignalHandler(signal, info, context);
}

void LinuxStackFramesCollector::ErrorStatistics::Add(std::int32_t errorCode)
{
    auto& value = _stats[errorCode];
    value++;
}

void LinuxStackFramesCollector::ErrorStatistics::Log()
{
    if (!_stats.empty())
    {
        std::stringstream ss;
        ss << std::setfill(' ') << std::setw(13) << "# occurrences" << " | " << "Error message\n";
        for (auto& errorCodeAndStats : _stats)
        {
            ss << std::setfill(' ') << std::setw(10) << errorCodeAndStats.second << "  |  " << ErrorCodeToString(errorCodeAndStats.first) << " (" << errorCodeAndStats.first << ")\n";
        }

        Log::Info("LinuxStackFramesCollector::CollectStackSampleImplementation: The sampler thread encoutered errors in the interval\n",
                  ss.str());
        _stats.clear();
    }
}
