// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#ifdef _WINDOWS

#include "Timer.h"

Timer::Timer(std::function<void()> callback, std::chrono::milliseconds period) :
        _callback(std::move(callback)),
        _period(period),
        _internalTimer(nullptr)
{
}

Timer::~Timer()
{
    if (_internalTimer != nullptr)
    {
        // First, cancel the timer to make sure no callback is executing
        // https://docs.microsoft.com/en-us/windows/win32/api/threadpoolapiset/nf-threadpoolapiset-closethreadpooltimer#remarks
        SetThreadpoolTimer(_internalTimer, nullptr, 0, 0);
        WaitForThreadpoolTimerCallbacks(_internalTimer, true);

        CloseThreadpoolTimer(_internalTimer);

        _internalTimer = nullptr;
    }
}

void Timer::Start()
{
    _internalTimer = CreateThreadpoolTimer(&OnTick, &_callback, nullptr);

    ULARGE_INTEGER rawDueTime;
    rawDueTime.QuadPart = _period.count() * -1 * 10 /* microseconds */ * 1000 /* milliseconds */;

    FILETIME dueTime;
    dueTime.dwHighDateTime = rawDueTime.HighPart;
    dueTime.dwLowDateTime = rawDueTime.LowPart;

    SetThreadpoolTimer(_internalTimer, &dueTime, _period.count(), 100);
}

void NTAPI Timer::OnTick(
        PTP_CALLBACK_INSTANCE Instance,
        PVOID                 Context,
        PTP_TIMER             Timer)
{
    const auto callback = static_cast<std::function<void()>*>(Context);
    (*callback)();
}

#endif