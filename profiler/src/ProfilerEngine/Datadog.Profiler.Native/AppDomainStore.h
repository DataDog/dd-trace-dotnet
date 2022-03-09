#pragma once

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
};
