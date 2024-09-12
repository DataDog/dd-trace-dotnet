// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LinuxStackFramesCollector.h"

#include <cassert>
#include <chrono>
#include <errno.h>
#include <iomanip>
#include <libunwind.h>
#include <mutex>
#include <ucontext.h>
#include <unordered_map>

#include "CallstackProvider.h"
#include "CpuProfilerDisableScope.h"
#include "IConfiguration.h"
#include "Log.h"
#include "ManagedThreadInfo.h"
#include "OpSysTools.h"
#include "ProfilerSignalManager.h"
#include "ScopeFinalizer.h"
#include "StackSnapshotResultBuffer.h"

using namespace std::chrono_literals;

std::mutex LinuxStackFramesCollector::s_stackWalkInProgressMutex;
LinuxStackFramesCollector* LinuxStackFramesCollector::s_pInstanceCurrentlyStackWalking = nullptr;

LinuxStackFramesCollector::LinuxStackFramesCollector(
    ProfilerSignalManager* signalManager,
    IConfiguration const* const configuration,
    CallstackProvider* callstackProvider,
    LibrariesInfoCache* librariesCacheInfo) :
    StackFramesCollectorBase(configuration, callstackProvider),
    _lastStackWalkErrorCode{0},
    _stackWalkFinished{false},
    _processId{OpSysTools::GetProcId()},
    _signalManager{signalManager},
    _errorStatistics{},
    _useBacktrace2{configuration->UseBacktrace2()},
    _plibrariesInfo{librariesCacheInfo}
{
    if (_signalManager != nullptr)
    {
        _signalManager->RegisterHandler(LinuxStackFramesCollector::CollectStackSampleSignalHandler);
    }
}

