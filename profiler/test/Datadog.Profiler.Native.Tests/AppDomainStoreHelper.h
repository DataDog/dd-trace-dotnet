// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <unordered_map>
#include "IAppDomainStore.h"

class AppDomainInfo
{
public:
    AppDomainInfo();
    AppDomainInfo(ProcessID pid, std::string name);

public:
    ProcessID Pid;
    std::string AppDomainName;
};


class AppDomainStoreHelper : public IAppDomainStore
{
public:
    AppDomainStoreHelper(size_t appDomainCount);
    AppDomainStoreHelper(const std::unordered_map<AppDomainID, AppDomainInfo>& mapping);

public:
    // Inherited via IAppDomainStore
    bool GetInfo(AppDomainID appDomainId, ProcessID& pid, std::string& appDomainName) override;

private:
    std::unordered_map<AppDomainID, AppDomainInfo> _mapping;
};
