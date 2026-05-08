// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "IApplicationStore.h"
#include "ApplicationInfo.h"
#include "ServiceBase.h"

#include <memory>
#include <mutex>
#include <unordered_map>

// forward declarations
class IConfiguration;
class IRuntimeInfo;


/// <summary>
/// Stores the application information (name, environment, version) per runtime id
/// </summary>
class ApplicationStore
    :
    public IApplicationStore,
    public ServiceBase
{
public:
    ApplicationStore(IConfiguration* configuration, IRuntimeInfo* runtimeInfo);
    ~ApplicationStore();

    ApplicationInfo GetApplicationInfo(const std::string& runtimeId) override;
    void SetApplicationInfo(const std::string& runtimeId, const std::string& serviceName, const std::string& environment, const std::string& version, const std::string& processTags) override;
    void SetGitMetadata(std::string runtimeId, std::string repositoryUrl, std::string commitSha) override;

    const char* GetName() override;

    // Memory measurement (IMemoryFootprintProvider)
    size_t GetMemorySize() const override;
    void LogMemoryBreakdown() const override;

private:
    struct MemoryStats
    {
        size_t baseSize;
        size_t mapSize;
        size_t entryCount;
        size_t mapBuckets;
        size_t keysSize;
        size_t appInfosSize;

        size_t GetTotal() const
        {
            return baseSize + mapSize + keysSize + appInfosSize;
        }
    };

    MemoryStats ComputeMemoryStats() const;

private:
    const char* _serviceName = "ApplicationStore";

    bool StartImpl() override;
    bool StopImpl() override;

    IConfiguration* const _pConfiguration;
    IRuntimeInfo* _pRuntimeInfo;
    std::unordered_map<std::string, ApplicationInfo> _infos;
    // mutable to allow locking in const methods (e.g., GetMemorySize, LogMemoryBreakdown)
    mutable std::mutex _infosLock;
};
