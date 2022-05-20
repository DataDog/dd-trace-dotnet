#pragma once

#if WIN32
#include <windows.h>
#endif

#include <functional>
#include <thread>
#include <mutex>
#include <condition_variable>

class Timer {
public:
    Timer(std::function<void()> callback, unsigned long periodMs);

    ~Timer();
    Timer(const Timer&) = delete;
    Timer& operator=(const Timer&) = delete;

    void Start();

private:
    std::function<void()> _callback;
    unsigned long _periodMs;

#ifdef WIN32
    PTP_TIMER _internalTimer;

    static void NTAPI OnTick(
            PTP_CALLBACK_INSTANCE Instance,
            PVOID                 Context,
            PTP_TIMER             Timer);
#else
    std::thread _thread;
    std::mutex _exitMutex;
    std::condition_variable _exit;

    void ThreadProc();

#endif
};

