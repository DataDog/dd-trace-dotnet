#pragma once

#include "IThreadInfo.h"

#include "shared/src/native-src/string.h"

class LinuxThreadInfo : public IThreadInfo
{
public:
    LinuxThreadInfo(DWORD threadId, shared::WSTRING name);
    DWORD GetOsThreadId() const override;
    shared::WSTRING const& GetThreadName() const override;
    HANDLE GetOsThreadHandle() const override;

private:
    DWORD _threadId;
    shared::WSTRING _name;
};
