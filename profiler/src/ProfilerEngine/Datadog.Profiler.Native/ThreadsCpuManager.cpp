// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ThreadsCpuManager.h"
#include "shared/src/native-src/string.h"

#include "Log.h"
#include "ThreadCpuInfo.h"

ThreadsCpuManager::ThreadsCpuManager()
{
}

ThreadsCpuManager::~ThreadsCpuManager()
{
    std::lock_guard<std::recursive_mutex> lock(_lockThreads);
    _threads.clear();
}

const char* ThreadsCpuManager::GetName()
{
    return _serviceName;
}

bool ThreadsCpuManager::StartImpl()
{
    // nothing special to start
    return true;
}

bool ThreadsCpuManager::StopImpl()
{
    // nothing special to stop
    return true;
}

void ThreadsCpuManager::Map(DWORD threadOSId, const WCHAR* name)
{
    std::lock_guard<std::recursive_mutex> lock(_lockThreads);

    if (name == nullptr)
    {
        Log::Debug("Map (", threadOSId, ") to null");
    }
    else
    {
        Log::Debug("Map (", threadOSId, ") to ", shared::WSTRING(name));
    }

    // find or create the info corresponding to the given thread ID
    auto& pInfo = _threads[threadOSId];
    if (pInfo == nullptr)
    {
        pInfo = std::make_unique<ThreadCpuInfo>(threadOSId);
    }

    // set its name if any
    if ((name == nullptr) || (WStrLen(name) == 0))
    {
        pInfo->SetName(nullptr);
    }
    else
    {
        pInfo->SetName(name);
    }
}
