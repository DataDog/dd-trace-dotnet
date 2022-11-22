#pragma once

#include "IAppDomainStore.h"

#include "shared/src/native-src/com_ptr.h"

class AppDomainStore : public IAppDomainStore
{
public:
    AppDomainStore(ICorProfilerInfo4* pProfilerInfo);

public:
    // Inherited via IAppDomainStore
    bool GetInfo(AppDomainID appDomainId, ProcessID& pid, std::string& appDomainName) override;

private:
    ComPtr<ICorProfilerInfo4> _pProfilerInfo;
};
