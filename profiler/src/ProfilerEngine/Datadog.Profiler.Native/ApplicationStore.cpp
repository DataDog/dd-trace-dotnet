// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ApplicationStore.h"

#include "ApplicationInfo.h"
#include "IConfiguration.h"
#include "IRuntimeInfo.h"
#include "ISsiManager.h"
#include "ProfileExporter.h"
#include "TelemetryMetricsWorker.h"

ApplicationStore::ApplicationStore(IConfiguration* configuration, IRuntimeInfo* runtimeInfo, ISsiManager* ssiManager) :
    _pConfiguration{configuration},
    _pSsiManager{ssiManager},
    _pRuntimeInfo{runtimeInfo}
{
    // SSI telemetry is enabled if the configuration says so and the profiler has been deployed via SSI
    _isSsiTelemetryEnabled = _pConfiguration->IsSsiTelemetryEnabled() ? (_pSsiManager->GetDeploymentMode() == DeploymentMode::SingleStepInstrumentation) : false;
}

ApplicationStore::~ApplicationStore() = default;

ApplicationInfo ApplicationStore::GetApplicationInfo(const std::string& runtimeId)
{
    {
        std::lock_guard lock(_infosLock);

        const auto info_it = _infos.find(runtimeId);

        if (info_it != _infos.end())
        {
            return info_it->second;
        }

        auto info = ApplicationInfo{
            _pConfiguration->GetServiceName(),
            _pConfiguration->GetEnvironment(),
            _pConfiguration->GetVersion(),
            _pConfiguration->GetGitRepositoryUrl(),
            _pConfiguration->GetGitCommitSha()};

        InitializeTelemetryMetricsWorker(runtimeId, info);

        _infos[runtimeId] = info;
        return info;
    }
}
void ApplicationStore::SetApplicationInfo(const std::string& runtimeId, const std::string& serviceName, const std::string& environment, const std::string& version)
{
    std::lock_guard lock(_infosLock);
    auto& info = _infos[runtimeId];
    info.ServiceName = serviceName;
    info.Environment = environment;
    info.Version = version;
    info.RepositoryUrl = _pConfiguration->GetGitRepositoryUrl();
    info.CommitSha = _pConfiguration->GetGitCommitSha();

    // we have to recreate the telemetry metrics worker
    InitializeTelemetryMetricsWorker(runtimeId, info);
}

void ApplicationStore::SetGitMetadata(std::string runtimeId, std::string respositoryUrl, std::string commitSha)
{
    std::lock_guard lock(_infosLock);
    auto& info = _infos[std::move(runtimeId)];
    info.RepositoryUrl = std::move(respositoryUrl);
    info.CommitSha = std::move(commitSha);
    // no need to create worker, it has already been created
}

const char* ApplicationStore::GetName()
{
    return _serviceName;
}

bool ApplicationStore::StartImpl()
{
    // nothing special to start
    return true;
}

bool ApplicationStore::StopImpl()
{
    // nothing special to stop
    return true;
}

void ApplicationStore::InitializeTelemetryMetricsWorker(std::string const& runtimeId, ApplicationInfo& info)
{
    if (!_isSsiTelemetryEnabled)
    {
        return;
    }

    auto worker = std::make_shared<libdatadog::TelemetryMetricsWorker>(_pSsiManager);
    auto agentUrl = ProfileExporter::BuildAgentEndpoint(_pConfiguration);
    if (worker->Start(
            _pConfiguration,
            info.ServiceName,
            info.Version,
            ProfileExporter::LanguageFamily,
            _pRuntimeInfo->GetClrString(),
            ProfileExporter::LibraryVersion,
            agentUrl,
            runtimeId,
            info.Environment))
    {
        info.Worker = worker;
    }
    else
    {
        info.Worker = nullptr;
    }
}
