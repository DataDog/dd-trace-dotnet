// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "StackFramesCollectorBase.h"

#include <assert.h>
#include <chrono>
#include <condition_variable>
#include <mutex>

StackFramesCollectorBase::StackFramesCollectorBase()
{
    _isRequestedCollectionAbortSuccessful = false;
    _pReusableStackSnapshotResult = new StackSnapshotResultReusableBuffer();
    _pCurrentCollectionThreadInfo = nullptr;
}

StackFramesCollectorBase::~StackFramesCollectorBase()
{
    StackSnapshotResultReusableBuffer* pReusableStackSnapshotResult = _pReusableStackSnapshotResult;
    if (pReusableStackSnapshotResult != nullptr)
    {
        delete pReusableStackSnapshotResult;
        _pReusableStackSnapshotResult = nullptr;
    }
}

bool StackFramesCollectorBase::TryAddFrame(StackFrameCodeKind codeKind,
                                           FunctionID clrFunctionId,
                                           UINT_PTR nativeInstructionPointer,
                                           std::uint64_t moduleHandle)
{
    StackSnapshotResultFrameInfo* pCurrentFrameInfo;
    bool hasCapacityForSubsequentFrames;
    bool hasCapacityForThisFrame = _pReusableStackSnapshotResult->TryAddNextFrame(&pCurrentFrameInfo, &hasCapacityForSubsequentFrames);

    if (!hasCapacityForThisFrame)
    {
        // We run out of the preallocated space for storing results.
        // Allocating while threads are suspended is forbidden.
        // We are just being defensive: we should really never get here,
        // since last iteration we had hasCapacityForSubsequentFrames == true.

        // We will also increase the size of the preallocated buffer for the next time:
        _pReusableStackSnapshotResult->GrowCapacityAtNextReset();

        // Use the info we collected so far and abort further stack walking for this time:
        return false;
    }

    if (!hasCapacityForSubsequentFrames)
    {
        // We have preallocated space for only one more frame.
        // (allocating while threads are suspended is forbidden.)

        // We need to use it for a marker that signals that more frames exist, but we do not have information about them:
        pCurrentFrameInfo->Set(StackFrameCodeKind::MultipleMixed, 0, 0, 0);

        // We will also increase the size of the preallocated buffer for the next time:
        _pReusableStackSnapshotResult->GrowCapacityAtNextReset();

        // Use the info we collected so far and abort further stack walking for this time:
        return false;
    }

    pCurrentFrameInfo->Set(codeKind, clrFunctionId, nativeInstructionPointer, moduleHandle);

    return true;
}

void StackFramesCollectorBase::RequestAbortCurrentCollection(void)
{
    std::lock_guard<std::mutex> lock(_collectionAbortNotificationLock);

    _isRequestedCollectionAbortSuccessful = false;
    _isCurrentCollectionAbortRequested.store(true);
}

//
// =========== Default implementations of Protected Virtual business logic funcitons: ===========
//

void StackFramesCollectorBase::PrepareForNextCollectionImplementation(void)
{
    // The actual business logic provided by a subclass goes into the XxxImplementation(..) methods.
    // This is a fallback implementation, so that the implementing sub-class does not need to overwrite this method if it is a no-op.
}

bool StackFramesCollectorBase::SuspendTargetThreadImplementation(ManagedThreadInfo* pThreadInfo,
                                                                 bool* pIsTargetThreadSuspended)
{
    // The actual business logic provided by a subclass goes into the XxxImplementation(..) methods.
    // This is a fallback implementation, so that the implementing sub-class does not need to overwrite this method if it is a no-op.

    *pIsTargetThreadSuspended = false;
    return true;
}

void StackFramesCollectorBase::ResumeTargetThreadIfRequiredImplementation(ManagedThreadInfo* pThreadInfo,
                                                                          bool isTargetThreadSuspended,
                                                                          uint32_t* pErrorCodeHR)
{
    // The actual business logic provided by a subclass goes into the XxxImplementation(..) methods.
    // This is a fallback implementation, so that the implementing sub-class does not need to overwrite this method if it is a no-op.

    if (pErrorCodeHR != nullptr)
    {
        *pErrorCodeHR = isTargetThreadSuspended ? E_FAIL : S_OK;
    }
}

