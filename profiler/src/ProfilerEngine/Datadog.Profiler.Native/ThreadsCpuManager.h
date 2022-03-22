// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <memory>
#include <mutex>
#include <string>
#include <unordered_map>

#include "ThreadCpuInfo.h"
#include "IThreadsCpuManager.h"

class ThreadsCpuManager : public IThreadsCpuManager
{
public:
    ThreadsCpuManager();

public:
    ~ThreadsCpuManager() override;

// interfaces implementation
public:
    const char* GetName() override;
    bool Start() override;
    bool Stop() override;
    void Map(DWORD threadOSId, const WCHAR* name) override;
    void LogCpuTimes() override;

private:
    const char* _serviceName = "ThreadsCpuManager";

    // Need to protect access to the map. However, it should not trigger a lot of contention
    // because mostly needed when naming threads and logging CPU usage
    std::recursive_mutex _lockThreads;

    // map thread OS id to ThreadCpuInfo that stores name
    std::unordered_map<DWORD, ThreadCpuInfo*> _threads;
};
