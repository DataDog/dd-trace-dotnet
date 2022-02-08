// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LinuxStackFramesCollector.h"

#include <cassert>
#include <errno.h>
#include <mutex>
#include <signal.h>
#include <sys/syscall.h>

#include <libunwind-x86_64.h>

#include "Log.h"
#include "ManagedThreadInfo.h"
#include "ScopeFinalizer.h"
#include "StackFrameCodeKind.h"
#include "StackSnapshotResultReusableBuffer.h"

std::mutex LinuxStackFramesCollector::s_stackWalkInProgressMutex;
LinuxStackFramesCollector* LinuxStackFramesCollector::s_pInstanceCurrentlyStackWalking = nullptr;

LinuxStackFramesCollector::LinuxStackFramesCollector(ICorProfilerInfo4* const _pCorProfilerInfo) :
    StackFramesCollectorBase(),
    _pCorProfilerInfo(_pCorProfilerInfo),
    _signalToSend{-1},
    _isSignalHandlerSetup{false},
    _lastStackWalkErrorCode{0}
{
    _pCorProfilerInfo->AddRef();
    _isSignalHandlerSetup = SetupSignalHandler();
}

LinuxStackFramesCollector::~LinuxStackFramesCollector()
{
    _pCorProfilerInfo->Release();
    // !! @ToDo: We must uninstall the signal handler!!
}

StackSnapshotResultBuffer* LinuxStackFramesCollector::CollectStackSampleImplementation(ManagedThreadInfo* pThreadInfo,
                                                                                       uint32_t* pHR)
{
    // For now we ignore isTargetThreadSameAsCurrentThread.
    // However, we should probably look at it, and if it is True, and do the stack walk synchronously, rather than using a signal.

    if (!_isSignalHandlerSetup)
    {
        Log::Debug("LinuxStackFramesCollector::CollectStackSampleImplementation:"
                   " Signal handler not set up. Cannot collect callstacks."
                   " (Earlier log entry may contain additinal details.)");

        *pHR = E_FAIL;

        return GetStackSnapshotResult();
    }

    long errorCode;
    {
        std::unique_lock<std::mutex> stackWalkInProgressLock(s_stackWalkInProgressMutex);

        const DWORD osThreadId = pThreadInfo->GetOsThreadId();

        Log::Debug("LinuxStackFramesCollector::CollectStackSampleImplementation:"
                   " Sending signal ",
                   _signalToSend, " to thread with osThreadId=", osThreadId, ".");

        s_pInstanceCurrentlyStackWalking = this;
        auto scopeFinalizer = CreateScopeFinalizer(
            [] {
                s_pInstanceCurrentlyStackWalking = nullptr;
            });

        errorCode = syscall(SYS_tgkill, static_cast<::pid_t>(getpid()), static_cast<::pid_t>(osThreadId), _signalToSend);

        if (errorCode == -1)
        {
            Log::Error("LinuxStackFramesCollector::CollectStackSampleImplementation:"
                       " Unable to send signal USR1 to thread with osThreadId=",
                       osThreadId, ". Error code: ",
                       strerror(errno));
        }
        else
        {
            _stackWalkInProgressWaiter.wait(stackWalkInProgressLock);
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
        _signalToSend = SIGUSR1;
        Log::Info("LinuxStackFramesCollector::SetupSignalHandler: Successfully setup signal handler for SIGUSR1 signal.");
        return true;
    }

    if (TrySetHandlerForSignal(SIGUSR2, sampleAction))
    {
        _signalToSend = SIGUSR2;
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

void LinuxStackFramesCollector::CollectStackSampleSignalHandler(int signal)
{
    LinuxStackFramesCollector* pCollectorInstanceCurrentlyStackWalking;
    std::int32_t resultErrorCode;

    std::unique_lock<std::mutex> stackWalkInProgressLock(s_stackWalkInProgressMutex);

    pCollectorInstanceCurrentlyStackWalking = s_pInstanceCurrentlyStackWalking;

    // Fail fast in DEBUG builds:
    assert(pCollectorInstanceCurrentlyStackWalking != nullptr);

    // Bail out in RELEASE builds:
    if (pCollectorInstanceCurrentlyStackWalking == nullptr)
    {
        return;
    }

    {
        auto scopeFinalizer = CreateScopeFinalizer(
            [&pCollectorInstanceCurrentlyStackWalking, &stackWalkInProgressLock, resultErrorCode] {
                // @ToDo: Is resultErrorCode used correctly, so that its later value change applies to the inside of this lambda?

                if (pCollectorInstanceCurrentlyStackWalking != nullptr)
                {
                    stackWalkInProgressLock.unlock();
                    pCollectorInstanceCurrentlyStackWalking->NotifyStackWalkCompleted(resultErrorCode);
                }
            });

        // Collect data for TraceContext tracking:

        bool traceContextDataCollected = pCollectorInstanceCurrentlyStackWalking->TryApplyTraceContextDataFromCurrentCollectionThreadToSnapshot();
        assert(true == traceContextDataCollected);

        // Now walk the stack:

        unw_context_t uc;
        unw_getcontext(&uc);

        unw_cursor_t cursor;
        unw_init_local(&cursor, &uc);

        // After every lib call that touches non-local state, check if the StackSamplerLoopManager requested this walk to abort:
        if (pCollectorInstanceCurrentlyStackWalking->IsCurrentCollectionAbortRequested())
        {
            pCollectorInstanceCurrentlyStackWalking->TryAddFrame(StackFrameCodeKind::MultipleMixed, 0, 0, 0);
            resultErrorCode = E_ABORT;
            return;
        }

        resultErrorCode = unw_step(&cursor);

        // @ToDo: when do we stop? How do we signal normal vs abnormal completion via error codes here?
        while (resultErrorCode > 0)
        {
            // After every lib call that touches non-local state, check if the StackSamplerLoopManager requested this walk to abort:
            if (pCollectorInstanceCurrentlyStackWalking->IsCurrentCollectionAbortRequested())
            {
                pCollectorInstanceCurrentlyStackWalking->TryAddFrame(StackFrameCodeKind::MultipleMixed, 0, 0, 0);
                resultErrorCode = E_ABORT;
                return;
            }

            unw_word_t nativeInstructionPointer;
            resultErrorCode = unw_get_reg(&cursor, UNW_REG_IP, &nativeInstructionPointer);
            if (resultErrorCode != 0)
            {
                return;
            }

            if (!pCollectorInstanceCurrentlyStackWalking->TryAddFrame(StackFrameCodeKind::NotDetermined, 0, nativeInstructionPointer, 0))
            {
                resultErrorCode = S_FALSE;
                return;
            }

            resultErrorCode = unw_step(&cursor);
        }
    }
}
