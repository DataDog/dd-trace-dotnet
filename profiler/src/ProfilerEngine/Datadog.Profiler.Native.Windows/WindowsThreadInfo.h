// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IThreadInfo.h"
#include "ScopedHandle.h"

class WindowsThreadInfo : public IThreadInfo
{
public:
    WindowsThreadInfo(DWORD threadId, ScopedHandle handle, shared::WSTRING name);
    DWORD GetOsThreadId() const override;
    shared::WSTRING const& GetThreadName() const override;
    HANDLE GetOsThreadHandle() const override;
    std::string GetProfileThreadId() override;
    std::string GetProfileThreadName() override;

private:
    ScopedHandle _handle;
    DWORD _threadId;
    shared::WSTRING _name;
};
