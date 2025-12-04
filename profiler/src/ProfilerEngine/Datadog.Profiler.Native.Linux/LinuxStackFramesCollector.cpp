// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LinuxStackFramesCollector.h"

#include <algorithm>
#include <cassert>
#include <chrono>
#include <errno.h>
#include <iomanip>
#include <libunwind.h>
#include <mutex>
#include <ucontext.h>
#include <unistd.h>
#include <unordered_map>

#include "CallstackProvider.h"
#include "CorProfilerCallback.h"
#include "DiscardMetrics.h"
#include "IConfiguration.h"
#include "JitCodeCache.h"
#include "LibrariesInfoCache.h"
#include "Log.h"
#include "ManagedThreadInfo.h"
#include "OpSysTools.h"
#include "ProfilerSignalManager.h"
#include "ScopeFinalizer.h"
#include "StackSnapshotResultBuffer.h"

using namespace std::chrono_literals;

std::mutex LinuxStackFramesCollector::s_stackWalkInProgressMutex;
LinuxStackFramesCollector* LinuxStackFramesCollector::s_pInstanceCurrentlyStackWalking = nullptr;

void LinuxStackFramesCollector::HybridTraceBuffer::Reset(pid_t threadId, uintptr_t contextPointer)
{
    _threadId = threadId;
    _contextPointer = contextPointer;
    _initFlags = 0;
    _count.store(0, std::memory_order_relaxed);
    _overflow.store(false, std::memory_order_relaxed);
}

void LinuxStackFramesCollector::HybridTraceBuffer::SetInitFlags(std::uint32_t flags)
{
    _initFlags = flags;
}

void LinuxStackFramesCollector::HybridTraceBuffer::Append(HybridTraceEvent event, uintptr_t value, uintptr_t aux, std::int32_t result)
{
    auto index = _count.load(std::memory_order_relaxed);
    if (index >= static_cast<std::uint32_t>(MaxEntries))
    {
        _overflow.store(true, std::memory_order_relaxed);
        return;
    }

    _entries[static_cast<std::size_t>(index)] = HybridTraceEntry{event, value, aux, result};
    _count.store(static_cast<std::uint32_t>(index + 1U), std::memory_order_release);
}

std::uint32_t LinuxStackFramesCollector::HybridTraceBuffer::Count() const
{
    return _count.load(std::memory_order_acquire);
}

bool LinuxStackFramesCollector::HybridTraceBuffer::HasOverflow() const
{
    return _overflow.load(std::memory_order_acquire);
}

LinuxStackFramesCollector::HybridTraceEntry LinuxStackFramesCollector::HybridTraceBuffer::EntryAt(std::size_t index) const
{
    return _entries[index];
}

void LinuxStackFramesCollector::HybridTraceBuffer::ResetAfterFlush()
{
    _count.store(0, std::memory_order_relaxed);
    _overflow.store(false, std::memory_order_relaxed);
}

const char* LinuxStackFramesCollector::HybridTraceEventName(HybridTraceEvent event)
{
    switch (event)
    {
        case HybridTraceEvent::Start:
            return "Start";
        case HybridTraceEvent::AbortRequested:
            return "AbortRequested";
        case HybridTraceEvent::GetContextFailed:
            return "GetContextFailed";
        case HybridTraceEvent::InitFailed:
            return "InitFailed";
        case HybridTraceEvent::GetIpFailed:
            return "GetIpFailed";
        case HybridTraceEvent::AddFrameFailed:
            return "AddFrameFailed";
        case HybridTraceEvent::ManagedFrame:
            return "ManagedFrame";
        case HybridTraceEvent::NativeFrame:
            return "NativeFrame";
        case HybridTraceEvent::ManualStart:
            return "ManualStart";
        case HybridTraceEvent::ManualFramePointerUnavailable:
            return "ManualFramePointerUnavailable";
        case HybridTraceEvent::ManualFramePointerReadFailed:
            return "ManualFramePointerReadFailed";
        case HybridTraceEvent::ManualFramePointerInvalidReturn:
            return "ManualFramePointerInvalidReturn";
        case HybridTraceEvent::ManualFramePointerSuccess:
            return "ManualFramePointerSuccess";
        case HybridTraceEvent::ManualLinkRegisterSuccess:
            return "ManualLinkRegisterSuccess";
        case HybridTraceEvent::ManualFallback:
            return "ManualFallback";
        case HybridTraceEvent::StepResult:
            return "StepResult";
        case HybridTraceEvent::Finish:
            return "Finish";
        case HybridTraceEvent::ManagedViaJitCache:
            return "ManagedViaJitCache";
        case HybridTraceEvent::ManagedViaProcMaps:
            return "ManagedViaProcMaps";
        case HybridTraceEvent::ManagedDetectionMiss:
            return "ManagedDetectionMiss";
        case HybridTraceEvent::CacheMissing:
            return "CacheMissing";
    }

    return "Unknown";
}

