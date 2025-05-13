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
class ISsiManager;
class IRuntimeInfo;

namespace libatadog
{
class TelemetryMetricsWorker;
}

/// <summary>
/// Stores the application information (name, environment, version) per runtime id
/// </summary>
class ApplicationStore
    :
    public IApplicationStore,
    public ServiceBase
{
public:
    ApplicationStore(IConfiguration* configuration, IRuntimeInfo* runtimeInfo, ISsiManager* ssiManager);
    ~ApplicationStore();

    ApplicationInfo GetApplicationInfo(const std::string& runtimeId) override;
    void SetApplicationInfo(const std::string& runtimeId, const std::string& serviceName, const std::string& environment, const std::string& version) override;
    void SetGitMetadata(std::string runtimeId, std::string repositoryUrl, std::string commitSha) override;

    const char* GetName() override;

private:
    const char* _serviceName = "ApplicationStore";

    bool StartImpl() override;
    bool StopImpl() override;

    void InitializeTelemetryMetricsWorker(std::string const& runtimeId, ApplicationInfo& info);

    IConfiguration* const _pConfiguration;
    ISsiManager* _pSsiManager;
    IRuntimeInfo* _pRuntimeInfo;
    std::unordered_map<std::string, ApplicationInfo> _infos;
    std::mutex _infosLock;
    bool _isSsiTelemetryEnabled;
};
