// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LinuxStackFramesCollector.h"

#include "profiler/src/ProfilerEngine/Datadog.Profiler.Native/OpSysTools.h"

#include <cassert>
#include <chrono>
#include <errno.h>
#include <mutex>
#include <signal.h>
#include <sys/syscall.h>
#include <unordered_map>
#include <iomanip>
#include <iostream>
#include "ucontext.h"

#ifdef ARM64
#include <libunwind-aarch64.h>
#elif AMD64
#include <libunwind-x86_64.h>
#else
error("unsupported architecture")
#endif

#include "Log.h"
#include "ManagedThreadInfo.h"
#include "OpSysTools.h"
#include "ScopeFinalizer.h"
#include "StackSnapshotResultReusableBuffer.h"

using namespace std::chrono_literals;

std::mutex LinuxStackFramesCollector::s_signalHandlerInitLock;
std::mutex LinuxStackFramesCollector::s_stackWalkInProgressMutex;
bool LinuxStackFramesCollector::s_isSignalHandlerSetup = false;
int32_t LinuxStackFramesCollector::s_signalToSend = -1;
LinuxStackFramesCollector* LinuxStackFramesCollector::s_pInstanceCurrentlyStackWalking = nullptr;

LinuxStackFramesCollector::LinuxStackFramesCollector(ICorProfilerInfo4* const _pCorProfilerInfo, DacService* dac) :
    _pCorProfilerInfo(_pCorProfilerInfo),
    _lastStackWalkErrorCode{0},
    _stackWalkFinished{false},
    _errorStatistics{},
    _dac{dac}
{
    _pCorProfilerInfo->AddRef();
    InitializeSignalHandler();
}
LinuxStackFramesCollector::~LinuxStackFramesCollector()
{
    _pCorProfilerInfo->Release();
    _errorStatistics.Log();
    // !! @ToDo: We must uninstall the signal handler!!
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
        if (!s_isSignalHandlerSetup)
        {
            Log::Debug("LinuxStackFramesCollector::CollectStackSampleImplementation: Signal handler not set up. Cannot collect callstacks."
                       " (Earlier log entry may contain additinal details.)");

            *pHR = E_FAIL;

            return GetStackSnapshotResult();
        }

        std::unique_lock<std::mutex> stackWalkInProgressLock(s_stackWalkInProgressMutex);

        const auto osThreadId = static_cast<::pid_t>(pThreadInfo->GetOsThreadId());
        const auto processId = static_cast<::pid_t>(OpSysTools::GetProcId());

#ifndef NDEBUG
        Log::Debug("LinuxStackFramesCollector::CollectStackSampleImplementation: Sending signal ",
                   s_signalToSend, " to thread with osThreadId=", osThreadId, ".");
#endif
        s_pInstanceCurrentlyStackWalking = this;

        on_leave { s_pInstanceCurrentlyStackWalking = nullptr; };

        _stackWalkFinished = false;

        errorCode = syscall(SYS_tgkill, processId, osThreadId, s_signalToSend);

        if (errorCode == -1)
        {
            Log::Warn("LinuxStackFramesCollector::CollectStackSampleImplementation:"
                      " Unable to send signal USR1 to thread with osThreadId=",
                      osThreadId, ". Error code: ",
                      strerror(errno));
        }
        else
        {
            do
            {
                // When the application ends and the CLR shuts down, it might happen that
                // the currently walked thread gets terminated without noticing us.
                // This loop ensures that the code does not stay stuck waiting for a lock that will
                // never be released. It will exit if the stack walked thread does not run anymore or
                // the stack walk finishes as expected.
                _stackWalkInProgressWaiter.wait_for(stackWalkInProgressLock, 500ms);
                if (!IsThreadAlive(processId, osThreadId))
                {
                    _lastStackWalkErrorCode = E_ABORT;
                    break;
                }
            } while (!_stackWalkFinished);
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

void LinuxStackFramesCollector::InitializeSignalHandler()
{
    if (s_isSignalHandlerSetup)
        return;

    std::unique_lock<std::mutex> lock{s_signalHandlerInitLock};

    if (s_isSignalHandlerSetup)
        return;

    s_isSignalHandlerSetup = SetupSignalHandler();
}

bool LinuxStackFramesCollector::TrySetHandlerForSignal(int32_t signal, struct sigaction& action)
{
    struct sigaction oldAction;
    if (sigaction(signal, nullptr, &oldAction) < 0)
    {
        Log::Error("LinuxStackFramesCollector::TrySetHandlerForSignal:"
                   " Unable to examine signal handler for signal ",
                   signal, ". Reason:",
                   strerror(errno), ".");
        return false;
    }

    // Replace signal only if there is no user-defined one.
    // @ToDo: is that enough?
    if (oldAction.sa_handler == SIG_DFL || oldAction.sa_handler == SIG_IGN)
    {
        sigaddset(&action.sa_mask, signal);
        int32_t result = sigaction(signal, &action, &oldAction);
        if (result == 0)
        {
            return true;
        }

        sigdelset(&action.sa_mask, signal);
        Log::Error("LinuxStackFramesCollector::TrySetHandlerForSignal:"
                   " Unable to setup signal handler for signal",
                   signal, ". Reason: ",
                   strerror(errno), ".");
    }

    Log::Info("LinuxStackFramesCollector::TrySetHandlerForSignal:"
              " Unable to set signal for ",
              signal, ". The default one is overriden by ",
              oldAction.sa_handler, ".");

    return false;
}

bool LinuxStackFramesCollector::SetupSignalHandler()
{
    // SIGUSR1 & SIGUSR2 are not use in the CLR
    // But, let's check if they are available

    struct sigaction sampleAction;
    sampleAction.sa_flags = SA_RESTART | SA_SIGINFO;
    sampleAction.sa_handler = SIG_DFL;
    sampleAction.sa_sigaction = LinuxStackFramesCollector::CollectStackSampleSignalHandler;
    sigemptyset(&sampleAction.sa_mask);

    if (TrySetHandlerForSignal(SIGUSR1, sampleAction))
    {
        s_signalToSend = SIGUSR1;
        Log::Info("LinuxStackFramesCollector::SetupSignalHandler: Successfully setup signal handler for SIGUSR1 signal.");
        return true;
    }

    if (TrySetHandlerForSignal(SIGUSR2, sampleAction))
    {
        s_signalToSend = SIGUSR2;
        Log::Info("LinuxStackFramesCollector::SetupSignalHandler: Successfully setup signal handler for SIGUSR2 signal.");
        return true;
    }

    Log::Error("LinuxStackFramesCollector::SetupSignalHandler: Failed to setup signal handler for SIGUSR1 or SIGUSR2 signals.");
    return false;
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

static HRESULT GetFrameLocation(IXCLRDataStackWalk* pStackWalk, CLRDATA_ADDRESS* ip, CLRDATA_ADDRESS* sp)
{
    T_CONTEXT context;

    // https://github.com/dotnet/diagnostics/blob/b196cd60197fe12f845126394408cb72137827db/src/SOS/Strike/disasm.h
    HRESULT hr = pStackWalk->GetContext(0x0010000BL, sizeof(T_CONTEXT), NULL, (BYTE*)&context);
    if (FAILED(hr))
    {
        printf("GetFrameContext failed: %lx\n", hr);
        return hr;
    }
    if (hr == S_FALSE)
    {
        // GetFrameContext returns S_FALSE if the frame iterator is invalid.  That's basically an error for us.
        return E_FAIL;
    }
    // First find the info for the Frame object, if the current frame has an associated clr!Frame.
    *ip = context.Rip;
    *sp = context.Rsp;

    //std::cout << "GetFrameLocation - Rip: " << std::hex << *ip << std::endl;

    // if (IsDbgTargetArm())
    //     *ip = *ip & ~THUMB_CODE;

    return S_OK;
}


std::int32_t LinuxStackFramesCollector::StackWalkWithDac(void* context)
{
    //std::cout << "Flushing DAC" << std::endl;

    _dac->ClrDataProcess->Flush();

    if (context == nullptr)
    {
        _dac->DataTarget->OverrideIp = 0;

        //std::cout << "Selfwalk, no override" << std::endl;
    }
    else
    {
        auto originalContext = (ucontext_t*)context;
        _dac->DataTarget->OverrideIp = originalContext->uc_mcontext.gregs[REG_RIP];
        _dac->DataTarget->OverrideRsp = originalContext->uc_mcontext.gregs[REG_RSP];
        _dac->DataTarget->OverrideRbp = originalContext->uc_mcontext.gregs[REG_RBP];
        _dac->DataTarget->OverrideRdi = originalContext->uc_mcontext.gregs[REG_RDI];
        _dac->DataTarget->OverrideRsi = originalContext->uc_mcontext.gregs[REG_RSI];
        _dac->DataTarget->OverrideRbx = originalContext->uc_mcontext.gregs[REG_RBX];
        _dac->DataTarget->OverrideRdx = originalContext->uc_mcontext.gregs[REG_RDX];
        _dac->DataTarget->OverrideRcx = originalContext->uc_mcontext.gregs[REG_RCX];
        _dac->DataTarget->OverrideRax = originalContext->uc_mcontext.gregs[REG_RAX];
        _dac->DataTarget->OverrideR8 = originalContext->uc_mcontext.gregs[REG_R8];
        _dac->DataTarget->OverrideR9 = originalContext->uc_mcontext.gregs[REG_R9];
        _dac->DataTarget->OverrideR10 = originalContext->uc_mcontext.gregs[REG_R10];
        _dac->DataTarget->OverrideR11 = originalContext->uc_mcontext.gregs[REG_R11];
        _dac->DataTarget->OverrideR12 = originalContext->uc_mcontext.gregs[REG_R12];
        _dac->DataTarget->OverrideR13 = originalContext->uc_mcontext.gregs[REG_R13];
        _dac->DataTarget->OverrideR14 = originalContext->uc_mcontext.gregs[REG_R14];
        _dac->DataTarget->OverrideR15 = originalContext->uc_mcontext.gregs[REG_R15];

        //std::cout << "Registers are overriden - " << originalContext->uc_mcontext.gregs[REG_RIP] << " - " << originalContext->uc_mcontext.gregs[REG_RSP] << " - " << originalContext->uc_mcontext.gregs[REG_RBP] << std::endl;
        //std::cout << "Reading back: " << _dac->DataTarget->OverrideIp << " - " << _dac->DataTarget->OverrideRsp << " - " << _dac->DataTarget->OverrideRbp << std::endl;

    }


    IXCLRDataTask* task;

    auto result = _dac->ClrDataProcess->GetTaskByOSThreadID(OpSysTools::GetThreadId(), &task);

    if (FAILED(result))
    {
        std::cout << "GetTaskByOSThreadID failed for " << OpSysTools::GetThreadId() << " : " << result << std::endl;
        return E_FAIL;
    }

    IXCLRDataStackWalk* stackWalk;

    result = task->CreateStackWalk(CLRDATA_SIMPFRAME_MANAGED_METHOD |
                                       CLRDATA_SIMPFRAME_RUNTIME_MANAGED_CODE,
                                   &stackWalk);
    
    if (FAILED(result))
    {
        std::cout << "CreateStackWalk failed: " << result << std::endl;
        return E_FAIL;
    }

    int frameNumber = 0;
    int internalFrames = 0;
    HRESULT hr;
    do
    {
        CLRDATA_ADDRESS ip = 0, sp = 0;
        hr = GetFrameLocation(stackWalk, &ip, &sp);
        if (SUCCEEDED(hr))
        {
            DacpFrameData FrameData;
            HRESULT frameDataResult = FrameData.Request(stackWalk);
            if (SUCCEEDED(frameDataResult) && FrameData.frameAddr)
                sp = FrameData.frameAddr;

            // while ((numNativeFrames > 0) && (currentNativeFrame->StackOffset <= CDA_TO_UL64(sp)))
            //{
            //     if (currentNativeFrame->StackOffset != CDA_TO_UL64(sp))
            //     {
            //         PrintNativeStackFrame(out, currentNativeFrame, bSuppressLines);
            //     }
            //     currentNativeFrame++;
            //     numNativeFrames--;
            // }

            // Print the stack pointer.
            // std::cout << sp;

            // Print the method/Frame info
            if (SUCCEEDED(frameDataResult) && FrameData.frameAddr)
            {
                internalFrames++;

                // Skip the instruction pointer because it doesn't really mean anything for method frames
                // out.WriteColumn(1, bFull ? String("") : NativePtr(ip));

                // This is a clr!Frame.
                //std::cout << " clr frame - " << FrameData.frameAddr << std::endl;

                AddFrame(FrameData.frameAddr);

                // out.WriteColumn(2, GetFrameFromAddress(TO_TADDR(FrameData.frameAddr), pStackWalk, bFull));
            }
            else
            {
                // To get the source line number of the actual code that threw an exception, the IP needs
                // to be adjusted in certain cases.
                //
                // The IP of stack frame points to either:
                //
                // 1) Currently executing instruction (if you hit a breakpoint or are single stepping through).
                // 2) The instruction that caused a hardware exception (div by zero, null ref, etc).
                // 3) The instruction after the call to an internal runtime function (FCALL like IL_Throw,
                //    JIT_OverFlow, etc.) that caused a software exception.
                // 4) The instruction after the call to a managed function (non-leaf node).
                //
                // #3 and #4 are the cases that need IP adjusted back because they point after the call instruction
                // and may point to the next (incorrect) IL instruction/source line.  We distinguish these from #1
                // or #2 by either being non-leaf node stack frame (#4) or the present of an internal stack frame (#3).
                bool bAdjustIPForLineNumber = frameNumber > 0 || internalFrames > 0;
                frameNumber++;

                // The unmodified IP is displayed which points after the exception in most cases. This means that the
                // printed IP and the printed line number often will not map to one another and this is intentional.
                //std::cout << " ip - " << ip << std::endl;

                AddFrame(ip);

                // out.WriteColumn(1, InstructionPtr(ip));
                // out.WriteColumn(2, MethodNameFromIP(ip, bSuppressLines, bFull, bFull, bAdjustIPForLineNumber));
            }
        }

        hr = stackWalk->Next();
    } while (hr == S_OK);

    //std::cout << "Finished stackwalk" << std::endl;

    // _dac = nullptr;
    stackWalk->Release();
    task->Release();



    return S_OK;
}

std::int32_t LinuxStackFramesCollector::CollectCallStackCurrentThread(void* context)
{
    if (_dac != nullptr)
    {
        return StackWalkWithDac(context);   
    }

    try
    {
        std::int32_t resultErrorCode;

        {
            // Collect data for TraceContext tracking:
            bool traceContextDataCollected = TryApplyTraceContextDataFromCurrentCollectionThreadToSnapshot();

            // Now walk the stack:

            unw_context_t uc;
            unw_getcontext(&uc);

            unw_cursor_t cursor;
            unw_init_local(&cursor, &uc);

            // After every lib call that touches non-local state, check if the StackSamplerLoopManager requested this walk to abort:
            if (IsCurrentCollectionAbortRequested())
            {
                AddFakeFrame();
                return E_ABORT;
            }

            resultErrorCode = unw_step(&cursor);

            while (resultErrorCode > 0)
            {
                // After every lib call that touches non-local state, check if the StackSamplerLoopManager requested this walk to abort:
                if (IsCurrentCollectionAbortRequested())
                {
                    AddFakeFrame();
                    return E_ABORT;
                }

                unw_word_t nativeInstructionPointer;
                resultErrorCode = unw_get_reg(&cursor, UNW_REG_IP, &nativeInstructionPointer);
                if (resultErrorCode != 0)
                {
                    return resultErrorCode;
                }

                if (!AddFrame(nativeInstructionPointer))
                {
                    return S_FALSE;
                }

                resultErrorCode = unw_step(&cursor);
            }
        }
        return resultErrorCode;
    }
    catch (...)
    {
        return E_ABORT;
    }
}

void LinuxStackFramesCollector::CollectStackSampleSignalHandler(int32_t signal, siginfo_t* info, void* context)
{
    std::unique_lock<std::mutex> stackWalkInProgressLock(s_stackWalkInProgressMutex);
    LinuxStackFramesCollector* pCollectorInstanceCurrentlyStackWalking = s_pInstanceCurrentlyStackWalking;

    std::int32_t resultErrorCode = pCollectorInstanceCurrentlyStackWalking->CollectCallStackCurrentThread(context);
    stackWalkInProgressLock.unlock();
    pCollectorInstanceCurrentlyStackWalking->NotifyStackWalkCompleted(resultErrorCode);
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
        ss << std::setfill(' ') << std::setw(13) << "# occurrences" << "  |  " << "Error message\n";
        for (auto& errorCodeAndStats : _stats)
        {
            ss << std::setfill(' ') << std::setw(10) << errorCodeAndStats.second << "  |  " << ErrorCodeToString(errorCodeAndStats.first) << " (" << errorCodeAndStats.first << ")\n";
        }

        Log::Info("LinuxStackFramesCollector::CollectStackSampleImplementation: The sampler thread encoutered errors in the interval\n",
                  ss.str());
        _stats.clear();
    }
}