void LinuxStackFramesCollector::RecordHybridEvent(HybridTraceEvent event, uintptr_t value, uintptr_t aux, std::int32_t result)
{
    if (!_useHybridUnwinding)
    {
        return;
    }

    _hybridTrace.Append(event, value, aux, result);
}

void LinuxStackFramesCollector::FlushHybridTrace(std::int32_t finalResult)
{
    if (!_useHybridUnwinding)
    {
        return;
    }

    auto count = _hybridTrace.Count();
    const auto overflow = _hybridTrace.HasOverflow();

    if (count == 0 && !overflow)
    {
        return;
    }

    Log::Debug(
        "HybridUnwindTrace: threadId=",
        _hybridTrace.GetThreadId(),
        ", ctx=0x",
        std::hex,
        _hybridTrace.GetContextPointer(),
        std::dec,
        ", initFlags=",
        _hybridTrace.GetInitFlags(),
        ", finalResult=",
        finalResult,
        ", entries=",
        count,
        overflow ? ", overflow=1" : "");

    const auto limit = std::min<std::uint32_t>(count, HybridTraceBuffer::MaxEntries);
    for (std::uint32_t i = 0; i < limit; i++)
    {
        const auto entry = _hybridTrace.EntryAt(i);
        Log::Debug(
            "HybridUnwindTrace: [",
            i,
            "] ",
            HybridTraceEventName(entry.Event),
            " value=0x",
            std::hex,
            entry.Value,
            ", aux=0x",
            entry.Aux,
            std::dec,
            ", result=",
            entry.Result);
    }

    if (overflow)
    {
        Log::Debug("HybridUnwindTrace: entries truncated at ", HybridTraceBuffer::MaxEntries);
    }

    _hybridTrace.ResetAfterFlush();
}

