// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#ifndef _WINDOWS

#include "Timer.h"

#include <thread>
#include <functional>


Timer::Timer(std::function<void()> callback, std::chrono::milliseconds period) :
    _callback(std::move(callback)),
    _period(period),
    _thread(),
    _exitMutex(),
    _exit()
{
}

Timer::~Timer()
{
    if (_thread.get_id() != std::thread::id() && _thread.joinable())
    {
        _exit.notify_all();
        _thread.join();
    }
}

void Timer::Start()
{
    _thread = std::thread(&Timer::ThreadProc, this);
}

void Timer::ThreadProc()
{
    std::unique_lock<std::mutex> lock(_exitMutex);

    while (_exit.wait_for(lock, _period) == std::cv_status::timeout)
    {
        _callback();
    }
}

#endif