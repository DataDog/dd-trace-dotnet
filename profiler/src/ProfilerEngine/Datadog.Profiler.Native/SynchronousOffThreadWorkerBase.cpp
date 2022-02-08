// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <memory>
#include <mutex>
#include <stdexcept>

#include "HResultConverter.h"
#include "Log.h"
#include "OpSysTools.h"
#include "PInvoke.h"
#include "ScopeFinalizer.h"
#include "SynchronousOffThreadWorkerBase.h"
#include "ThreadsCpuManager.h"

SynchronousOffThreadWorkerBase::SynchronousOffThreadWorkerBase() :
    _pWorkerThread{nullptr},
    _mustAbort{false},
    _state{WorkerState::ReadyForWork},
    _pCurrentWorkItemParameters{nullptr},
    _pCurrentWorkItemResults{nullptr},
    _pManagedThreadNameToSet{nullptr},
    _pNativeThreadNameToSet{nullptr}
{
}

void SynchronousOffThreadWorkerBase::Start()
{
    _pWorkerThread = std::make_unique<std::thread>(&SynchronousOffThreadWorkerBase::MainWorkerLoop, this);
}

SynchronousOffThreadWorkerBase::~SynchronousOffThreadWorkerBase()
{
    JoinAndDeleteWorkerThread();
}

void SynchronousOffThreadWorkerBase::JoinAndDeleteWorkerThread(void)
{
    {
        std::lock_guard<std::mutex> lock(_syncLock);

        _mustAbort = true;
        _coordinator.notify_all();
    }

    std::unique_ptr<std::thread>& pWorkerThread = _pWorkerThread;
    if (pWorkerThread != nullptr)
    {
        pWorkerThread->join();
    }
}

bool SynchronousOffThreadWorkerBase::ExecuteWorkItem(void* pParameters, void* pResults)
{
    assert(_pWorkerThread != nullptr);

    if (_mustAbort)
    {
        return false;
    }

    {
        std::unique_lock<std::mutex> lock(_syncLock);

        if (_mustAbort)
        {
            return false;
        }

        _coordinator.wait(lock, [this] { return (_state == WorkerState::ReadyForWork); });

        _pCurrentWorkItemParameters = pParameters;
        _pCurrentWorkItemResults = pResults;
        _state = WorkerState::WorkInProgress;
        _coordinator.notify_all();
    }

    std::this_thread::yield();

    {
        std::unique_lock<std::mutex> lock(_syncLock);

        if (_mustAbort)
        {
            return false;
        }

        _coordinator.wait(lock, [this] { return (_state == WorkerState::WorkResultAvailable); });

        _pCurrentWorkItemParameters = nullptr;
        _pCurrentWorkItemResults = nullptr;
        _state = WorkerState::ReadyForWork;
        _coordinator.notify_all();
    }

    return true;
}

void SynchronousOffThreadWorkerBase::MainWorkerLoop(void)
{
    // Initial setup:

    // Check to see if we need to call InitializeCurrentThread() on a ICorProfilerInfoXyz instance, and do it if required.
    ICorProfilerInfo4* pCorProfilerInfo;
    if (ShouldInitializeCurrentThreadforManagedInteractions(&pCorProfilerInfo))
    {
        if (pCorProfilerInfo == nullptr)
        {
            Log::Debug("ShouldInitializeCurrentThreadforManagedInteractions(..) returned true, but provided a null pCorProfilerInfo.");
            return;
        }

        HRESULT hrICT = pCorProfilerInfo->InitializeCurrentThread();
        if (FAILED(hrICT))
        {
            Log::Error("SynchronousOffThreadWorkerBase::MainWorkerLoop:"
                       " Call to ICorProfilerInfo4::InitializeCurrentThread() did not complete successfully. Error: ",
                       HResultConverter::ToStringWithCode(hrICT), ".");
        }
    }

    {
        // Check to see if we need to set a managed thread name for this thread.
        // If yes, we will store the thread name
        // and then we will try setting it in the main loop until we discover a reverse-PInvoke callback for it.

        const char* pManagedThreadName;
        const WCHAR* pNativeThreadName;

        if (ShouldSetManagedThreadName(&pManagedThreadName))
        {
            if (pManagedThreadName == nullptr)
            {
                Log::Debug("ShouldSetManagedThreadName(..) returned true, but provided a null pManagedThreadName.");
            }

            _pManagedThreadNameToSet = pManagedThreadName;
        }

        if (ShouldSetNativeThreadName(&pNativeThreadName))
        {
            if (pNativeThreadName == nullptr)
            {
                Log::Debug("ShouldSetNativeThreadName(..) returned true, but provided a null pNativeThreadName.");
            }

            _pNativeThreadNameToSet = pNativeThreadName;
        }

        if (_pNativeThreadNameToSet == nullptr)
        {
            ThreadsCpuManager::GetSingletonInstance()->Map((DWORD)OpSysTools::GetThreadId(), WStr("SynchronousOffThreadWorkerBase::MainWorkerLoop"));
        }
        else
        {
            ThreadsCpuManager::GetSingletonInstance()->Map((DWORD)OpSysTools::GetThreadId(), _pNativeThreadNameToSet);
        }
    }

    // Mail loop:

    while (!_mustAbort)
    {
        try
        {
            TrySetThreadNamesIfRequired();

            std::unique_lock<std::mutex> lock(_syncLock);
            _coordinator.wait(lock, [this] { return _mustAbort || (_state == WorkerState::WorkInProgress); });

            if (_mustAbort && _state != WorkerState::WorkInProgress)
            {
                _coordinator.notify_all();
                return;
            }

            auto scopeFinalizer = CreateScopeFinalizer(
                [this] {
                    _state = WorkerState::WorkResultAvailable;
                    _coordinator.notify_all();
                });

            PerformWork(_pCurrentWorkItemParameters, _pCurrentWorkItemResults);
        }
        catch (const std::runtime_error& re)
        {
            Log::Error("Runtime error in SynchronousOffThreadWorkerBase::MainWorkerLoop: ", re.what());
        }
        catch (const std::exception& ex)
        {
            Log::Error("Typed Exception in SynchronousOffThreadWorkerBase::MainWorkerLoop: ", ex.what());
        }
        catch (...)
        {
            Log::Error("Unknown Exception in SynchronousOffThreadWorkerBase::MainWorkerLoop.");
        }
    }
}

void SynchronousOffThreadWorkerBase::TrySetThreadNamesIfRequired(void)
{
    if (_pNativeThreadNameToSet != nullptr)
    {
        if (OpSysTools::SetNativeThreadName(_pWorkerThread.get(), _pNativeThreadNameToSet))
        {
            _pNativeThreadNameToSet = nullptr;
        }
    }

    if (_pManagedThreadNameToSet != nullptr)
    {
        HRESULT hr;
        if (ManagedCallbackRegistry::SetCurrentManagedThreadName::TryInvoke(_pManagedThreadNameToSet, &hr))
        {
            if (SUCCEEDED(hr))
            {
                _pManagedThreadNameToSet = nullptr;
            }
        }
    }
}
