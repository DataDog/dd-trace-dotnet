// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LinuxClrBasedStackFramesCollector.h"

#include "Log.h"
#include "ManagedThreadList.h"
#include "ManagedThreadInfo.h"
#include "OpSysTools.h"
#include "ProfilerSignalManager.h"
#include "ScopeFinalizer.h"
#include "StackSnapshotResultBuffer.h"

#include <chrono>
#include <string.h>

using namespace std::chrono_literals;

std::mutex LinuxClrBasedStackFramesCollector::s_stackWalkInProgressMutex;
LinuxClrBasedStackFramesCollector* LinuxClrBasedStackFramesCollector::s_pInstanceCurrentlyStackWalking = nullptr;

// This method is called from the CLR so we need to use STDMETHODCALLTYPE macro to match the CLR declaration
HRESULT STDMETHODCALLTYPE LinuxStackSnapshotCallback(FunctionID funcId, UINT_PTR ip, COR_PRF_FRAME_INFO frameInfo, ULONG32 contextSize, BYTE context[], void* clientData);

LinuxClrBasedStackFramesCollector::LinuxClrBasedStackFramesCollector(ICorProfilerInfo4* info, ProfilerSignalManager* signalManager) :
    _info{info},
    _signalManager{signalManager}
{
    _info->AddRef();
    _signalManager->RegisterHandler(LinuxClrBasedStackFramesCollector::CollectStackSampleSignalHandler);
}

LinuxClrBasedStackFramesCollector::~LinuxClrBasedStackFramesCollector()
{
    _info->Release();
}

StackSnapshotResultBuffer* LinuxClrBasedStackFramesCollector::CollectStackSampleImplementation(ManagedThreadInfo* pThreadInfo,
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
        if (!_signalManager->IsHandlerInPlace())
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

        errorCode = _signalManager->SendSignal(threadId);

        if (errorCode == -1)
        {
            Log::Warn("LinuxClrBasedStackFramesCollector::CollectStackSampleImplementation:"
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
    //if (errorCode < 0)
    //{
    //    UpdateErrorStats(errorCode);
    //}

    *pHR = (errorCode == 0) ? S_OK : E_FAIL;

    return GetStackSnapshotResult();
}

// This symbol is defined in the Datadog.Linux.ApiWrapper. It allows us to check if the thread to be profiled
// contains a frame of a function that might cause a deadlock.
extern "C" unsigned long long dd_inside_wrapped_functions() __attribute__((weak));

std::int32_t LinuxClrBasedStackFramesCollector::CollectCallStackCurrentThread()
{
    if (dd_inside_wrapped_functions != nullptr && dd_inside_wrapped_functions() != 0)
    {
        return E_ABORT;
    }

    try
    {
        // Collect data for TraceContext tracking:
        bool traceContextDataCollected = TryApplyTraceContextDataFromCurrentCollectionThreadToSnapshot();

        auto res = _info->DoStackSnapshot(
            static_cast<ThreadID>(NULL),
            LinuxStackSnapshotCallback,
            _COR_PRF_SNAPSHOT_INFO::COR_PRF_SNAPSHOT_DEFAULT,
            this,
            nullptr, // BYTE* context
            0);      //

        // to review
        return S_OK;
    }
    catch (...)
    {
        return E_ABORT;
    }
}



bool LinuxClrBasedStackFramesCollector::CanCollect(int32_t threadId, pid_t processId) const
{
    // on OSX, processId can be equal to 0. https://sourcegraph.com/github.com/dotnet/runtime/-/blob/src/coreclr/pal/src/exception/signal.cpp?L818:5&subtree=true
    // Since the profiler does not run on OSX, we leave it like this.
    auto* currentThreadInfo = _pCurrentCollectionThreadInfo;
    return currentThreadInfo != nullptr && currentThreadInfo->GetOsThreadId() == threadId && processId == _processId;
}

void LinuxClrBasedStackFramesCollector::MarkAsInterrupted()
{
    auto* currentThreadInfo = _pCurrentCollectionThreadInfo;

    if (currentThreadInfo != nullptr)
    {
        currentThreadInfo->MarkAsInterrupted();
    }
}

void LinuxClrBasedStackFramesCollector::NotifyStackWalkCompleted(std::int32_t resultErrorCode)
{
    _lastStackWalkErrorCode = resultErrorCode;
    _stackWalkFinished = true;
    _stackWalkInProgressWaiter.notify_one();
}

bool LinuxClrBasedStackFramesCollector::CollectStackSampleSignalHandler(int signal, siginfo_t* info, void* context)
{
    // Libunwind can overwrite the value of errno - save it beforehand and restore it at the end
    auto oldErrno = errno;

    bool success = false;

    LinuxClrBasedStackFramesCollector* pCollectorInstance = s_pInstanceCurrentlyStackWalking;

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
                auto resultErrorCode = pCollectorInstance->CollectCallStackCurrentThread();

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



// This method is called from the CLR so we need to use STDMETHODCALLTYPE macro to match the CLR declaration
HRESULT STDMETHODCALLTYPE LinuxStackSnapshotCallback(
    FunctionID functionId,
    UINT_PTR ip,
    COR_PRF_FRAME_INFO /* frameInfo */,
    ULONG32 /* contextSize */,
    BYTE[] /* context[] */,
    void* clientData)
{

    if (clientData == nullptr)
    {
        return S_OK;
    }

    auto const pStackFramesCollector = static_cast<LinuxClrBasedStackFramesCollector*>(clientData);

    if (pStackFramesCollector == nullptr)
    {
        return S_FALSE; // Abort stackwalk since we have no place to store results. !! @ToDo: Should we be returning E_FAIL??
    }

    // If the StackSamplerLoopManager requested this walk to abort, do so now.
    if (pStackFramesCollector->IsCurrentCollectionAbortRequested())
    {
        pStackFramesCollector->AddFakeFrame();
        return S_FALSE; //  @ToDo: Should we be returning E_ABORT ?
    }

    if (pStackFramesCollector->AddFrame(ip))
    {
        return S_OK;
    }

    // Use the info we collected so far and abort further stack walking for this time.
    // There was no error, we just run out of buffer space for the results.
    // We signal to the CLR an S_FALSE result to indicate that we thewre was no error, but the stack walk should not be continued.
    return S_FALSE;
}