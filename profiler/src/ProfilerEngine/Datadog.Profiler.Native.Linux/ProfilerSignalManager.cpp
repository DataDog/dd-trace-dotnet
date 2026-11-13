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
    _handler.store(nullptr, std::memory_order_release);
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
    HandlerFn_t current = _handler.load(std::memory_order_acquire);

    if (current != nullptr)
    {
        assert(current == handler);
        return current == handler;
    }
    std::unique_lock<std::mutex> lock(_handlerRegisterMutex);

    current = _handler.load(std::memory_order_acquire);
    if (current != nullptr)
    {
        assert(current == handler);
        return current == handler;
    }

    _isHandlerInPlace = SetupSignalHandler();

    if (_isHandlerInPlace)
    {
        _handler.store(handler, std::memory_order_release);
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

bool ProfilerSignalManager::IgnoreSignal() {
    struct sigaction sampleAction;
    sampleAction.sa_handler = SIG_IGN;
    sigemptyset(&sampleAction.sa_mask);
    sampleAction.sa_flags = 0;

    int32_t result = sigaction(_signalToSend, &sampleAction, nullptr);
    if (result != 0)
    {
        Log::Error("ProfilerSignalManager::IgnoreSignal: Failed mark ", strsignal(_signalToSend), " as ignored. Reason: ",
                   strerror(errno), ".");
        return false;
    }
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

    return IsProfilerSignalHandler(currentAction);
}

bool ProfilerSignalManager::IsProfilerSignalHandler(const struct sigaction& action)
{
    return (action.sa_flags & SA_SIGINFO) == SA_SIGINFO && action.sa_sigaction == ProfilerSignalManager::SignalHandler;
}

bool ProfilerSignalManager::IsHandlerInPlace() const
{
    return _isHandlerInPlace;
}

bool ProfilerSignalManager::SetupSignalHandler()
{
    struct sigaction currentAction;
    if (sigaction(_signalToSend, nullptr, &currentAction) != 0)
    {
        Log::Error("ProfilerSignalManager::SetupSignalHandler: Failed to get the signal handler for ", strsignal(_signalToSend), " signals. Reason: ",
                   strerror(errno), ".");
        return false;
    }

    // Installed again over itself: it must keep chaining to the action it replaced the first time
    const bool isAlreadyInstalled = IsProfilerSignalHandler(currentAction);
    const struct sigaction& actionToChainTo = isAlreadyInstalled ? _previousAction : currentAction;

    struct sigaction sampleAction;
    sampleAction.sa_flags = SA_RESTART | SA_SIGINFO;
    sampleAction.sa_sigaction = ProfilerSignalManager::SignalHandler;
    sigemptyset(&sampleAction.sa_mask);
    if (_signalToSend == SIGSEGV || _signalToSend == SIGBUS)
    {
        // The previous handler (the CLR one) is called from this one: it must run on the stack and with the signals
        // blocked it was registered with. The CLR SIGSEGV handler runs on the alternate stack, with its suspension signal
        // (SIGRTMIN) blocked. Called from the thread stack, the .NET 6 one executes its worker just below the faulting
        // frame, where our frames and the signal frame are: it overwrites them, and the process crashes when it returns.
        // The CLR SIGBUS handler runs on the thread stack.
        sampleAction.sa_flags |= actionToChainTo.sa_flags & SA_ONSTACK;
        sampleAction.sa_mask = actionToChainTo.sa_mask;
    }
    sigaddset(&sampleAction.sa_mask, _signalToSend);

    struct sigaction replacedAction;
    int32_t result = sigaction(_signalToSend, &sampleAction, &replacedAction);
    if (result != 0)
    {
        Log::Error("ProfilerSignalManager::SetupSignalHandler: Failed to setup signal handler for ", strsignal(_signalToSend), " signals. Reason: ",
                   strerror(errno), ".");
        return false;
    }

    if (!IsProfilerSignalHandler(replacedAction))
    {
        _previousAction = replacedAction;
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
    HandlerFn_t handler = _handler.load(std::memory_order_acquire);
    return handler != nullptr && handler(signal, info, context);
}

void ProfilerSignalManager::CallOrignalHandler(int32_t signal, siginfo_t* info, void* context)
{
    if (_previousAction.sa_handler == SIG_DFL || _previousAction.sa_handler == SIG_IGN || IsProfilerSignalHandler(_previousAction))
    {
        // Nothing to chain to. A fault raised by the CPU happens again when this handler returns: the thread would
        // loop on the faulting instruction (https://github.com/DataDog/dd-trace-dotnet/issues/9563). Like the CLR,
        // put the default action back, so that the fault ends the process as it would without the profiler.
        if ((signal == SIGSEGV || signal == SIGBUS) && info != nullptr && info->si_code > 0)
        {
            struct sigaction defaultAction = {};
            defaultAction.sa_handler = SIG_DFL;
            sigemptyset(&defaultAction.sa_mask);
            sigaction(signal, &defaultAction, nullptr);
        }
        return;
    }

    // For the signals that are sent by the profiler (SIGUSR1, SIGPROF), this thread local variable helps in detecting
    // the case where the profiler handler and the previous handler are referencing/calling each other.
    // As it's synchronous, we can check if we already executed the previous handler and stop if it's the case.
    //
    // It cannot be used for SIGSEGV and SIGBUS: the previous handler (the CLR one) does not return for
    // a hardware exception in managed code (a null reference for example). It leaves the alternate stack and
    // dispatches the managed exception on the faulting thread's stack (see also Datadog.Linux.ApiWrapper/functions_to_wrap.c).
    // The flag would never be cleared: the next fault on this thread would not be chained, and the faulting instruction
    // would be restarted forever (https://github.com/DataDog/dd-trace-dotnet/issues/9563).
    static thread_local bool isExecuting = false;
    const bool useRecursionFlag = signal != SIGSEGV && signal != SIGBUS;

    if (useRecursionFlag)
    {
        if (isExecuting)
            return;

        isExecuting = true;
    }

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

    if (useRecursionFlag)
    {
        isExecuting = false;
    }
}

int32_t ProfilerSignalManager::GetSignal() const
{
    return _signalToSend;
}