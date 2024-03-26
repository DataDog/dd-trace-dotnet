// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ApplicationStore.h"

#include "ApplicationInfo.h"
#include "IConfiguration.h"

ApplicationStore::ApplicationStore(IConfiguration* configuration) :
    _pConfiguration{configuration}
{
}

ApplicationInfo ApplicationStore::GetApplicationInfo(const std::string& runtimeId)
{
    {
        std::lock_guard lock(_infosLock);

        const auto info = _infos.find(runtimeId);

        if (info != _infos.end())
        {
            return info->second;
        }
    }

    return
    {
        _pConfiguration->GetServiceName(),
        _pConfiguration->GetEnvironment(),
        _pConfiguration->GetVersion(),
        _pConfiguration->GetGitRepositoryUrl(),
        _pConfiguration->GetGitCommitSha()
    };
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
}

void ApplicationStore::SetGitMetadata(std::string runtimeId, std::string respositoryUrl, std::string commitSha)
{
    std::lock_guard lock(_infosLock);
    auto& info = _infos[std::move(runtimeId)];
    info.RepositoryUrl = std::move(respositoryUrl);
    info.CommitSha = std::move(commitSha);
}

const char* ApplicationStore::GetName()
{
    return _serviceName;
}

bool ApplicationStore::Start()
{
    // nothing special to start
    return true;
}

bool ApplicationStore::Stop()
{
    // nothing special to stop
    return true;
}
