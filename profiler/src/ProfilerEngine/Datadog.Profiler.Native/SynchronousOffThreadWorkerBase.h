// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <condition_variable>
#include <memory>
#include <mutex>
#include <thread>

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

// forward declarations
class IThreadsCpuManager;


class SynchronousOffThreadWorkerBase
{
private:
    enum class WorkerState
    {
        Unknown = 0,
        ReadyForWork = 1,
        WorkInProgress = 2,
        WorkResultAvailable = 3
    };

public:
    SynchronousOffThreadWorkerBase(IThreadsCpuManager* pThreadsCpuManager);
    virtual ~SynchronousOffThreadWorkerBase();

    void Start();
    bool ExecuteWorkItem(void* pParameters, void* pResults);

    template <typename TParams, typename TResults>
    bool ExecuteWorkItem(TParams* pParameters, TResults* pResults)
    {
        return ExecuteWorkItem(static_cast<void*>(pParameters), static_cast<void*>(pResults));
    }

protected:
    virtual bool ShouldInitializeCurrentThreadforManagedInteractions(ICorProfilerInfo4** ppCorProfilerInfo) = 0;
    virtual bool ShouldSetManagedThreadName(const char** managedThreadName) = 0;
    virtual bool ShouldSetNativeThreadName(const WCHAR** nativeThreadName) = 0;
    virtual void PerformWork(void* pParameters, void* pResults) = 0;

private:
    void MainWorkerLoop(void);
    void JoinAndDeleteWorkerThread(void);
    void TrySetThreadNamesIfRequired(void);

private:
    std::unique_ptr<std::thread> _pWorkerThread;
    std::mutex _syncLock;
    std::condition_variable _coordinator;

    bool _mustAbort;
    WorkerState _state;
    void* _pCurrentWorkItemParameters;
    void* _pCurrentWorkItemResults;

    IThreadsCpuManager* _pThreadsCpuManager;

    const char* _pManagedThreadNameToSet;
    const WCHAR* _pNativeThreadNameToSet;
};