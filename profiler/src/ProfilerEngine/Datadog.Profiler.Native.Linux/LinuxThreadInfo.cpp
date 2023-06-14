// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LinuxThreadInfo.h"

LinuxThreadInfo::LinuxThreadInfo(DWORD threadId, shared::WSTRING name) :
    _threadId{threadId},
    _name{std::move(name)}
{
}

DWORD LinuxThreadInfo::GetOsThreadId() const
{
    return _threadId;
}

shared::WSTRING const& LinuxThreadInfo::GetThreadName() const
{
    return _name;
}

HANDLE LinuxThreadInfo::GetOsThreadHandle() const
{
    return {};
}