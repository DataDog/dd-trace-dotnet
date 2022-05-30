// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#ifdef _WINDOWS
#include <Windows.h>
#else
#include <future>
#endif

#include <functional>
#include <thread>
#include <mutex>

class Timer {
public:
    Timer(std::function<void()> callback, std::chrono::milliseconds period);
    
    ~Timer();
    Timer(const Timer&) = delete;
    Timer& operator=(const Timer&) = delete;

    void Start();

private:
    std::function<void()> _callback;
    std::chrono::milliseconds _period;

#ifdef _WINDOWS
    PTP_TIMER _internalTimer;

    static void NTAPI OnTick(
            PTP_CALLBACK_INSTANCE Instance,
            PVOID                 Context,
            PTP_TIMER             Timer);
#else
    std::thread _thread;
    std::promise<void> _exitPromise;

    void ThreadProc();
#endif
};

