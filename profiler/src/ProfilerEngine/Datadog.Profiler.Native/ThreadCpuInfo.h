// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "shared/src/native-src/string.h"
#include <memory>

class ThreadCpuInfo
{
private:
    ThreadCpuInfo() = delete;

public:
    explicit ThreadCpuInfo(DWORD threadOSId);

public:
    void SetName(const WCHAR* pName);
    const shared::WSTRING* GetName();

private:
    DWORD _threadOSId;
    std::unique_ptr<shared::WSTRING> _pName;
    // FAR: in case of thread end/restart such as what happens
    //      in case of deadlock detection, the previous CPU usage
    //      should be stored. It would also mean that several
    //      ThreadCpuInfo will have the same name
};
