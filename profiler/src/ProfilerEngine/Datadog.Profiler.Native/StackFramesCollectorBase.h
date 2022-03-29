// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>
#include <condition_variable>
#include <mutex>

#include "ManagedThreadInfo.h"
#include "StackSnapshotResultReusableBuffer.h"

class StackFramesCollectorBase
{
protected:
    StackFramesCollectorBase();

    bool TryAddFrame(StackFrameCodeKind codeKind,
                     FunctionID clrFunctionId,
                     UINT_PTR nativeInstructionPointer,
                     std::uint64_t moduleHandle);

    bool TryApplyTraceContextDataFromCurrentCollectionThreadToSnapshot(void);

    StackSnapshotResultBuffer* GetStackSnapshotResult(void);
    bool IsCurrentCollectionAbortRequested();

    // The XxxImplementation(..) methods below are the key routines to be implemented by the specific stack sample collectors.
    // We make them virtual but NOT astract, so that if a particular collector does not need to implement some
    // of these methods (because they are no-op for that specific collector), then they do not have to.
    // For this, we provide a reasonable default implementation.
    virtual void PrepareForNextCollectionImplementation(void);
    virtual bool SuspendTargetThreadImplementation(ManagedThreadInfo* pThreadInfo, bool* pIsTargetThreadSuspended);
    virtual void ResumeTargetThreadIfRequiredImplementation(ManagedThreadInfo* pThreadInfo, bool isTargetThreadSuspended, uint32_t* pErrorCodeHR);
    virtual StackSnapshotResultBuffer* CollectStackSampleImplementation(ManagedThreadInfo* pThreadInfo, uint32_t* pHR);

public:
    virtual ~StackFramesCollectorBase();
    StackFramesCollectorBase(StackFramesCollectorBase const&) = delete;
    StackFramesCollectorBase& operator=(StackFramesCollectorBase const&) = delete;

    virtual void OnDeadlock();

    void RequestAbortCurrentCollection(void);
    void PrepareForNextCollection(void);
    bool SuspendTargetThread(ManagedThreadInfo* pThreadInfo, bool* pIsTargetThreadSuspended);
    void ResumeTargetThreadIfRequired(ManagedThreadInfo* pThreadInfo, bool isTargetThreadSuspended, uint32_t* pErrorCodeHR);
    StackSnapshotResultBuffer* CollectStackSample(ManagedThreadInfo* pThreadInfo, uint32_t* pHR);

protected:
    ManagedThreadInfo* _pCurrentCollectionThreadInfo;

private:
    StackSnapshotResultReusableBuffer* _pReusableStackSnapshotResult;
    std::atomic<bool> _isCurrentCollectionAbortRequested;
    std::condition_variable _collectionAbortPerformedSignal;
    std::mutex _collectionAbortNotificationLock;
    bool _isRequestedCollectionAbortSuccessful;
};
