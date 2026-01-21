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
    std::string_view GetName(AppDomainID appDomainId) override;

    // For now we do not deregister app domains
    // .NET Core enforces one and only app domain per process.
    // The only exception is .NET Framework (IIS case).
    // There can be hundreds of them but most of the time their lifetime is tied to the process lifetime.
    void Register(AppDomainID appDomainId) override;
private:
    ICorProfilerInfo4* _pProfilerInfo;

    // no need for a shared mutex:
    // GetInfo is called by only one thread at a time (read)
    // Register is called by multiple threads (write)
    std::mutex _lock;
    std::unordered_map<AppDomainID, std::string> _appDomainToName;
};
