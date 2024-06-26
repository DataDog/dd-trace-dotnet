// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <atomic>
#include <mutex>
#include <signal.h>
#include <sys/types.h>
#include <type_traits>

class ProfilerSignalManager
{
public:
    using HandlerFn_t = std::add_pointer<bool(int, siginfo_t*, void*)>::type;

    static ProfilerSignalManager* Get(int signal);

    bool RegisterHandler(HandlerFn_t handler);
    bool UnRegisterHandler();
    int32_t SendSignal(pid_t threadId);
    bool CheckSignalHandler();
    bool IsHandlerInPlace() const;
    int32_t GetSignal() const;

#ifdef DD_TEST
    void Reset()
    {
        _handler = nullptr;
        sigaction(_signalToSend, &_previousAction, nullptr);
        _isHandlerInPlace = false;
        _canReplaceSignalHandler = true;
    }
#endif

private:
    explicit ProfilerSignalManager() noexcept;

    // prevent copy and move semantics.
    ProfilerSignalManager(ProfilerSignalManager&) noexcept = delete;
    ProfilerSignalManager& operator=(ProfilerSignalManager&) noexcept = delete;
    ProfilerSignalManager(ProfilerSignalManager&& other) noexcept = delete;
    ProfilerSignalManager& operator=(ProfilerSignalManager&& other) noexcept = delete;

    ~ProfilerSignalManager() noexcept;

    bool IsProfilerSignalHandlerInstalled();
    bool SetupSignalHandler();
    bool CallCustomHandler(int32_t signal, siginfo_t* info, void* context);
    void CallOrignalHandler(int32_t signal, siginfo_t* info, void* context);
    void SetSignal(int32_t signal);

    static void SignalHandler(int signal, siginfo_t* info, void* context);

private:
    bool _canReplaceSignalHandler;
    int32_t _signalToSend;
    HandlerFn_t _handler;
    pid_t _processId;
    bool _isHandlerInPlace;
    struct sigaction _previousAction;
    std::mutex _handlerRegisterMutex;
};
