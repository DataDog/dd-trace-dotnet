#pragma once

#include "timer.h"

#include <thread>
#include <functional>


Timer::Timer(std::function<void()> callback, unsigned long periodMs) :
    _callback(std::move(callback)),
    _periodMs(periodMs),
    _thread(),
    _exitMutex(),
    _exit()
{
}

Timer::~Timer()
{
    if (_thread.joinable())
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

    while (_exit.wait_for(lock, std::chrono::milliseconds(_periodMs)) == std::cv_status::timeout)
    {
        _callback();
    }
}