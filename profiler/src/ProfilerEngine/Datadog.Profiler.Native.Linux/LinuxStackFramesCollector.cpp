// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LinuxStackFramesCollector.h"

#include <cassert>
#include <chrono>
#include <errno.h>
#include <mutex>
#include <signal.h>
#include <sys/syscall.h>

#include <libunwind-x86_64.h>

#include "Log.h"
#include "ManagedThreadInfo.h"
#include "OpSysTools.h"
#include "ScopeFinalizer.h"
#include "StackSnapshotResultReusableBuffer.h"

using namespace std::chrono_literals;

std::mutex LinuxStackFramesCollector::s_signalHandlerInitLock;
std::mutex LinuxStackFramesCollector::s_stackWalkInProgressMutex;
bool LinuxStackFramesCollector::s_isSignalHandlerSetup = false;
int LinuxStackFramesCollector::s_signalToSend = -1;
LinuxStackFramesCollector* LinuxStackFramesCollector::s_pInstanceCurrentlyStackWalking = nullptr;

LinuxStackFramesCollector::LinuxStackFramesCollector(ICorProfilerInfo4* const _pCorProfilerInfo) :
    _pCorProfilerInfo(_pCorProfilerInfo),
    _lastStackWalkErrorCode{0},
    _stackWalkFinished{false}
{
    _pCorProfilerInfo->AddRef();
    InitializeSignalHandler();
}
LinuxStackFramesCollector::~LinuxStackFramesCollector()
{
    _pCorProfilerInfo->Release();
    // !! @ToDo: We must uninstall the signal handler!!
}

bool IsThreadAlive(::pid_t processId, ::pid_t threadId)
{
    return syscall(SYS_tgkill, processId, threadId, 0) == 0;
}

StackSnapshotResultBuffer* LinuxStackFramesCollector::CollectStackSampleImplementation(ManagedThreadInfo* pThreadInfo,
                                                                                       uint32_t* pHR,
                                                                                       bool selfCollect)
{
    long errorCode;

    if (selfCollect)
    {
        errorCode = CollectCallStackCurrentThread();
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
        auto scopeFinalizer = CreateScopeFinalizer(
            [] {
                s_pInstanceCurrentlyStackWalking = nullptr;
            });

        _stackWalkFinished = false;

        errorCode = syscall(SYS_tgkill, processId,osThreadId, s_signalToSend);

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
        Log::Info("LinuxStackFramesCollector::CollectStackSampleImplementation:"
                  " A problem occured while collecting a stack sample:",
                  " ", ErrorCodeToString(errorCode), " (", errorCode, ").",
                  " The stack sample collection may have been aborted, and the sample may",
                  " be invalid, however the execution will continue normally.");
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

bool LinuxStackFramesCollector::TrySetHandlerForSignal(int signal, struct sigaction& action)
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
        int result = sigaction(signal, &action, &oldAction);
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
    sampleAction.sa_flags = 0;
    sampleAction.sa_handler = LinuxStackFramesCollector::CollectStackSampleSignalHandler;
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

char const* LinuxStackFramesCollector::ErrorCodeToString(int errorCode)
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

std::int32_t LinuxStackFramesCollector::CollectCallStackCurrentThread()
{
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
    catch(...)
    {
        return E_ABORT;
    }
}

void LinuxStackFramesCollector::CollectStackSampleSignalHandler(int signal)
{
    std::unique_lock<std::mutex> stackWalkInProgressLock(s_stackWalkInProgressMutex);
    LinuxStackFramesCollector* pCollectorInstanceCurrentlyStackWalking = s_pInstanceCurrentlyStackWalking;

    std::int32_t resultErrorCode = pCollectorInstanceCurrentlyStackWalking->CollectCallStackCurrentThread();
    stackWalkInProgressLock.unlock();
    pCollectorInstanceCurrentlyStackWalking->NotifyStackWalkCompleted(resultErrorCode);
}
