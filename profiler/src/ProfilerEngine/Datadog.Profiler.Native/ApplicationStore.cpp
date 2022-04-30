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
        _pConfiguration->GetVersion()
    };
}


void ApplicationStore::SetApplicationInfo(const std::string& runtimeId, const std::string& serviceName, const std::string& environment, const std::string& version)
{
    const ApplicationInfo info(serviceName, environment, version);

    std::lock_guard lock(_infosLock);
    _infos.insert_or_assign(runtimeId, info);
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
