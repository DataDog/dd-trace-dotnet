// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <memory>
#include <mutex>
#include <string>
#include <unordered_map>

#include "ThreadCpuInfo.h"
#include "IThreadsCpuManager.h"
#include "ServiceBase.h"

class ThreadsCpuManager
    :
    public IThreadsCpuManager,
    public ServiceBase
{
public:
    ThreadsCpuManager();

public:
    ~ThreadsCpuManager() override;

// interfaces implementation
public:
    const char* GetName() override;
    void Map(DWORD threadOSId, const WCHAR* name) override;
    void LogCpuTimes() override;

    // IMemoryFootprintProvider
    size_t GetMemorySize() const override;
    void LogMemoryBreakdown() const override;

private:
    struct MemoryStats
    {
        size_t baseSize;
        size_t mapSize;
        size_t threadCount;
        size_t mapBuckets;
        size_t threadInfosSize;

        size_t GetTotal() const
        {
            return baseSize + mapSize + threadInfosSize;
        }
    };

    MemoryStats ComputeMemoryStats() const;

private:
    bool StartImpl() override;
    bool StopImpl() override;

    const char* _serviceName = "ThreadsCpuManager";

    // Need to protect access to the map. However, it should not trigger a lot of contention
    // because mostly needed when naming threads and logging CPU usage
    // mutable to allow locking in const methods (e.g., GetMemorySize, LogMemoryBreakdown)
    mutable std::recursive_mutex _lockThreads;

    // map thread OS id to ThreadCpuInfo that stores name
    std::unordered_map<DWORD, std::unique_ptr<ThreadCpuInfo>> _threads;
};
