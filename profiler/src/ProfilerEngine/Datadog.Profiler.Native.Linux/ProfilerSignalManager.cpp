// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ProfilerSignalManager.h"

#include <signal.h>
#include <stdexcept>
#include <sys/syscall.h>

#include "Log.h"
#include "OpSysTools.h"

ProfilerSignalManager::ProfilerSignalManager() noexcept :
    _canReplaceSignalHandler{true},
    _signalToSend{-1},
    _handler{nullptr},
    _processId{OpSysTools::GetProcId()},
    _isHandlerInPlace{false},
    _previousAction{},
    _handlerRegisterMutex{}
{
}

ProfilerSignalManager::~ProfilerSignalManager() noexcept
{
    if (_isHandlerInPlace)
    {
        _isHandlerInPlace = false;
        sigaction(_signalToSend, &_previousAction, nullptr);
    }
    _handler = nullptr;
}

ProfilerSignalManager* ProfilerSignalManager::Get(int signal)
{
    static ProfilerSignalManager signalManagers[31]; // only for the standard signals

    if (signal < 1 || signal > 31)
    {
        Log::Info("Signal argument is invalid (", std::to_string(signal), " aka ", strsignal(signal), "). Value must be: 1 <= signal <= 31");
        return nullptr;
    }

    auto* manager = &signalManagers[signal - 1]; // 0-based array
    manager->SetSignal(signal);

    return manager;
}

bool ProfilerSignalManager::RegisterHandler(HandlerFn_t handler)
{
    HandlerFn_t current = _handler;

    if (current != nullptr)
    {
        assert(current == handler);
        return current == handler;
    }
    std::unique_lock<std::mutex> lock(_handlerRegisterMutex);

    if (current != nullptr)
    {
        assert(current == handler);
        return current == handler;
    }

    _isHandlerInPlace = SetupSignalHandler();

    if (_isHandlerInPlace)
    {
        _handler = handler;
    }

    return _isHandlerInPlace;
}

bool ProfilerSignalManager::UnRegisterHandler()
{
    if (!IsProfilerSignalHandlerInstalled())
    {
        return false;
    }

    int32_t result = sigaction(_signalToSend, &_previousAction, nullptr);
    if (result != 0)
    {
        Log::Error("ProfilerSignalManager::UnRegisterHandler: Failed to un-register signal handler for ", _signalToSend, " signals. Reason: ",
                   strerror(errno), ".");
        return false;
    }

    _isHandlerInPlace = false;
    return true;
}

void ProfilerSignalManager::SetSignal(int32_t signal)
{
    _signalToSend = signal;
}

std::int32_t ProfilerSignalManager::SendSignal(pid_t threadId)
{
#ifndef NDEBUG
    Log::Debug("ProfilerSignalManager::CollectStackSampleImplementation: Sending signal ",
               _signalToSend, " to thread with threadId=", threadId, ".");
#endif

    return syscall(SYS_tgkill, _processId, threadId, _signalToSend);
}

bool ProfilerSignalManager::CheckSignalHandler()
{
    if (!_canReplaceSignalHandler)
    {
        _isHandlerInPlace = false;
        LogOnce(Warn, "Profiler signal handler was replaced again. As of now, it will not be restored: the profiler is disabled.");
        return false;
    }

    if (IsProfilerSignalHandlerInstalled())
    {
        return true;
    }

    Log::Info("Profiler signal handler has been replaced. Restoring it.");

    // restore profiler handler
    _isHandlerInPlace = SetupSignalHandler();

    if (!_isHandlerInPlace)
    {
        Log::Warn("Fail to restore profiler signal handler.");
    }

    _canReplaceSignalHandler = false;
    return _isHandlerInPlace;
}

bool ProfilerSignalManager::IsProfilerSignalHandlerInstalled()
{
    struct sigaction currentAction;

    sigaction(_signalToSend, nullptr, &currentAction);

    return (currentAction.sa_flags & SA_SIGINFO) == SA_SIGINFO &&
           currentAction.sa_sigaction == ProfilerSignalManager::SignalHandler;
}

bool ProfilerSignalManager::IsHandlerInPlace() const
{
    return _isHandlerInPlace;
}

bool ProfilerSignalManager::SetupSignalHandler()
{
    struct sigaction sampleAction;
    sampleAction.sa_flags = SA_RESTART | SA_SIGINFO;
    sampleAction.sa_sigaction = ProfilerSignalManager::SignalHandler;
    sigemptyset(&sampleAction.sa_mask);
    sigaddset(&sampleAction.sa_mask, _signalToSend);

    int32_t result = sigaction(_signalToSend, &sampleAction, &_previousAction);
    if (result != 0)
    {
        Log::Error("ProfilerSignalManager::SetupSignalHandler: Failed to setup signal handler for ", strsignal(_signalToSend), " signals. Reason: ",
                   strerror(errno), ".");
        return false;
    }

    Log::Info("ProfilerSignalManager::SetupSignalHandler: Successfully setup signal handler for ", strsignal(_signalToSend), " signal.");
    return true;
}

void ProfilerSignalManager::SignalHandler(int signal, siginfo_t* info, void* context)
{
    auto* signalManager = Get(signal);

    if (signalManager == nullptr) [[unlikely]]
    {
        return;
    }

    if (!signalManager->CallCustomHandler(signal, info, context))
    {
        signalManager->CallOrignalHandler(signal, info, context);
    }
}

bool ProfilerSignalManager::CallCustomHandler(int32_t signal, siginfo_t* info, void* context)
{
    HandlerFn_t handler = _handler;
    return handler != nullptr && handler(signal, info, context);
}

void ProfilerSignalManager::CallOrignalHandler(int32_t signal, siginfo_t* info, void* context)
{
    // This thread local variable helps in detecting the case where the profiler handler and
    // the previous handler are referencing/calling each other.
    // As it's synchchronous, we can check if we already executed the previous handler and
    // stop if it's the case.
    static thread_local bool isExecuting = false;

    if (isExecuting)
        return;

    isExecuting = true;

    try
    {
        if ((_previousAction.sa_flags & SA_SIGINFO) == SA_SIGINFO && _previousAction.sa_sigaction != nullptr)
        {
            _previousAction.sa_sigaction(signal, info, context);
        }
        else
        {
            if (_previousAction.sa_handler != SIG_DFL && _previousAction.sa_handler != SIG_IGN)
            {
                _previousAction.sa_handler(signal);
            }
        }
    }
    catch (...)
    {
    }

    isExecuting = false;
}

int32_t ProfilerSignalManager::GetSignal() const
{
    return _signalToSend;
}