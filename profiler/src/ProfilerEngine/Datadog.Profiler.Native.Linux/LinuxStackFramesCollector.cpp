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

#include "IConfiguration.h"
#include "Log.h"
#include "ManagedThreadInfo.h"
#include "OpSysTools.h"
#include "ProfilerSignalManager.h"
#include "ScopeFinalizer.h"
#include "StackSnapshotResultBuffer.h"
#include "NativeLibraries.h"

using namespace std::chrono_literals;

std::mutex LinuxStackFramesCollector::s_stackWalkInProgressMutex;
LinuxStackFramesCollector* LinuxStackFramesCollector::s_pInstanceCurrentlyStackWalking = nullptr;

LinuxStackFramesCollector::LinuxStackFramesCollector(ProfilerSignalManager* signalManager, IConfiguration const* const configuration, UnwindTablesStore* unwindTablesStore) :
    StackFramesCollectorBase(configuration),
    _lastStackWalkErrorCode{0},
    _stackWalkFinished{false},
    _errorStatistics{},
    _processId{OpSysTools::GetProcId()},
    _signalManager{signalManager},
    _useBacktrace2{configuration->UseBacktrace2()},
    _unwindTablesStore{unwindTablesStore}
{
    _signalManager->RegisterHandler(LinuxStackFramesCollector::CollectStackSampleSignalHandler);
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

    if (selfCollect)
    {
        errorCode = CollectCallStackCurrentThread(nullptr);
    }
    else
    {
        if (!_signalManager->IsHandlerInPlace())
        {
            *pHR = E_FAIL;
            return GetStackSnapshotResult();
        }

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
                ;
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
        bool traceContextDataCollected = TryApplyTraceContextDataFromCurrentCollectionThreadToSnapshot();
        return CollectStackWithAsyncProfilerUnwinder(ctx);
        //return _useBacktrace2 ? CollectStackWithBacktrace2(ctx) : CollectStackManually(ctx);
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
    auto [data, size] = Data();
    auto count = unw_backtrace2((void**)data, size, context);

    if (count == 0)
    {
        return E_FAIL;
    }

    SetFrameCount(count);
    return S_OK;
}


class SafeAccess
{
public:
    NOINLINE __attribute__((aligned(16))) static void* load(void** ptr)
    {
        return *ptr;
    }

    static uintptr_t skipFaultInstruction(uintptr_t pc)
    {
        if ((pc - (uintptr_t)load) < 16)
        {
#if defined(__x86_64__)
            return *(u16*)pc == 0x8b48 ? 3 : 0; // mov rax, [reg]
#elif defined(__i386__)
            return *(u8*)pc == 0x8b ? 2 : 0; // mov eax, [reg]
#elif defined(__arm__) || defined(__thumb__)
            return (*(instruction_t*)pc & 0x0e50f000) == 0x04100000 ? 4 : 0; // ldr r0, [reg]
#elif defined(__aarch64__)
            return (*(instruction_t*)pc & 0xffc0001f) == 0xf9400000 ? 4 : 0; // ldr x0, [reg]
#else
            return sizeof(instruction_t);
#endif
        }
        return 0;
    }
};

const intptr_t MIN_VALID_PC = 0x1000;
const intptr_t MAX_WALK_SIZE = 0x100000;
const intptr_t MAX_FRAME_SIZE = 0x40000;

#define stripPointer(p) (p)

__attribute__((visibility("hidden"))) bool dwarfUnwind(FrameDesc* f, const void*& pc, uintptr_t& fp, uintptr_t& sp, uintptr_t prev_sp, uintptr_t bottom)
{
    u8 cfa_reg = (u8)f->cfa;
    int cfa_off = f->cfa >> 8;
    if (cfa_reg == DW_REG_SP)
    {
        sp = sp + cfa_off;
    }
    else if (cfa_reg == DW_REG_FP)
    {
        sp = fp + cfa_off;
    }
    else if (cfa_reg == DW_REG_PLT)
    {
        sp += ((uintptr_t)pc & 15) >= 11 ? cfa_off * 2 : cfa_off;
    }
    else
    {
        return false;
    }
    // Check if the next frame is below on the current stack
    if (sp < prev_sp || sp >= prev_sp + MAX_FRAME_SIZE || sp >= bottom)
    {
        return false;
    }
    // Stack pointer must be word aligned
    if ((sp & (sizeof(uintptr_t) - 1)) != 0)
    {
        return false;
    }
    if (f->fp_off & DW_PC_OFFSET)
    {
        pc = (const char*)pc + (f->fp_off >> 1);
    }
    else
    {
        if (f->fp_off != DW_SAME_FP && f->fp_off < MAX_FRAME_SIZE && f->fp_off > -MAX_FRAME_SIZE)
        {
            fp = (uintptr_t)SafeAccess::load((void**)(sp + f->fp_off));
        }
        pc = stripPointer(SafeAccess::load((void**)sp - 1));
    }
    if (pc < (const void*)MIN_VALID_PC || pc > (const void*)-MIN_VALID_PC)
    {
        return false;
    }

    return true;
}

inline std::tuple<const void*, uintptr_t, uintptr_t> ExtractExecutionInfos(void* ctx)
{
    if (ctx != nullptr)
    {
        StackFrame frame(ctx);
        return {(const void*)frame.pc(), frame.fp(), frame.sp()};
    }
    
    return {__builtin_return_address(0), (uintptr_t)__builtin_frame_address(1), (uintptr_t)__builtin_frame_address(0)};
}

std::uint16_t LinuxStackFramesCollector::stackWalkBro(ddprof::span<std::uintptr_t> callchain, void* ctx)
{
    auto [pc, fp, sp] = ExtractExecutionInfos(ctx);

    uintptr_t bottom = _pCurrentCollectionThreadInfo->GetStackBasedAddress();
    if (bottom == 0)
    {
        bottom = (uintptr_t)&sp + MAX_WALK_SIZE;
    }

    std::shared_ptr<UnwindTablesStore::UnwindTable> cc = nullptr;
    uintptr_t prev_sp;
    std::size_t depth = 0;

    while (depth < callchain.size())
    {
        callchain[depth++] = (uintptr_t)pc;
        prev_sp = sp;

        FrameDesc* f;

        if (cc != nullptr)
        {
            bool stopUnwinding = false;
            while (cc->contains(pc) && (f = cc->findFrameDesc(pc)) != nullptr )
            {
                assert(f != nullptr);
                if (!dwarfUnwind(f, pc, fp, sp, prev_sp, bottom))
                {
                    stopUnwinding = true;
                    break; // we should get out of the outer loop
                }
                callchain[depth++] = (uintptr_t)pc;
                prev_sp = sp;
            }
            if (stopUnwinding)
            {
                break;
            }
        }

        cc = _unwindTablesStore->FindByAddress(pc);

        if (cc == NULL || (f = cc->findFrameDesc(pc)) == nullptr)
        {
            //walkFp(pc, fp, sp); // how to break there is an issue
            // Check if the next frame is below on the current stack
            if (fp < sp || fp >= sp + MAX_FRAME_SIZE || fp >= bottom)
            {
                break;
            }

            // Frame pointer must be word aligned
            if ((fp & (sizeof(uintptr_t) - 1)) != 0)
            {
                break;
            }

            pc = stripPointer(SafeAccess::load((void**)fp + FRAME_PC_SLOT));
            if (pc < (const void*)MIN_VALID_PC || pc > (const void*)-MIN_VALID_PC)
            {
                break;
            }

            sp = fp + (FRAME_PC_SLOT + 1) * sizeof(void*);
            fp = *(uintptr_t*)fp;
            continue;
        }

        if (!dwarfUnwind(f, pc, fp, sp, prev_sp, bottom))
        {
            break;
        }
    }
    return depth;
}

std::int32_t LinuxStackFramesCollector::CollectStackWithAsyncProfilerUnwinder(void* ctx)
{
    auto [data, max_depth] = Data();

    auto callchain = ddprof::span<uintptr_t>(data, max_depth);
    auto count = stackWalkBro(callchain, ctx);

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