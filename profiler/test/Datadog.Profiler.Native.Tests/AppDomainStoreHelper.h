// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "IAppDomainStore.h"
#include <unordered_map>
#include <string>

class AppDomainStoreHelper : public IAppDomainStore
{
public:
    AppDomainStoreHelper(size_t appDomainCount);
    AppDomainStoreHelper(const std::unordered_map<AppDomainID, std::string>& mapping);

public:
    // Inherited via IAppDomainStore
    std::string_view GetName(AppDomainID appDomainId) override;
    void Register(AppDomainID appDomainId) override;

private:
    std::unordered_map<AppDomainID, std::string> _mapping;
};
