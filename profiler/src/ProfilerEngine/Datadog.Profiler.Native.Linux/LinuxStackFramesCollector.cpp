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
#include <unistd.h>
#include <unordered_map>

#include "CallstackProvider.h"
#include "CorProfilerCallback.h"
#include "DiscardMetrics.h"
#include "IConfiguration.h"
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

LinuxStackFramesCollector::LinuxStackFramesCollector(
    ProfilerSignalManager* signalManager,
    IConfiguration const* const configuration,
    CallstackProvider* callstackProvider,
    MetricsRegistry& metricsRegistry) :
    StackFramesCollectorBase(configuration, callstackProvider),
    _lastStackWalkErrorCode{0},
    _stackWalkFinished{false},
    _processId{OpSysTools::GetProcId()},
    _signalManager{signalManager},
    _errorStatistics{},
    _useBacktrace2{configuration->UseBacktrace2()},
    _useHybridUnwinding{configuration->UseHybridUnwinding()}
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

        // HYBRID LOGIC: Check if current frame is managed
        if (IsManagedCode(ip))
        {
            // Use manual unwinding for managed frames
            resultErrorCode = UnwindManagedFrameManually(&cursor, ip);
        }
        else
        {
            // Use libunwind for native frames
            resultErrorCode = unw_step(&cursor);
        }

    } while (resultErrorCode > 0);

    return resultErrorCode;
}

bool LinuxStackFramesCollector::IsManagedCode(uintptr_t instructionPointer)
{
    // Use the LibrariesInfoCache to detect if the instruction pointer falls within
    // .NET runtime or managed code regions
    auto* librariesCache = LibrariesInfoCache::GetInstance();
    if (librariesCache == nullptr)
    {
        // Fallback: assume native if no cache available
        return false;
    }

    // Check if the instruction pointer falls within any known mapping
    // This will return the cached result if found, or trigger missing mapping flag if not found
    return librariesCache->IsAddressInManagedRegion(instructionPointer);
}

std::int32_t LinuxStackFramesCollector::UnwindManagedFrameManually(unw_cursor_t* cursor, uintptr_t ip)
{
#ifdef ARM64
    // ARM64-specific manual unwinding for managed frames
    unw_word_t fp, sp, lr;

    // Get current frame pointer (x29), stack pointer, link register (x30)
    int fp_result = unw_get_reg(cursor, UNW_AARCH64_X29, &fp);
    int sp_result = unw_get_reg(cursor, UNW_REG_SP, &sp);
    int lr_result = unw_get_reg(cursor, UNW_AARCH64_X30, &lr);

    if (fp_result == 0 && fp != 0)
    {
        // Frame pointer available - use it
        // ARM64 frame layout: [previous_fp][return_address]
        unw_word_t prev_fp = 0;
        unw_word_t return_addr = 0;

        // Read previous frame pointer and return address from stack
        if (ReadStackMemory(fp, &prev_fp, sizeof(prev_fp)) &&
            ReadStackMemory(fp + 8, &return_addr, sizeof(return_addr)))
        {
            // Validate the return address looks reasonable
            if (IsValidReturnAddress(return_addr))
            {
                // Update cursor registers for next frame
                unw_set_reg(cursor, UNW_AARCH64_X29, prev_fp);
                unw_set_reg(cursor, UNW_REG_SP, fp + 16); // Skip saved fp + lr
                unw_set_reg(cursor, UNW_REG_IP, return_addr);

                return 1; // Success, frame unwound
            }
        }
    }

    // Fallback: try to use link register if it looks valid
    if (lr_result == 0 && lr != 0 && IsValidReturnAddress(lr))
    {
        unw_set_reg(cursor, UNW_REG_IP, lr);
        // Estimate stack adjustment (this is heuristic)
        unw_set_reg(cursor, UNW_REG_SP, sp + EstimateStackFrameSize(ip));
        return 1;
    }

    // Last resort: fall back to libunwind
    return unw_step(cursor);

#else
    // For x86_64, similar logic but with different registers
    unw_word_t rbp, rsp;

    // Get current frame pointer (RBP), stack pointer (RSP)
    int rbp_result = unw_get_reg(cursor, UNW_X86_64_RBP, &rbp);
    int rsp_result = unw_get_reg(cursor, UNW_REG_SP, &rsp);

    if (rbp_result == 0 && rbp != 0)
    {
        // Frame pointer available - use it
        // x86_64 frame layout: [previous_rbp][return_address]
        unw_word_t prev_rbp = 0;
        unw_word_t return_addr = 0;

        // Read previous frame pointer and return address from stack
        if (ReadStackMemory(rbp, &prev_rbp, sizeof(prev_rbp)) &&
            ReadStackMemory(rbp + 8, &return_addr, sizeof(return_addr)))
        {
            // Validate the return address looks reasonable
            if (IsValidReturnAddress(return_addr))
            {
                // Update cursor registers for next frame
                unw_set_reg(cursor, UNW_X86_64_RBP, prev_rbp);
                unw_set_reg(cursor, UNW_REG_SP, rbp + 16); // Skip saved rbp + return addr
                unw_set_reg(cursor, UNW_REG_IP, return_addr);

                return 1; // Success, frame unwound
            }
        }
    }

    // Fallback to libunwind for x86_64
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
    if (address < 0x10000 || address >= 0x7f0000000000ULL)
    {
        return false;
    }

    // Additional validation: try to read a few bytes to see if it's accessible
    uint32_t instruction;
    if (!ReadStackMemory(address, &instruction, sizeof(instruction)))
    {
        return false;
    }

    return true;
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