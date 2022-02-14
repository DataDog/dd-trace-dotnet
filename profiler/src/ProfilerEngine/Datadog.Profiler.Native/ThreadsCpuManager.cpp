// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ThreadsCpuManager.h"
#include "shared/src/native-src/string.h"

#include "Log.h"
#include "ThreadCpuInfo.h"

std::unique_ptr<ThreadsCpuManager> ThreadsCpuManager::s_singleton;

void ThreadsCpuManager::CreateNewSingletonInstance()
{
    auto currentInstance = s_singleton.get();
    if (currentInstance == nullptr)
    {
        s_singleton.reset(new ThreadsCpuManager());
        return;
    }

    throw std::logic_error("Only one ThreadsCpuManager instance can be created.");
}

ThreadsCpuManager* const ThreadsCpuManager::GetSingletonInstance()
{
    auto currentInstance = s_singleton.get();
    if (currentInstance != nullptr)
    {
        return currentInstance;
    }

    throw std::logic_error("Missing call to ThreadsCpuManager::CreateSingleton().");
}

void ThreadsCpuManager::DeleteSingletonInstance()
{
    auto currentInstance = s_singleton.get();
    if (currentInstance != nullptr)
    {
        s_singleton.reset();
        return;
    }

    throw std::logic_error("No ThreadsCpuManager singleton to delete.");
}

ThreadsCpuManager::ThreadsCpuManager()
{
}

ThreadsCpuManager::~ThreadsCpuManager()
{
    std::lock_guard<std::recursive_mutex> lock(_lockThreads);

    for (const std::pair<DWORD, ThreadCpuInfo*>& current : _threads)
    {
        delete current.second;
    }
    _threads.clear();
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
    ThreadCpuInfo* pInfo = nullptr;
    auto element = _threads.find(threadOSId);
    if (element == _threads.end())
    {
        pInfo = new ThreadCpuInfo(threadOSId);
        _threads[threadOSId] = pInfo;
    }
    else
    {
        pInfo = element->second;
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
