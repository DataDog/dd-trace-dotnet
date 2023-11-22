#include "FrameworkThreadInfo.h"

FrameworkThreadInfo::FrameworkThreadInfo(DWORD threadId)
    : _osThreadId{threadId}
{
}

const shared::WSTRING _emptyWString{};

DWORD FrameworkThreadInfo::GetOsThreadId() const
{
    return _osThreadId;
}

shared::WSTRING const& FrameworkThreadInfo::GetThreadName() const
{
    return _emptyWString;
}

HANDLE FrameworkThreadInfo::GetOsThreadHandle() const
{
    return HANDLE();
}

std::string FrameworkThreadInfo::GetProfileThreadId()
{
    std::stringstream buffer;
    buffer << "<0> [#" << _osThreadId << "]";
    return buffer.str();
}

std::string FrameworkThreadInfo::GetProfileThreadName()
{
    std::stringstream buffer;
    buffer << "Managed thread (name unknown) [#" << _osThreadId << "]";
    return buffer.str();
}
