// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <sstream>
#include "AppDomainStoreHelper.h"


AppDomainInfo::AppDomainInfo()
    :
    AppDomainInfo(0, "")
{
}

AppDomainInfo::AppDomainInfo(ProcessID pid, std::string name)
    :
    Pid{pid},
    AppDomainName{std::move(name)}
{
}

AppDomainStoreHelper::AppDomainStoreHelper(size_t appDomainCount)
{
    for (size_t i = 1; i <= appDomainCount; i++)
    {
        std::stringstream builder;
        builder << "AD_" << i;
        _mapping[i] = AppDomainInfo(i, builder.str());
    }
}

AppDomainStoreHelper::AppDomainStoreHelper(const std::unordered_map<AppDomainID, AppDomainInfo>& mapping)
    :
    _mapping{mapping}
{
}


bool AppDomainStoreHelper::GetInfo(AppDomainID appDomainId, ProcessID& pid, std::string& appDomainName)
{
    auto item = _mapping.find(appDomainId);
    if (item == _mapping.end())
    {
        return false;
    }

    pid = item->second.Pid;
    appDomainName = item->second.AppDomainName;
    return true;
}
