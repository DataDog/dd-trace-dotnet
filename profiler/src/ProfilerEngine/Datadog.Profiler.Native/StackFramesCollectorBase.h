// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>
#include <condition_variable>
#include <memory>
#include <mutex>

#include "ManagedThreadInfo.h"
#include "StackSnapshotResultBuffer.h"

class IConfiguration;

class StackFramesCollectorBase
{
protected:
    StackFramesCollectorBase(IConfiguration const * _configuration);

    bool TryApplyTraceContextDataFromCurrentCollectionThreadToSnapshot();
    bool AddFrame(std::uintptr_t ip);
    void AddFakeFrame();

    StackSnapshotResultBuffer* GetStackSnapshotResult();
    bool IsCurrentCollectionAbortRequested();

    // The XxxImplementation(..) methods below are the key routines to be implemented by the specific stack sample collectors.
    // We make them virtual but NOT astract, so that if a particular collector does not need to implement some
    // of these methods (because they are no-op for that specific collector), then they do not have to.
    // For this, we provide a reasonable default implementation.
    virtual void PrepareForNextCollectionImplementation();
    virtual bool SuspendTargetThreadImplementation(ManagedThreadInfo* pThreadInfo, bool* pIsTargetThreadSuspended);
    virtual void ResumeTargetThreadIfRequiredImplementation(ManagedThreadInfo* pThreadInfo, bool isTargetThreadSuspended, uint32_t* pErrorCodeHR);
    virtual StackSnapshotResultBuffer* CollectStackSampleImplementation(ManagedThreadInfo* pThreadInfo, uint32_t* pHR, bool selfCollect);

public:
    virtual ~StackFramesCollectorBase() = default;
    StackFramesCollectorBase(StackFramesCollectorBase const&) = delete;
    StackFramesCollectorBase& operator=(StackFramesCollectorBase const&) = delete;

    virtual void OnDeadlock();

    void RequestAbortCurrentCollection();

    template <class TRawSample>
    void PrepareForNextCollection(TRawSample* rawSample);

    bool SuspendTargetThread(ManagedThreadInfo* pThreadInfo, bool* pIsTargetThreadSuspended);
    void ResumeTargetThreadIfRequired(ManagedThreadInfo* pThreadInfo, bool isTargetThreadSuspended, uint32_t* pErrorCodeHR);
    StackSnapshotResultBuffer* CollectStackSample(ManagedThreadInfo* pThreadInfo, uint32_t* pHR);

protected:
    ManagedThreadInfo* _pCurrentCollectionThreadInfo;

private:
    std::unique_ptr<StackSnapshotResultBuffer> _pStackSnapshotResult;
    std::atomic<bool> _isCurrentCollectionAbortRequested;
    std::condition_variable _collectionAbortPerformedSignal;
    std::mutex _collectionAbortNotificationLock;
    bool _isRequestedCollectionAbortSuccessful;

    // CI Visibility support
    bool _isCIVisibilityEnabled;
    uint64_t _ciVisibilitySpanId;
};

template <class TRawSample>
void StackFramesCollectorBase::PrepareForNextCollection(TRawSample* rawSample)
{
    // We cannot allocate memory once a thread is suspended.
    // This is because malloc() uses a lock and so if we suspend a thread that was allocating, we will deadlock.
    // So we pre-allocate the memory buffer and reset it before suspending the target thread.
    _pStackSnapshotResult->Reset();
    _pStackSnapshotResult->Initialize(rawSample);

    // Clear the current collection thread pointer:
    _pCurrentCollectionThreadInfo = nullptr;

    // Clean up initialization state:
    _isCurrentCollectionAbortRequested.store(false);
    _isRequestedCollectionAbortSuccessful = false;

    // Subclasses can implement their own specific initialization before each collection. Invoke it:
    PrepareForNextCollectionImplementation();
}