// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Windows32BitStackFramesCollector.h"

#include <cinttypes>
#include <winnt.h>

#include "Log.h"
#include "ManagedThreadInfo.h"
#include "StackSnapshotResultReusableBuffer.h"

// This method is called from the CLR so we need to use STDMETHODCALLTYPE macro to match the CLR declaration
HRESULT STDMETHODCALLTYPE StackSnapshotCallbackHandlerImpl(FunctionID funcId, UINT_PTR ip, COR_PRF_FRAME_INFO frameInfo, ULONG32 contextSize, BYTE context[], void* clientData);

Windows32BitStackFramesCollector::Windows32BitStackFramesCollector(ICorProfilerInfo4* const _pCorProfilerInfo) :
    _pCorProfilerInfo(_pCorProfilerInfo)
{
    _pCorProfilerInfo->AddRef();
}

Windows32BitStackFramesCollector::~Windows32BitStackFramesCollector()
{
    _pCorProfilerInfo->Release();
}

StackSnapshotResultBuffer* Windows32BitStackFramesCollector::CollectStackSampleImplementation(ManagedThreadInfo* pThreadInfo,
                                                                                              uint32_t* pHR,
                                                                                              bool selfCollect)
{
    // Collect data for TraceContext Tracking:
    bool traceContextDataCollected = this->TryApplyTraceContextDataFromCurrentCollectionThreadToSnapshot();
    assert(traceContextDataCollected);

    // Now walk the stack:
    __try
    {
        HANDLE osThreadHandle = INVALID_HANDLE_VALUE;

        auto hr = _pCorProfilerInfo->GetHandleFromThread(pThreadInfo->GetClrThreadId(), &osThreadHandle);

        if (FAILED(hr) || osThreadHandle == INVALID_HANDLE_VALUE || osThreadHandle == nullptr)
        {
            // Looks like the thread got destroyed, or we don't have its information yet
            *pHR = E_ABORT;
            return GetStackSnapshotResult();
        }
        
        // Sometimes, we could hit an access violation, so catch it and just return.
        // This can happen if we are in a deadlock situation and resume the target thread
        // while walking its stack.

        hr = _pCorProfilerInfo->DoStackSnapshot(
            selfCollect ? NULL : pThreadInfo->GetClrThreadId(),
            StackSnapshotCallbackHandlerImpl,
            _COR_PRF_SNAPSHOT_INFO::COR_PRF_SNAPSHOT_DEFAULT,
            this,
            nullptr, // BYTE* context
            0);      // ULONG32 contextSize

        *pHR = hr;

        return GetStackSnapshotResult();
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        AddFakeFrame();

        *pHR = E_ABORT;
        return GetStackSnapshotResult();
    }
}

// This method is called from the CLR so we need to use STDMETHODCALLTYPE macro to match the CLR declaration
HRESULT STDMETHODCALLTYPE StackSnapshotCallbackHandlerImpl(
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

    auto const pStackFramesCollector = static_cast<Windows32BitStackFramesCollector*>(clientData);

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

void Windows32BitStackFramesCollector::OnDeadlock()
{
    // The DoStackSnapshot method calls SuspendThread/ResumeThread.
    // In case of a deadlock, the sampling thread will be killed.
    // We have to call ResumeThread.

    // TODO: Maybe return the count to detect failed call to ResumeThread

    auto targetThread = _pCurrentCollectionThreadInfo;
    if (targetThread == nullptr)
        return;

    auto count = ::ResumeThread(targetThread->GetOsThreadHandle());
    Log::Info("Windows32BitStackFramesCollector::OnDeadlock() : Resuming sampled thread ", targetThread->GetOsThreadId(),
              "(= 0x", std::hex, targetThread->GetClrThreadId(), ")", ". Count=", std::dec, count);
}