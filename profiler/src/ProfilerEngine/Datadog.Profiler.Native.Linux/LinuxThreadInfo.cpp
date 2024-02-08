// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LinuxThreadInfo.h"
#include "shared/src/native-src/string.h"

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

std::string LinuxThreadInfo::GetProfileThreadId()
{
    std::stringstream buffer;
    buffer << "<0> [#" << _threadId << "]";
    return buffer.str();
}

std::string LinuxThreadInfo::GetProfileThreadName()
{
    shared::WSTRINGSTREAM wbuffer;
    wbuffer << _name << WStr("[#") << _threadId << WStr("]");
    return shared::ToString(wbuffer.str());
}
