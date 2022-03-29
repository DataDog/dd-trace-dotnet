// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ThreadCpuInfo.h"

ThreadCpuInfo::ThreadCpuInfo(DWORD threadOSId) :
    _threadOSId(threadOSId),
    _pName(nullptr)
{
}

void ThreadCpuInfo::SetName(const WCHAR* pName)
{
    auto newName = std::make_unique<shared::WSTRING>(pName);
    _pName.swap(newName);
}

const shared::WSTRING* ThreadCpuInfo::GetName()
{
    return _pName.get();
}