StackSnapshotResultBuffer* StackFramesCollectorBase::CollectStackSampleImplementation(ManagedThreadInfo* pThreadInfo,
                                                                                      uint32_t* pHR)
{
    // The actual business logic provided by a subclass goes into the XxxImplementation(..) methods.
    // This is a fallback implementation, so that the implementing sub-class does not need to overwrite this method if it is a no-op.

    bool frame1Added = TryAddFrame(StackFrameCodeKind::Dummy, 0, 0, 0);
    bool frame2Added = TryAddFrame(StackFrameCodeKind::Dummy, 0, 0, 0);
    bool frame3Added = TryAddFrame(StackFrameCodeKind::Dummy, 0, 0, 0);

    if (pHR != nullptr)
    {
        *pHR = (frame1Added && frame2Added && frame3Added) ? S_OK : E_FAIL;
    }

    return GetStackSnapshotResult();
}

bool StackFramesCollectorBase::IsCurrentCollectionAbortRequested()
{
    return _isCurrentCollectionAbortRequested.load();
}

bool StackFramesCollectorBase::TryApplyTraceContextDataFromCurrentCollectionThreadToSnapshot()
{
    // If TraceContext Tracking is not enabled, then we will simply get zero IDs.
    ManagedThreadInfo* pCurrentCollectionThreadInfo = _pCurrentCollectionThreadInfo;
    if (nullptr != pCurrentCollectionThreadInfo)
    {
        std::uint64_t traceId = pCurrentCollectionThreadInfo->GetTraceContextTraceId();
        std::uint64_t spanId = pCurrentCollectionThreadInfo->GetTraceContextSpanId();

        _pReusableStackSnapshotResult->SetTraceContextTraceId(traceId);
        _pReusableStackSnapshotResult->SetTraceContextSpanId(spanId);

        return true;
    }

    return false;
}

StackSnapshotResultBuffer* StackFramesCollectorBase::GetStackSnapshotResult()
{
    return _pReusableStackSnapshotResult;
}

// ----------- Inline stubs for APIs that are specific to overriding implementations: -----------
// They perform the work required for the shared base implementation (this class) and then invoke the respective XxxImplementaiton(..) method.
// This is less error-prone than simply making these methods virtual and relying on the sub-classes to remember calling the base class method.

void StackFramesCollectorBase::PrepareForNextCollection(void)
{
    // We cannot allocate memory once a thread is suspended.
    // This is because malloc() uses a lock and so if we suspend a thread that was allocating, we will deadlock.
    // So we pre-allocate the memory buffer and reset it before suspending the target thread.
    _pReusableStackSnapshotResult->Reset();

    // Clear the current collection thread pointer:
    _pCurrentCollectionThreadInfo = nullptr;

    // Clean up initialization state:
    _isCurrentCollectionAbortRequested.store(false);
    _isRequestedCollectionAbortSuccessful = false;

    // Subclasses can implement their own specific initialization before each collection. Invoke it:
    PrepareForNextCollectionImplementation();
}

bool StackFramesCollectorBase::SuspendTargetThread(ManagedThreadInfo* pThreadInfo, bool* pIsTargetThreadSuspended)
{
    return SuspendTargetThreadImplementation(pThreadInfo, pIsTargetThreadSuspended);
}

void StackFramesCollectorBase::ResumeTargetThreadIfRequired(ManagedThreadInfo* pThreadInfo, bool isTargetThreadSuspended, uint32_t* pErrorCodeHR)
{
    ResumeTargetThreadIfRequiredImplementation(pThreadInfo, isTargetThreadSuspended, pErrorCodeHR);
}

StackSnapshotResultBuffer* StackFramesCollectorBase::CollectStackSample(ManagedThreadInfo* pThreadInfo, uint32_t* pHR)
{
    // Update state with the info for the thread that we are collecting:
    _pCurrentCollectionThreadInfo = pThreadInfo;

    // Execute the actual collection:
    StackSnapshotResultBuffer* result = CollectStackSampleImplementation(pThreadInfo, pHR);

    // No longer collecting the specified thread:
    _pCurrentCollectionThreadInfo = nullptr;

    // If someone has requested an abort, notify them now:

    if (IsCurrentCollectionAbortRequested())
    {
        {
            std::lock_guard<std::mutex> lock(_collectionAbortNotificationLock);
            _isRequestedCollectionAbortSuccessful = true;
        }

        _collectionAbortPerformedSignal.notify_all();
    }

    return result;
}

void StackFramesCollectorBase::OnDeadlock()
{
    // In 32bits, we use the method DoStackSnapshot to walk and collect a thread callstack.
    // The DoStackSnapshot method calls SuspendThread/ResumeThread.
    // In case of a deadlock, if the sampling thread has not gracefully finished, it will be killed.
    // The result will be: 1 call to ResumeThread missing.
}