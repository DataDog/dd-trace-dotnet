// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LinuxStackFramesCollector.h"

#include <cassert>
#include <chrono>
#include <errno.h>
#include <iomanip>
// No need to add UNW_LOCAL_ONLY here, we do not call unw_backtraceXX here.
#include <libunwind.h>
#include <mutex>
#include <ucontext.h>
#include <unordered_map>

#include "CallstackProvider.h"
#include "DiscardMetrics.h"
#include "IConfiguration.h"
#include "IUnwinder.h"
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
    MetricsRegistry& metricsRegistry,
    IUnwinder* pUnwinder) :
    StackFramesCollectorBase(configuration, callstackProvider),
    _lastStackWalkErrorCode{0},
    _stackWalkFinished{false},
    _processId{OpSysTools::GetProcId()},
    _signalManager{signalManager},
    _errorStatistics{},
    _pUnwinder{pUnwinder}
{
    if (_signalManager != nullptr)
    {
        _signalManager->RegisterHandler(LinuxStackFramesCollector::CollectStackSampleSignalHandler);
    }

    // For now have one metric for both walltime and cpu (naive)
    _samplingRequest = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_walltime_cpu_sampling_requests");
    _discardMetrics = metricsRegistry.GetOrRegister<DiscardMetrics>("dotnet_walltime_cpu_sample_discarded");

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
        pThreadInfo->AcquireLock();

        on_leave
        {
            pThreadInfo->ReleaseLock();
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

        std::unique_lock<std::mutex> stackWalkInProgressLock(s_stackWalkInProgressMutex);

        const auto threadId = static_cast<::pid_t>(pThreadInfo->GetOsThreadId());

        s_pInstanceCurrentlyStackWalking = this;

        on_leave { s_pInstanceCurrentlyStackWalking = nullptr; };

        _stackWalkFinished = false;

        _samplingRequest->Incr();
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
        _discardMetrics->Incr<DiscardReason::InsideWrappedFunction>();
        return E_ABORT;
    }

    try
    {
        // Collect data for TraceContext tracking:
        TryApplyTraceContextDataFromCurrentCollectionThreadToSnapshot();

        return CollectStack(ctx);
    }
    catch (...)
    {
        return E_ABORT;
    }
}

inline std::int32_t LinuxStackFramesCollector::CollectStack(void* ctx)
{
    auto buffer = Data();
    auto count = _pUnwinder->Unwind(ctx, reinterpret_cast<std::uintptr_t*>(buffer.data()), buffer.size());

    if (count == 0)
    {
        _discardMetrics->Incr<DiscardReason::EmptyBacktrace>();
        return E_FAIL;
    }

    SetFrameCount(count);

    return S_OK;
}


bool IsInSigSegvHandler(void* context)
{
    auto* ctx = reinterpret_cast<ucontext_t*>(context);

    // If SIGSEGV is part of the sigmask set, it means that the thread was executing
    // the SIGSEGV signal handler (or someone blocks SIGSEGV signal for this thread,
    // but that less likely)
    return sigismember(&(ctx->uc_sigmask), SIGSEGV) == 1;
}

bool LinuxStackFramesCollector::CanCollect(int32_t threadId, siginfo_t* info, void* context) const
{
    // This is a workaround to prevent libunwind from unwinding 2 signal frames and potentially crashing.
    // Current crash occurs in libcoreclr.so, while reading the Elf header.
    if (IsInSigSegvHandler(context))
    {
        _discardMetrics->Incr<DiscardReason::InSegvHandler>();
        return false;
    }

    auto* currentThreadInfo = _pCurrentCollectionThreadInfo;
    if (currentThreadInfo == nullptr)
    {
        _discardMetrics->Incr<DiscardReason::UnknownThread>();
        return false;
    }

    if (currentThreadInfo->GetOsThreadId() != threadId)
    {
        _discardMetrics->Incr<DiscardReason::WrongManagedThread>();
        return false;
    }

    // on OSX, processId can be equal to 0. https://sourcegraph.com/github.com/dotnet/runtime/-/blob/src/coreclr/pal/src/exception/signal.cpp?L818:5&subtree=true
    // Since the profiler does not run on OSX, we leave it like this.
    if (info->si_pid != _processId)
    {
        _discardMetrics->Incr<DiscardReason::ExternalSignal>();
        return false;
    }

    return true;
}

void LinuxStackFramesCollector::MarkAsInterrupted()
{
    auto* currentThreadInfo = _pCurrentCollectionThreadInfo;

    if (currentThreadInfo != nullptr)
    {
        currentThreadInfo->MarkAsInterrupted();
    }
}

bool LinuxStackFramesCollector::CollectStackSampleSignalHandler(int signal, siginfo_t* info, void* context)
{
    // Libunwind can overwrite the value of errno - save it beforehand and restore it at the end
    auto oldErrno = errno;

    bool success = false;

    LinuxStackFramesCollector* pCollector = s_pInstanceCurrentlyStackWalking;

    if (pCollector != nullptr)
    {
        std::unique_lock<std::mutex> lock(s_stackWalkInProgressMutex);

        pCollector = s_pInstanceCurrentlyStackWalking;

        // sampling in progress
        if (pCollector != nullptr)
        {
            pCollector->MarkAsInterrupted();

            // There can be a race:
            // The sampling thread has sent the signal and is waiting, but another SIGUSR1 signal was sent
            // by another thread and is handled before the one sent by the sampling thread.
            if (pCollector->CanCollect(OpSysTools::GetThreadId(), info, context))
            {

                // In case it's the thread we want to sample, just get its callstack
                auto errorCode = pCollector->CollectCallStackCurrentThread(context);

                // release the lock
                lock.unlock();
                pCollector->NotifyStackWalkCompleted(errorCode);
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