// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <mutex>
#include <unordered_map>

#include "IAppDomainStore.h"

class AppDomainStore : public IAppDomainStore
{
public:
    AppDomainStore(ICorProfilerInfo4* pProfilerInfo);

public:
    // Inherited via IAppDomainStore
    bool GetInfo(AppDomainID appDomainId, ProcessID& pid, std::string& appDomainName) override;

private:
    ICorProfilerInfo4* _pProfilerInfo;

    std::mutex _lock;
    std::unordered_map<AppDomainID, std::pair<ProcessID, std::string>> _appDomainToInfo;
};
