// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "IApplicationStore.h"
#include "ApplicationInfo.h"

#include <mutex>
#include <unordered_map>

// forward declarations
class IConfiguration;

/// <summary>
/// Stores the application information (name, environment, version) per runtime id
/// </summary>
class ApplicationStore : public IApplicationStore
{
public:
    ApplicationStore(IConfiguration* configuration);

    ApplicationInfo GetApplicationInfo(const std::string& runtimeId) override;
    void SetApplicationInfo(const std::string& runtimeId, const std::string& serviceName, const std::string& environment, const std::string& version) override;
    void SetGitMetadata(std::string runtimeId, std::string repositoryUrl, std::string commitSha) override;

    const char* GetName() override;
    bool Start() override;
    bool Stop() override;

private:
    const char* _serviceName = "ApplicationStore";

    IConfiguration* const _pConfiguration;
    std::unordered_map<std::string, ApplicationInfo> _infos;
    std::mutex _infosLock;
};
