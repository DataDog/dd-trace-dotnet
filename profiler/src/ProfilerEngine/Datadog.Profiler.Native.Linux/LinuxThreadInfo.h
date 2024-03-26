// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IThreadInfo.h"

#include "shared/src/native-src/string.h"

class LinuxThreadInfo : public IThreadInfo
{
public:
    LinuxThreadInfo(DWORD threadId, shared::WSTRING name);

    // Inherited via IThreadInfo
    DWORD GetOsThreadId() const override;
    shared::WSTRING const& GetThreadName() const override;
    HANDLE GetOsThreadHandle() const override;
    std::string GetProfileThreadId() override;
    std::string GetProfileThreadName() override;

private:
    DWORD _threadId;
    shared::WSTRING _name;
};