LinuxStackFramesCollector::LinuxStackFramesCollector(
    ProfilerSignalManager* signalManager,
    IConfiguration const* const configuration,
    CallstackProvider* callstackProvider,
    MetricsRegistry& metricsRegistry,
    JitCodeCache* jitCodeCache) :
    StackFramesCollectorBase(configuration, callstackProvider),
    _lastStackWalkErrorCode{0},
    _stackWalkFinished{false},
    _processId{OpSysTools::GetProcId()},
    _signalManager{signalManager},
    _errorStatistics{},
    _useBacktrace2{configuration->UseBacktrace2()},
    _useHybridUnwinding{configuration->UseHybridUnwinding()},
    _pJitCodeCache{jitCodeCache}
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

        on_leave
        {
            s_pInstanceCurrentlyStackWalking = nullptr;
        };

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

    if (_useHybridUnwinding)
    {
        FlushHybridTrace(static_cast<std::int32_t>(errorCode));
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

        if (_useHybridUnwinding)
        {
            return CollectStackHybrid(ctx);
        }
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

// Hybrid unwinding implementation
std::int32_t LinuxStackFramesCollector::CollectStackHybrid(void* ctx)
{
    std::int32_t resultErrorCode;

    const auto threadId = (_pCurrentCollectionThreadInfo != nullptr)
                              ? static_cast<pid_t>(_pCurrentCollectionThreadInfo->GetOsThreadId())
                              : 0;
    _hybridTrace.Reset(threadId, reinterpret_cast<uintptr_t>(ctx));

    // Initialize libunwind context as in CollectStackManually
    auto flag = UNW_INIT_SIGNAL_FRAME;
    unw_context_t context;
    if (ctx != nullptr)
    {
        context = *reinterpret_cast<unw_context_t*>(ctx);
    }
    else
    {
        // not in signal handler. Get the context and initialize the cursor from here
        resultErrorCode = unw_getcontext(&context);
        if (resultErrorCode != 0)
        {
            RecordHybridEvent(HybridTraceEvent::GetContextFailed, 0, 0, resultErrorCode);
            return E_ABORT; // unw_getcontext does not return a specific error code. Only -1
        }

        flag = static_cast<unw_init_local2_flags_t>(0);
    }

    _hybridTrace.SetInitFlags(static_cast<std::uint32_t>(flag));
    RecordHybridEvent(HybridTraceEvent::Start, reinterpret_cast<uintptr_t>(ctx), static_cast<uintptr_t>(flag), 0);

    unw_cursor_t cursor;
    resultErrorCode = unw_init_local2(&cursor, &context, flag);

    if (resultErrorCode < 0)
    {
        RecordHybridEvent(HybridTraceEvent::InitFailed, 0, 0, resultErrorCode);
        return resultErrorCode;
    }

    do
    {
        // After every lib call that touches non-local state, check if the StackSamplerLoopManager requested this walk to abort:
        if (IsCurrentCollectionAbortRequested())
        {
            AddFakeFrame();
            RecordHybridEvent(HybridTraceEvent::AbortRequested);
            return E_ABORT;
        }

        unw_word_t ip;
        resultErrorCode = unw_get_reg(&cursor, UNW_REG_IP, &ip);
        if (resultErrorCode != 0)
        {
            RecordHybridEvent(HybridTraceEvent::GetIpFailed, 0, 0, resultErrorCode);
            return resultErrorCode;
        }

        if (!AddFrame(ip))
        {
            RecordHybridEvent(HybridTraceEvent::AddFrameFailed, static_cast<uintptr_t>(ip), 0, S_FALSE);
            return S_FALSE;
        }

        // HYBRID LOGIC: Check if current frame is managed
        const auto isManaged = IsManagedCode(ip);
        RecordHybridEvent(isManaged ? HybridTraceEvent::ManagedFrame : HybridTraceEvent::NativeFrame, static_cast<uintptr_t>(ip));

        if (isManaged)
        {
            // Get current FP and SP to start manual walk
            unw_word_t fp_val, sp_val;
            #if defined(__aarch64__)
            int fp_res = unw_get_reg(&cursor, UNW_AARCH64_X29, &fp_val);
            #else
            int fp_res = unw_get_reg(&cursor, UNW_X86_64_RBP, &fp_val);
            #endif
            int sp_res = unw_get_reg(&cursor, UNW_REG_SP, &sp_val);
            
            if (fp_res == 0 && sp_res == 0)
            {
                // Walk the entire managed call chain without touching libunwind cursor
                WalkManagedStackChain(ip, fp_val, sp_val);
            }
            
            // After walking managed frames, we've hit native code or end of chain
            // Stop unwinding (return 0)
            resultErrorCode = 0;
        }
        else
        {
            // Use libunwind for native frames
            resultErrorCode = unw_step(&cursor);
        }

        RecordHybridEvent(HybridTraceEvent::StepResult, static_cast<uintptr_t>(ip), 0, resultErrorCode);

    } while (resultErrorCode > 0);

    RecordHybridEvent(HybridTraceEvent::Finish, 0, 0, resultErrorCode);

    return resultErrorCode;
}

bool LinuxStackFramesCollector::IsManagedCode(uintptr_t instructionPointer)
{
#ifdef LINUX
    if (const auto* methodInfo = _pJitCodeCache->FindMethod(instructionPointer))
    {
        RecordHybridEvent(
            HybridTraceEvent::ManagedViaJitCache,
            instructionPointer,
            methodInfo->Start);

        return true;
    }
#endif
    // Use the LibrariesInfoCache to detect if the instruction pointer falls within
    // .NET runtime or managed code regions
    auto* librariesCache = LibrariesInfoCache::GetInstance();
    if (librariesCache == nullptr)
    {
        // Fallback: assume native if no cache available
        RecordHybridEvent(HybridTraceEvent::CacheMissing, instructionPointer);
        return false;
    }

    // Check if the instruction pointer falls within any known mapping
    // This will return the cached result if found, or trigger missing mapping flag if not found
    const bool inManagedRegion = librariesCache->IsAddressInManagedRegion(instructionPointer);
    if (inManagedRegion)
    {
        RecordHybridEvent(HybridTraceEvent::ManagedViaProcMaps, instructionPointer);
    }
    else
    {
        RecordHybridEvent(HybridTraceEvent::ManagedDetectionMiss, instructionPointer);
    }

    return inManagedRegion;
}
std::int32_t LinuxStackFramesCollector::WalkManagedStackChain(uintptr_t initial_ip, uintptr_t initial_fp, uintptr_t initial_sp)
{
    // Pure manual FP-chain walk for managed code
    // This does NOT use libunwind cursor - we walk the stack ourselves
    
    uintptr_t current_ip = initial_ip;
    uintptr_t current_fp = initial_fp;
    uintptr_t current_sp = initial_sp;
    
    // 100 is reallty to short for managed frames.
    constexpr size_t MaxManagedFrames = 100;  // Safety limit
    constexpr size_t MaxFrameDistanceBytes = 1 << 20;
    
    for (size_t i = 0; i < MaxManagedFrames; i++)
    {
        // Check if still in managed code
        if (!IsManagedCode(current_ip))
        {
            // Hit native code - stop manual walk
            return 1;  // Success - walked all managed frames
        }
        
        // Try to get JIT metadata
        const auto* methodInfo = _pJitCodeCache->FindMethod(current_ip);
        const bool hasCachedOffsets = (methodInfo != nullptr) && 
                                       (methodInfo->SavedFpOffset >= 0) && 
                                       (methodInfo->SavedLrOffset >= 0);
        
        // Read saved FP and return address
        const int32_t fpOffset = hasCachedOffsets ? methodInfo->SavedFpOffset : 0;
        const int32_t lrOffset = hasCachedOffsets ? methodInfo->SavedLrOffset : static_cast<int32_t>(sizeof(uintptr_t));
        
        uintptr_t saved_fp = 0;
        uintptr_t return_addr = 0;
        
        const bool readOk = 
            ReadStackMemory(current_fp + fpOffset, &saved_fp, sizeof(saved_fp)) &&
            ReadStackMemory(current_fp + lrOffset, &return_addr, sizeof(return_addr));
        
        if (!readOk)
        {
            break;  // Can't read stack - stop
        }
        
        // Validate the read values
        const bool fp_valid = (saved_fp > current_fp) && (saved_fp - current_fp < MaxFrameDistanceBytes);
        const bool ret_valid = IsValidReturnAddress(return_addr);
        
        if (!fp_valid || !ret_valid)
        {
            break;  // Invalid frame - stop
        }
        
        // Compute new SP
        // Shouldn't we bail out if methodInfo is null or FrameSize is 0?
        const uint32_t frameSize = (methodInfo != nullptr && methodInfo->FrameSize > 0) 
                                    ? methodInfo->FrameSize 
                                    : 128;  // Default estimate
        const uintptr_t new_sp = current_fp + frameSize;
        
        // Add the caller frame
        if (!AddFrame(return_addr))
        {
            break;  // Can't add more frames
        }
        
        RecordHybridEvent(HybridTraceEvent::ManualFramePointerSuccess, return_addr, saved_fp);
        
        // Move to next frame
        current_ip = return_addr;
        current_fp = saved_fp;
        current_sp = new_sp;
    }
    
    return 1;  // Walked as far as we could
}

std::int32_t LinuxStackFramesCollector::UnwindManagedFrameManually(unw_cursor_t* cursor, uintptr_t ip, unw_context_t* original_context)
{
#ifdef ARM64
    RecordHybridEvent(HybridTraceEvent::ManualStart, ip);

    unw_word_t fp, sp, lr;
    int fp_result = unw_get_reg(cursor, UNW_AARCH64_X29, &fp);
    int sp_result = unw_get_reg(cursor, UNW_REG_SP, &sp);
    int lr_result = unw_get_reg(cursor, UNW_AARCH64_X30, &lr);

    const auto* methodInfo = _pJitCodeCache->FindMethod(ip);
    constexpr size_t DefaultMaxFrameDistanceBytes = 1ULL << 20;
    const size_t maxFrameDistanceBytes =
        (methodInfo != nullptr && methodInfo->FrameSize > 0)
            ? static_cast<size_t>(methodInfo->FrameSize) + 128
            : DefaultMaxFrameDistanceBytes;

    const int32_t cachedFpOffset =
        (methodInfo != nullptr && methodInfo->SavedFpOffset >= 0) ? methodInfo->SavedFpOffset : 0;
    const int32_t cachedLrOffset =
        (methodInfo != nullptr && methodInfo->SavedLrOffset >= 0) ? methodInfo->SavedLrOffset : static_cast<int32_t>(sizeof(uintptr_t));
    const bool hasCachedOffsets =
        (methodInfo != nullptr) && (methodInfo->SavedFpOffset >= 0) && (methodInfo->SavedLrOffset >= 0);

    // ----------------------------------------------------------
    // SAFETY: SP must *always* move UP (toward larger addresses)
    // Never decrease SP or go below the current one.
    // TODO: Once real stack boundaries are available, validate
    //       fp and prev_fp against them.
    // ----------------------------------------------------------

    if (fp_result == 0 && fp != 0)
    {
        unw_word_t prev_fp = 0;
        unw_word_t return_addr = 0;

        const uintptr_t prevFpAddress = static_cast<uintptr_t>(fp) + static_cast<uintptr_t>(hasCachedOffsets ? cachedFpOffset : 0);
        const uintptr_t returnAddressLocation =
            static_cast<uintptr_t>(fp) + static_cast<uintptr_t>(hasCachedOffsets ? cachedLrOffset : sizeof(prev_fp));

        const bool readPrev =
            ReadStackMemory(prevFpAddress, &prev_fp, sizeof(prev_fp)) &&
            ReadStackMemory(returnAddressLocation, &return_addr, sizeof(return_addr));

        if (readPrev)
        {
            // Relax validation to improve success rate
            // Allow FP to stay the same or move backward slightly (for leaf functions)
            const bool fp_chain_ok =
                (prev_fp >= fp) &&  // Allow equal (leaf functions)
                ((uintptr_t)prev_fp - (uintptr_t)fp < maxFrameDistanceBytes);

            const bool return_valid = IsValidReturnAddress(return_addr);

            uintptr_t new_sp = 0;
            if (methodInfo != nullptr && methodInfo->FrameSize > 0)
            {
                new_sp = static_cast<uintptr_t>(fp) + methodInfo->FrameSize;
            }
            else
            {
                new_sp = static_cast<uintptr_t>(fp) + 2 * sizeof(uintptr_t);
            }

            // Relax SP validation - allow SP to stay the same
            const bool sp_forward = new_sp >= sp;

            // Be more permissive - only require return address to be valid
            // FP chain and SP checks are hints, not hard requirements
            if (return_valid && (fp_chain_ok || sp_forward))
            {
                RecordHybridEvent(HybridTraceEvent::ManualFramePointerSuccess,
                                  (uintptr_t)return_addr, (uintptr_t)prev_fp);

                // PURE MANUAL UNWINDING APPROACH:
                // We CANNOT manipulate libunwind's cursor - any unw_set_reg() call
                // corrupts its internal state machine.
                //
                // Instead: Walk the entire managed call chain manually, adding frames
                // directly, then return 0 to stop unwinding when we hit native code.
                //
                // For now, just fall through to libunwind and let it fail gracefully
                // TODO: Implement complete manual stack walk
            }

            // Record which validation failed (signal-safe)
            if (!fp_chain_ok)
            {
                // Use aux field to encode validation failure: bit 0 = fp_chain_ok
                RecordHybridEvent(HybridTraceEvent::ManualFramePointerInvalidReturn,
                                  (uintptr_t)return_addr, (uintptr_t)prev_fp | 0x1);
            }
            else if (!return_valid)
            {
                // bit 1 = return_valid
                RecordHybridEvent(HybridTraceEvent::ManualFramePointerInvalidReturn,
                                  (uintptr_t)return_addr, (uintptr_t)prev_fp | 0x2);
            }
            else // !sp_forward
            {
                // bit 2 = sp_forward
                RecordHybridEvent(HybridTraceEvent::ManualFramePointerInvalidReturn,
                                  (uintptr_t)return_addr, (uintptr_t)prev_fp | 0x4);
            }
        }
        else
        {
            RecordHybridEvent(HybridTraceEvent::ManualFramePointerReadFailed,
                              (uintptr_t)fp);
        }
    }
    else
    {
        RecordHybridEvent(HybridTraceEvent::ManualFramePointerUnavailable,
                          (uintptr_t)fp, (uintptr_t)fp_result);
    }

    // ----------------------------------------------------------------------
    // FALLBACK: LR-based unwinding (leaf-ish or tail-call methods)
    //
    // *We DO NOT modify SP anymore.*
    //
    // Rationale: LR might be correct even when no FP exists, but we do not
    //            know the frame size. Moving SP heuristically is unsafe.
    //            Keeping SP stable ensures libunwind can recover correctly.
    // ----------------------------------------------------------------------
    if (lr_result == 0 && lr != 0 && IsValidReturnAddress(lr))
    {
        RecordHybridEvent(HybridTraceEvent::ManualLinkRegisterSuccess,
                          (uintptr_t)lr, (uintptr_t)sp);

        // Only update IP for LR-based unwinding
        // Keep SP and FP unchanged since we don't know the frame size
        unw_set_reg(cursor, UNW_REG_IP, lr);
        return 1;
    }

    // ----------------------------------------------------
    // Last fallback: allow libunwind to step normally
    // (will likely fail, but better than corrupting SP)
    // ----------------------------------------------------
    RecordHybridEvent(HybridTraceEvent::ManualFallback, (uintptr_t)ip);
    return unw_step(cursor);

#else
    // For x86_64, similar logic but with different registers
    RecordHybridEvent(HybridTraceEvent::ManualStart, ip);
    unw_word_t rbp, rsp;

    // Get current frame pointer (RBP), stack pointer (RSP)
    int rbp_result = unw_get_reg(cursor, UNW_X86_64_RBP, &rbp);
    int rsp_result = unw_get_reg(cursor, UNW_REG_SP, &rsp);

    if (rbp_result == 0 && rbp != 0)
    {
        constexpr size_t MaxFrameDistanceBytes = 1 << 20;

        unw_word_t prev_rbp = 0;
        unw_word_t return_addr = 0;

        // TODO: Guard this memory access using a signal-safe probe (see SafeAccess in Java profiler).
        const bool readPrev =
            ReadStackMemory(rbp, &prev_rbp, sizeof(prev_rbp)) &&
            ReadStackMemory(rbp + sizeof(prev_rbp), &return_addr, sizeof(return_addr));

        if (readPrev)
        {
            const bool stackOrderValid =
                (prev_rbp > rbp) && (static_cast<uintptr_t>(prev_rbp) - static_cast<uintptr_t>(rbp) < MaxFrameDistanceBytes);

            const bool returnValid = IsValidReturnAddress(return_addr);

            if (stackOrderValid && returnValid)
            {
                RecordHybridEvent(
                    HybridTraceEvent::ManualFramePointerSuccess,
                    static_cast<uintptr_t>(return_addr),
                    static_cast<uintptr_t>(prev_rbp));
                
                // UNW_REG_SP is read-only - we can only update IP and RBP
                unw_set_reg(cursor, UNW_REG_IP, return_addr);
                unw_set_reg(cursor, UNW_X86_64_RBP, prev_rbp);
                return 1;
            }

            RecordHybridEvent(
                HybridTraceEvent::ManualFramePointerInvalidReturn,
                static_cast<uintptr_t>(return_addr),
                static_cast<uintptr_t>(prev_rbp));
        }
        else
        {
            RecordHybridEvent(
                HybridTraceEvent::ManualFramePointerReadFailed,
                static_cast<uintptr_t>(rbp));
        }
    }
    else
    {
        RecordHybridEvent(
            HybridTraceEvent::ManualFramePointerUnavailable,
            static_cast<uintptr_t>(rbp),
            static_cast<uintptr_t>(rbp_result));
    }

    // Fallback to libunwind for x86_64
    RecordHybridEvent(HybridTraceEvent::ManualFallback, static_cast<uintptr_t>(ip));
    return unw_step(cursor);
#endif
}

bool LinuxStackFramesCollector::ReadStackMemory(uintptr_t address, void* buffer, size_t size)
{
    // Basic validation: check if address looks reasonable
    if (address == 0 || size == 0 || size > 1024) // Sanity check on size
    {
        return false;
    }

    // Check alignment - stack addresses should be reasonably aligned
    if ((address & 0x7) != 0) // 8-byte alignment check
    {
        return false;
    }

    // Simple bounds check: ensure we're in a reasonable stack range
    // This is heuristic - real stack bounds checking would require parsing /proc/self/maps
    // or using getrlimit(RLIMIT_STACK), but this gives basic protection
    void* current_stack_ptr;
#ifdef ARM64
    asm volatile("mov %0, sp"
                 : "=r"(current_stack_ptr)
                 :
                 : "memory");
#else
    asm volatile("mov %%rsp, %0"
                 : "=r"(current_stack_ptr)
                 :
                 : "memory");
#endif
    uintptr_t current_sp = reinterpret_cast<uintptr_t>(current_stack_ptr);

    // Stack typically grows downward, so valid addresses should be >= current SP
    // and within a reasonable distance (e.g., 8MB stack limit)
    if (address < current_sp || (address - current_sp) > (8 * 1024 * 1024))
    {
        return false;
    }

    // Direct memory access - we're in the same process and have done basic validation above
    // TODO: Implement proper bounds checking by:
    // 1. Parsing /proc/self/maps to get actual stack boundaries
    // 2. Using getrlimit(RLIMIT_STACK) for stack size limits
    // 3. Consider using signal handlers (SIGSEGV) for true crash protection
    // 4. Or use mincore() to check if pages are mapped before accessing
    memcpy(buffer, reinterpret_cast<void*>(address), size);
    return true;
}

bool LinuxStackFramesCollector::IsValidReturnAddress(uintptr_t address)
{
    // Check if address looks like a valid code address
    if (address == 0)
    {
        return false;
    }

    // Check reasonable alignment (ARM64 instructions are 4-byte aligned, x86_64 can be 1-byte)
#ifdef ARM64
    if ((address & 0x3) != 0)
    {
        return false;
    }
#endif

    // Basic sanity checks - address should be in a reasonable range
    // Typically code is above 0x10000 and below kernel space
    // ARM64 user space can go up to 0x0000ffffffffffff (48-bit addressing)
    // x86_64 user space typically up to 0x00007fffffffffff (47-bit addressing)
    #ifdef ARM64
    constexpr uintptr_t maxUserSpace = 0x0000ffffffffffffULL;
    #else
    constexpr uintptr_t maxUserSpace = 0x00007fffffffffffULL;
    #endif
    
    if (address < 0x10000 || address >= maxUserSpace)
    {
        return false;
    }

    // Check JIT cache first - most specific
    const auto* methodInfo = _pJitCodeCache->FindMethod(address);
    if (methodInfo != nullptr)
    {
        return true;
    }

    // Check if in any managed region (from /proc/self/maps or dl_iterate_phdr)
    auto* librariesCache = LibrariesInfoCache::GetInstance();
    if (librariesCache != nullptr && librariesCache->IsAddressInManagedRegion(address))
    {
        return true;
    }

    // Fallback heuristic for native return addresses
    return address >= 0x10000 && address < 0x7f0000000000ULL;
}

size_t LinuxStackFramesCollector::EstimateStackFrameSize(uintptr_t ip)
{
    // Heuristic to estimate managed frame size
    // JIT-compiled managed methods typically have modest stack frames
    // This is a conservative estimate that should work for most cases

#ifdef ARM64
    // ARM64 stack frames are typically 16-byte aligned
    return 64; // Conservative estimate: 4 saved registers * 8 bytes + padding
#else
    // x86_64 stack frames
    return 32; // Conservative estimate: 4 saved registers * 8 bytes
#endif
}

// Static method for CPU profiler to use hybrid unwinding without needing an instance
std::int32_t LinuxStackFramesCollector::CollectStackHybridStatic(void* ctx, uintptr_t* buffer, size_t bufferSize)
{
    // This is a simplified version that doesn't require a LinuxStackFramesCollector instance
    // It mimics the hybrid unwinding logic but writes directly to the provided buffer
    
    std::int32_t resultErrorCode;
    auto flag = UNW_INIT_SIGNAL_FRAME;
    unw_context_t context;
    
    if (ctx != nullptr)
    {
        context = *reinterpret_cast<unw_context_t*>(ctx);
    }
    else
    {
        resultErrorCode = unw_getcontext(&context);
        if (resultErrorCode != 0)
        {
            return 0; // Failed to get context
        }
        flag = static_cast<unw_init_local2_flags_t>(0);
    }
    
    unw_cursor_t cursor;
    resultErrorCode = unw_init_local2(&cursor, &context, flag);
    
    if (resultErrorCode < 0)
    {
        return 0;
    }
    
    size_t frameCount = 0;
    
    do
    {
        if (frameCount >= bufferSize)
        {
            break; // Buffer full
        }
        
        unw_word_t ip;
        if (unw_get_reg(&cursor, UNW_REG_IP, &ip) < 0)
        {
            break;
        }
        
        buffer[frameCount++] = static_cast<uintptr_t>(ip);
        
        // Check if this is managed code
        bool isManaged = false;
        
        #ifdef LINUX
        if (const auto* methodInfo = _pJitCodeCache->FindMethod(static_cast<uintptr_t>(ip)))
        {
            isManaged = true;
        }
        else
        #endif
        {
            auto* librariesCache = LibrariesInfoCache::GetInstance();
            if (librariesCache != nullptr)
            {
                isManaged = librariesCache->IsAddressInManagedRegion(static_cast<uintptr_t>(ip));
            }
        }
        
        if (isManaged)
        {
            // Try manual unwinding for managed frames
            #if defined(__aarch64__)
            unw_word_t fp, sp, lr;
            int fp_result = unw_get_reg(&cursor, UNW_AARCH64_X29, &fp);
            int sp_result = unw_get_reg(&cursor, UNW_REG_SP, &sp);
            
            if (fp_result == 0 && fp != 0)
            {
                // Walk the FP chain manually for managed frames
                uintptr_t current_fp = static_cast<uintptr_t>(fp);
                uintptr_t current_sp = static_cast<uintptr_t>(sp);
                constexpr size_t MaxManagedFrames = 50;
                constexpr size_t MaxFrameDistanceBytes = 1 << 20;
                
                auto* librariesCache = LibrariesInfoCache::GetInstance();
                
                for (size_t i = 0; i < MaxManagedFrames && frameCount < bufferSize; i++)
                {
                    const auto* methodInfo = _pJitCodeCache->FindMethod(static_cast<uintptr_t>(ip));
                    const int32_t cachedFpOffset = (methodInfo != nullptr && methodInfo->SavedFpOffset >= 0) ? methodInfo->SavedFpOffset : 0;
                    const int32_t cachedLrOffset = (methodInfo != nullptr && methodInfo->SavedLrOffset >= 0) ? methodInfo->SavedLrOffset : static_cast<int32_t>(sizeof(uintptr_t));
                    
                    unw_word_t prev_fp = 0;
                    unw_word_t return_addr = 0;
                    
                    // Read stack memory safely using memcpy (signal-safe)
                    std::memcpy(&prev_fp, reinterpret_cast<void*>(current_fp + cachedFpOffset), sizeof(prev_fp));
                    std::memcpy(&return_addr, reinterpret_cast<void*>(current_fp + cachedLrOffset), sizeof(return_addr));
                    
                    // Validate
                    if (prev_fp <= current_fp || prev_fp - current_fp >= MaxFrameDistanceBytes)
                    {
                        break;
                    }
                    
                    // Check if return address is still managed
                    bool stillManaged = false;
                    if (const auto* nextMethod = _pJitCodeCache->FindMethod(static_cast<uintptr_t>(return_addr)))
                    {
                        stillManaged = true;
                    }
                    else if (librariesCache != nullptr)
                    {
                        stillManaged = librariesCache->IsAddressInManagedRegion(static_cast<uintptr_t>(return_addr));
                    }
                    
                    if (!stillManaged)
                    {
                        break; // Hit native code
                    }
                    
                    buffer[frameCount++] = static_cast<uintptr_t>(return_addr);
                    current_fp = static_cast<uintptr_t>(prev_fp);
                    ip = return_addr;
                }
                
                // After manual walk, break out of libunwind loop
                break;
            }
            #endif
        }
        
        resultErrorCode = unw_step(&cursor);
    } while (resultErrorCode > 0);
    
    return static_cast<std::int32_t>(frameCount);
}