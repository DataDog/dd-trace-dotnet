#include "LinuxThreadInfo.h"

LinuxThreadInfo::LinuxThreadInfo(DWORD threadId, shared::WSTRING name) :
    _threadId{threadId},
    _name{name}
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