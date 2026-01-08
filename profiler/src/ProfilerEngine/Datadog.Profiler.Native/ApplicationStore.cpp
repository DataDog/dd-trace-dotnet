// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ApplicationStore.h"

#include "ApplicationInfo.h"
#include "IConfiguration.h"
#include "IRuntimeInfo.h"
#include "ISsiManager.h"
#include "Log.h"
#include "ProfileExporter.h"

ApplicationStore::ApplicationStore(IConfiguration* configuration, IRuntimeInfo* runtimeInfo) :
    _pConfiguration{configuration},
    _pRuntimeInfo{runtimeInfo}
{
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

        Log::Debug("Creating new application info for runtimeId: ", runtimeId, ", serviceName: ", info.ServiceName, ", environment: ", info.Environment, ", version: ", info.Version);

        _infos[runtimeId] = info;
        return info;
    }
}
void ApplicationStore::SetApplicationInfo(const std::string& runtimeId, const std::string& serviceName, const std::string& environment, const std::string& version, const std::string& processTags)
{
    Log::Debug("Setting application info for runtimeId: ", runtimeId, ", serviceName: ", serviceName, ", environment: ", environment, ", version: ", version);

    std::lock_guard lock(_infosLock);
    auto& info = _infos[runtimeId];
    info.ServiceName = serviceName;
    info.Environment = environment;
    info.Version = version;

    // do not overwrite previously set value if the parameter is the default empty string
    if (info.ProcessTags.empty() || !processTags.empty())
    {
        info.ProcessTags = processTags;
    }

    info.RepositoryUrl = _pConfiguration->GetGitRepositoryUrl();
    info.CommitSha = _pConfiguration->GetGitCommitSha();
}

void ApplicationStore::SetGitMetadata(std::string runtimeId, std::string repositoryUrl, std::string commitSha)
{
    std::lock_guard lock(_infosLock);
    auto& info = _infos[std::move(runtimeId)];
    info.RepositoryUrl = std::move(repositoryUrl);
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
