// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"

#include <utility>

class ScopedHandle
{
public:
    explicit ScopedHandle(HANDLE hnd) :
        _handle(hnd)
    {
    }

    ~ScopedHandle()
    {
#ifdef _WINDOWS
        if (IsValid())
        {
            ::CloseHandle(_handle);
        }
#endif
    }

    // Make it non copyable
    ScopedHandle(ScopedHandle&) = delete;
    ScopedHandle& operator=(ScopedHandle&) = delete;

    ScopedHandle(ScopedHandle&& other) noexcept
    {
        // set the other handle to NULL and store its value in _handle
        _handle = std::exchange(other._handle, static_cast<HANDLE>(NULL));
    }

    ScopedHandle& operator=(ScopedHandle&& other) noexcept
    {
        if (this != &other)
        {
            // set the other handle to NULL and store its value in _handle
            _handle = std::exchange(other._handle, static_cast<HANDLE>(NULL));
        }
        return *this;
    }

    operator HANDLE() const
    {
        return _handle;
    }

    bool IsValid()
    {
        return _handle != INVALID_HANDLE_VALUE && _handle != NULL;
    }

private:
    HANDLE _handle;
};
