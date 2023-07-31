// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "StackFramesCollectorBase.h"

#include "EnvironmentVariables.h"
#include "ManagedThreadList.h"
#include "OpSysTools.h"

#include "shared/src/native-src/util.h"

#include <assert.h>
#include <chrono>
#include <condition_variable>
#include <mutex>

StackFramesCollectorBase::StackFramesCollectorBase()
{
    _isRequestedCollectionAbortSuccessful = false;
    _pStackSnapshotResult = std::make_unique<StackSnapshotResultBuffer>();
    _pCurrentCollectionThreadInfo = nullptr;
    _isCurrentCollectionAbortRequested.store(false);
}

bool StackFramesCollectorBase::AddFrame(std::uintptr_t ip)
{
    return _pStackSnapshotResult->AddFrame(ip);
}

void StackFramesCollectorBase::AddFakeFrame()
{
    _pStackSnapshotResult->AddFakeFrame();
}

void StackFramesCollectorBase::SetFrameCount(std::uint16_t count)
{
    _pStackSnapshotResult->SetFramesCount(count);
}

std::pair<uintptr_t*, std::uint16_t> StackFramesCollectorBase::Data()
{
    return {_pStackSnapshotResult->Data(), StackSnapshotResultBuffer::MaxSnapshotStackDepth_Limit};
}

void StackFramesCollectorBase::RequestAbortCurrentCollection()
{
    std::lock_guard<std::mutex> lock(_collectionAbortNotificationLock);

    _isRequestedCollectionAbortSuccessful = false;
    _isCurrentCollectionAbortRequested.store(true);
}

//
// =========== Default implementations of Protected Virtual business logic funcitons: ===========
//

void StackFramesCollectorBase::PrepareForNextCollectionImplementation()
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
                                                                                      uint32_t* pHR,
                                                                                      bool selfCollect)
{
    // The actual business logic provided by a subclass goes into the XxxImplementation(..) methods.
    // This is a fallback implementation, so that the implementing sub-class does not need to overwrite this method if it is a no-op.

    bool frame1Added = AddFrame(1);
    bool frame2Added = AddFrame(2);
    bool frame3Added = AddFrame(3);

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
    // InternalCIVisibilitySpanId for CODE HOTSPOT in the whole process
    static std::uint64_t ciVisibilitySpanId = 0;
    static bool ciVisibilitySpanRequested = false;

    if (!ciVisibilitySpanRequested)
    {
        const auto internalCIVisibilitySpanId = ::shared::GetEnvironmentValue(EnvironmentVariables::InternalCIVisibilitySpanId);
        if (!internalCIVisibilitySpanId.empty())
        {
            ciVisibilitySpanId = std::stoull(shared::ToString(internalCIVisibilitySpanId));
        }

        ciVisibilitySpanRequested = true;
    }

    if (ciVisibilitySpanId > 0)
    {
        _pStackSnapshotResult->SetLocalRootSpanId(ciVisibilitySpanId);
        _pStackSnapshotResult->SetSpanId(ciVisibilitySpanId);
        return true;
    }
    
    // If TraceContext Tracking is not enabled, then we will simply get zero IDs.
    ManagedThreadInfo* pCurrentCollectionThreadInfo = _pCurrentCollectionThreadInfo;
    if (nullptr != pCurrentCollectionThreadInfo && pCurrentCollectionThreadInfo->CanReadTraceContext())
    {
        std::uint64_t localRootSpanId = pCurrentCollectionThreadInfo->GetLocalRootSpanId();
        std::uint64_t spanId = pCurrentCollectionThreadInfo->GetSpanId();

        _pStackSnapshotResult->SetLocalRootSpanId(localRootSpanId);
        _pStackSnapshotResult->SetSpanId(spanId);

        return true;
    }

    return false;
}

StackSnapshotResultBuffer* StackFramesCollectorBase::GetStackSnapshotResult()
{
    return _pStackSnapshotResult.get();
}

// ----------- Inline stubs for APIs that are specific to overriding implementations: -----------
// They perform the work required for the shared base implementation (this class) and then invoke the respective XxxImplementaiton(..) method.
// This is less error-prone than simply making these methods virtual and relying on the sub-classes to remember calling the base class method.

void StackFramesCollectorBase::PrepareForNextCollection()
{
    // We cannot allocate memory once a thread is suspended.
    // This is because malloc() uses a lock and so if we suspend a thread that was allocating, we will deadlock.
    // So we pre-allocate the memory buffer and reset it before suspending the target thread.
    _pStackSnapshotResult->Reset();

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

    const auto currentThreadId = OpSysTools::GetThreadId();

    // Execute the actual collection:
    StackSnapshotResultBuffer* result = CollectStackSampleImplementation(pThreadInfo, pHR, pThreadInfo->GetOsThreadId() == currentThreadId);

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