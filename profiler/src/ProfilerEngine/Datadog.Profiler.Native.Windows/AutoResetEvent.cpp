// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2024 Datadog, Inc.

#include "AutoResetEvent.h"

#include <windows.h>
#include <cassert>
#include "Log.h"

using namespace std::chrono_literals;

struct AutoResetEvent::EventImpl
{
public:
    HANDLE _eventHandle;
};

AutoResetEvent::AutoResetEvent(bool initialState)
    : _impl{std::make_unique<EventImpl>()}
{
    // CreateEvent parameters:
    // - lpEventAttributes: NULL (default security)
    // - bManualReset: FALSE (auto-reset event)
    // - bInitialState: initialState
    // - lpName: NULL (unnamed event)
    _impl->_eventHandle = CreateEvent(
        NULL,           // Default security attributes
        FALSE,          // Auto-reset event
        initialState,   // Initial state
        NULL);          // Unnamed event

    if (_impl->_eventHandle == NULL)
    {
        Log::Error("AutoResetEvent: Failed to create event. Error: ", GetLastError());
    }
}

AutoResetEvent::~AutoResetEvent()
{
    if (_impl->_eventHandle != NULL)
    {
        CloseHandle(_impl->_eventHandle);
        _impl->_eventHandle = NULL;
    }
}

void AutoResetEvent::Set()
{
    if (_impl->_eventHandle != NULL)
    {
        SetEvent(_impl->_eventHandle);
    }
}

bool AutoResetEvent::Wait(std::chrono::milliseconds timeout)
{
    if (_impl->_eventHandle == NULL)
    {
        return false;
    }

    DWORD waitTimeMs;
    
    if (timeout == InfiniteTimeout || timeout < 0ms)
    {
        // Infinite wait
        waitTimeMs = INFINITE;
    }
    else
    {
        // Convert milliseconds to DWORD
        // Note: WaitForSingleObject expects DWORD (32-bit unsigned)
        // If timeout is very large, cap it at maximum DWORD value
        auto timeoutCount = timeout.count();
        if (timeoutCount > INFINITE - 1)
        {
            waitTimeMs = INFINITE - 1;  // Maximum finite timeout
        }
        else
        {
            waitTimeMs = static_cast<DWORD>(timeoutCount);
        }
    }

    DWORD result = WaitForSingleObject(_impl->_eventHandle, waitTimeMs);

    switch (result)
    {
        case WAIT_OBJECT_0:
            return true;

        case WAIT_TIMEOUT:
            // Timeout occurred
            return false;

        case WAIT_FAILED:
            LogOnce(Debug, "AutoResetEvent::Wait: WaitForSingleObject failed. Error: ", GetLastError());
            return false;

        default:
            LogOnce(Debug, "AutoResetEvent::Wait: Unexpected result: ", result);
            return false;
    }
}

