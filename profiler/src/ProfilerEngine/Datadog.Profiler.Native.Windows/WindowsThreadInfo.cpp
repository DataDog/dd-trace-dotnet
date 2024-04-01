// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "WindowsThreadInfo.h"

#include "OsSpecificApi.h"

WindowsThreadInfo::WindowsThreadInfo(DWORD threadId, ScopedHandle handle, shared::WSTRING name) :
    _handle{std::move(handle)},
    _threadId{threadId},
    _name{std::move(name)}
{
}

DWORD WindowsThreadInfo::GetOsThreadId() const
{
    return _threadId;
}

shared::WSTRING const& WindowsThreadInfo::GetThreadName() const
{
    return _name;
}

HANDLE WindowsThreadInfo::GetOsThreadHandle() const
{
    return _handle;
}

std::string WindowsThreadInfo::GetProfileThreadId()
{
    assert(false); // not supposed to be called on Windows
    return "??ProfilerThreadId??";
}

std::string WindowsThreadInfo::GetProfileThreadName()
{
    assert(false); // not supposed to be called on Windows
    return "??ProfilerThreadName??";
}