LinuxStackFramesCollector::~LinuxStackFramesCollector()
{
    _errorStatistics.Log();
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


StackSnapshotResultBuffer* LinuxStackFramesCollector::CollectStackSampleImplementation(ManagedThreadInfo* pThreadInfo,
                                                                                       uint32_t* pHR,
                                                                                       bool selfCollect)
{
    long errorCode;

    // If there a timer associated to the managed thread, we have to disarm it.
    // Otherwise, the CPU consumption to collect the callstack, will be accounted as "user app CPU time"
    auto timerId = pThreadInfo->GetTimerId();

    if (selfCollect)
    {
        // In case we are self-unwinding, we do not want to be interrupted by the signal-based profilers (walltime and cpu)
        // This will crashing in libunwind (accessing a memory area  which was unmapped)
        // This lock is acquired by the signal-based profiler (see StackSamplerLoop->StackSamplerLoopManager)
        pThreadInfo->GetStackWalkLock().Acquire();

        _plibrariesInfo->UpdateCache();

        on_leave
        {
            pThreadInfo->GetStackWalkLock().Release();
        };

        errorCode = CollectCallStackCurrentThread(nullptr);
    }
    else
    {
        if (_signalManager == nullptr || !_signalManager->IsHandlerInPlace())
        {
            *pHR = E_FAIL;
            return GetStackSnapshotResult();
        }

        // Disable timer_create-based CPU profiler if needed
        // When scope goes out of scope, the CPU profiler will be reenabled for
        // pThreadInfo thread
        auto scope = CpuProfilerDisableScope(pThreadInfo);

        _plibrariesInfo->UpdateCache();

        std::unique_lock<std::mutex> stackWalkInProgressLock(s_stackWalkInProgressMutex);

        const auto threadId = static_cast<::pid_t>(pThreadInfo->GetOsThreadId());

        s_pInstanceCurrentlyStackWalking = this;

        on_leave { s_pInstanceCurrentlyStackWalking = nullptr; };

        _stackWalkFinished = false;

        errorCode = _signalManager->SendSignal(threadId);

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
                
                if (!_signalManager->CheckSignalHandler())
                {
                    _lastStackWalkErrorCode = E_FAIL;
                    Log::Info("Profiler signal handler was replaced but we failed or stopped at restoring it. We won't be able to collect callstacks.");
                    *pHR = E_FAIL;
                    return GetStackSnapshotResult();
                }
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
    _stackWalkFinished = true;
    _stackWalkInProgressWaiter.notify_one();
}

// This symbol is defined in the Datadog.Linux.ApiWrapper. It allows us to check if the thread to be profiled
// contains a frame of a function that might cause a deadlock.
extern "C" unsigned long long dd_inside_wrapped_functions() __attribute__((weak));

std::int32_t LinuxStackFramesCollector::CollectCallStackCurrentThread(void* ctx)
{
    if (dd_inside_wrapped_functions != nullptr && dd_inside_wrapped_functions() != 0)
    {
        return E_ABORT;
    }

    try
    {
        // Collect data for TraceContext tracking:
        TryApplyTraceContextDataFromCurrentCollectionThreadToSnapshot();

        return _useBacktrace2 ? CollectStackWithBacktrace2(ctx) : CollectStackManually(ctx);
    }
    catch (...)
    {
        return E_ABORT;
    }
}

std::int32_t LinuxStackFramesCollector::CollectStackManually(void* ctx)
{
    std::int32_t resultErrorCode;

    // if we are in the signal handler, ctx won't be null, so we will use the context
    // This will allow us to skip the syscall frame and start from the frame before the syscall.
    auto flag = UNW_INIT_SIGNAL_FRAME;
    unw_context_t context;
    if (ctx != nullptr)
    {
        context = *reinterpret_cast<unw_context_t*>(ctx);
    }
    else
    {
        // not in signal handler. Get the context and initialize the cursor form here
        resultErrorCode = unw_getcontext(&context);
        if (resultErrorCode != 0)
        {
            return E_ABORT; // unw_getcontext does not return a specific error code. Only -1
        }

        flag = static_cast<unw_init_local2_flags_t>(0);
    }

    unw_cursor_t cursor;
    resultErrorCode = unw_init_local2(&cursor, &context, flag);

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

std::int32_t LinuxStackFramesCollector::CollectStackWithBacktrace2(void* ctx)
{
    auto* context = reinterpret_cast<unw_context_t*>(ctx);

    // Now walk the stack:
    auto buffer = Data();
    auto count = unw_backtrace2((void**)buffer.data(), buffer.size(), context, UNW_INIT_SIGNAL_FRAME);

    if (count == 0)
    {
        return E_FAIL;
    }

    SetFrameCount(count);

    return S_OK;
}

bool LinuxStackFramesCollector::CanCollect(int32_t threadId, pid_t processId) const
{
    // on OSX, processId can be equal to 0. https://sourcegraph.com/github.com/dotnet/runtime/-/blob/src/coreclr/pal/src/exception/signal.cpp?L818:5&subtree=true
    // Since the profiler does not run on OSX, we leave it like this.
    auto* currentThreadInfo = _pCurrentCollectionThreadInfo;
    return currentThreadInfo != nullptr && currentThreadInfo->GetOsThreadId() == threadId && processId == _processId;
}

void LinuxStackFramesCollector::MarkAsInterrupted()
{
    auto* currentThreadInfo = _pCurrentCollectionThreadInfo;

    if (currentThreadInfo != nullptr)
    {
        currentThreadInfo->MarkAsInterrupted();
    }
}

bool IsInSigSegvHandler(void* context)
{
    auto* ctx = reinterpret_cast<ucontext_t*>(context);

    // If SIGSEGV is part of the sigmask set, it means that the thread was executing
    // the SIGSEGV signal handler (or someone blocks SIGSEGV signal for this thread,
    // but that less likely)
    return sigismember(&(ctx->uc_sigmask), SIGSEGV) == 1;
}

bool LinuxStackFramesCollector::CollectStackSampleSignalHandler(int signal, siginfo_t* info, void* context)
{
    // This is a workaround to prevent libunwind from unwind 2 signal frames and potentially crashing.
    // Current crash occurs in libcoreclr.so, while reading the Elf header.
    if (IsInSigSegvHandler(context))
    {
        return false;
    }

    // Libunwind can overwrite the value of errno - save it beforehand and restore it at the end
    auto oldErrno = errno;

    bool success = false;

    LinuxStackFramesCollector* pCollectorInstance = s_pInstanceCurrentlyStackWalking;

    if (pCollectorInstance != nullptr)
    {
        std::unique_lock<std::mutex> stackWalkInProgressLock(s_stackWalkInProgressMutex);

        pCollectorInstance = s_pInstanceCurrentlyStackWalking;

        // sampling in progress
        if (pCollectorInstance != nullptr)
        {
            pCollectorInstance->MarkAsInterrupted();

            // There can be a race:
            // The sampling thread has sent the signal and is waiting, but another SIGUSR1 signal was sent
            // by another thread and is handled before the one sent by the sampling thread.
            if (pCollectorInstance->CanCollect(OpSysTools::GetThreadId(), info->si_pid))
            {
                // In case it's the thread we want to sample, just get its callstack
                auto resultErrorCode = pCollectorInstance->CollectCallStackCurrentThread(context);

                // release the lock
                stackWalkInProgressLock.unlock();
                pCollectorInstance->NotifyStackWalkCompleted(resultErrorCode);
                success = true;
            }
        }
        // no need to release the lock and notify. The sampling thread must wait until its signal is handled correctly
    }

    errno = oldErrno;
    return success;
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
        ss << std::setfill(' ') << std::setw(13) << "# occurrences"
           << " | "
           << "Error message\n";
        for (auto& errorCodeAndStats : _stats)
        {
            ss << std::setfill(' ') << std::setw(10) << errorCodeAndStats.second << "  |  " << unw_strerror(errorCodeAndStats.first) << " (" << errorCodeAndStats.first << ")\n";
        }

        Log::Info("LinuxStackFramesCollector::CollectStackSampleImplementation: The sampler thread encoutered errors in the interval\n",
                  ss.str());
        _stats.clear();
    }
}
