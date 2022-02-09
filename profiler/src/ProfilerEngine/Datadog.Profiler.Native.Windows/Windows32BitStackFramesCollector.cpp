// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Windows32BitStackFramesCollector.h"

#include <cinttypes>
#include <winnt.h>

#include "Log.h"
#include "ManagedThreadInfo.h"
#include "StackFrameCodeKind.h"
#include "StackSnapshotResultFrameInfo.h"
#include "StackSnapshotResultReusableBuffer.h"

// This method is called from the CLR so we need to use STDMETHODCALLTYPE macro to match the CLR declaration
HRESULT STDMETHODCALLTYPE StackSnapshotCallbackHandlerImpl(FunctionID funcId, UINT_PTR ip, COR_PRF_FRAME_INFO frameInfo, ULONG32 contextSize, BYTE context[], void* clientData);

Windows32BitStackFramesCollector::Windows32BitStackFramesCollector(ICorProfilerInfo4* const _pCorProfilerInfo) :
    StackFramesCollectorBase(),
    _pCorProfilerInfo(_pCorProfilerInfo)
{
    _pCorProfilerInfo->AddRef();
}

Windows32BitStackFramesCollector::~Windows32BitStackFramesCollector()
{
    _pCorProfilerInfo->Release();
}

StackSnapshotResultBuffer* Windows32BitStackFramesCollector::CollectStackSampleImplementation(ManagedThreadInfo* pThreadInfo,
                                                                                              uint32_t* pHR)
{
    // Collect data for TraceContext Tracking:
    bool traceContextDataCollected = this->TryApplyTraceContextDataFromCurrentCollectionThreadToSnapshot();
    assert(traceContextDataCollected);

    // Now walk the stack:
    __try
    {
        // Sometimes, we could hit an access violation, so catch it and just return.
        // This can happen if we are in a deadlock situation and resume the target thread
        // while walking its stack.

        StackSnapshotCallbackClientData customParams(pThreadInfo, this);
        HRESULT hr = _pCorProfilerInfo->DoStackSnapshot(
            pThreadInfo->GetClrThreadId(),
            StackSnapshotCallbackHandlerImpl,
            _COR_PRF_SNAPSHOT_INFO::COR_PRF_SNAPSHOT_DEFAULT,
            &customParams,
            nullptr, // BYTE* context
            0);      // ULONG32 contextSize

        *pHR = hr;

        return GetStackSnapshotResult();
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        TryAddFrame(StackFrameCodeKind::MultipleMixed, 0, 0, 0);

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

    auto const pInvocationState = static_cast<Windows32BitStackFramesCollector::StackSnapshotCallbackClientData*>(clientData);
    Windows32BitStackFramesCollector* const pStackFramesCollector = pInvocationState->PtrStackFramesCollector;

    if (pStackFramesCollector == nullptr)
    {
        return S_FALSE; // Abort stackwalk since we have no place to store results. !! @ToDo: Should we be returning E_FAIL??
    }

    // If the StackSamplerLoopManager requested this walk to abort, do so now.
    if (pStackFramesCollector->IsCurrentCollectionAbortRequested())
    {
        pStackFramesCollector->TryAddFrame(StackFrameCodeKind::MultipleMixed, 0, 0, 0);
        return S_FALSE; //  @ToDo: Should we be returning E_ABORT ?
    }

    bool canStoreFrame;
    if (functionId == 0)
    {
        canStoreFrame = pStackFramesCollector->TryAddFrame(StackFrameCodeKind::UnknownNative, 0, ip, 0);
    }
    else
    {
        // At this point we want to do as little work as possible (but as much as necessary to be able to later resolve the symbols).
        // For managed frames, we will call later _pCorProfilerInfo->GetFunctionInfo2() for symbol resolution.
        // That takes the functionId and the COR_PRF_FRAME_INFO value.
        // The COR_PRF_FRAME_INFO value is only valid here, within the StackSnapshotCallback, but the functionId can be used later.
        // However, although the COR_PRF_FRAME_INFO value we receive in this StackSnapshotCallback is not actually helpful/useful.
        // So, we do not bother storing it. Instead, we just store the functionId to pass it to GetFunctionInfo2() later.
        // For info on this useless-ness of the COR_PRF_FRAME_INFO, see
        // https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/icorprofilerinfo2-getfunctioninfo2-method#remarks
        // and
        // https://github.com/dotnet/runtime/blob/d14b50ae2186af77ad9b68370978182584b43e65/docs/design/coreclr/profiling/davbr-blog-archive/Generics%20and%20Your%20Profiler.md
        canStoreFrame = pStackFramesCollector->TryAddFrame(StackFrameCodeKind::ClrManaged, functionId, ip, 0);
    }

    if (!canStoreFrame)
    {
        // Use the info we collected so far and abort further stack walking for this time.
        // There was no error, we just run out of buffer space for the results.
        // We signal to the CLR an S_FALSE result to indicate that we thewre was no error, but the stack walk should not be continued.
        return S_FALSE;
    }
    else
    {
        return S_OK;
    }
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