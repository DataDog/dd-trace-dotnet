// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <sstream>
#include "AppDomainStoreHelper.h"


AppDomainStoreHelper::AppDomainStoreHelper(size_t appDomainCount)
{
    for (size_t i = 1; i <= appDomainCount; i++)
    {
        std::stringstream builder;
        builder << "AD_" << i;
        _mapping[i] = builder.str();
    }
}

AppDomainStoreHelper::AppDomainStoreHelper(const std::unordered_map<AppDomainID, std::string>& mapping)
    :
    _mapping{mapping}
{
}


std::string_view AppDomainStoreHelper::GetName(AppDomainID appDomainId)
{
    auto item = _mapping.find(appDomainId);
    if (item == _mapping.end())
    {
        return {};
    }

    return item->second;
}

void AppDomainStoreHelper::Register(AppDomainID appDomainId)
{
    // do nothing
}