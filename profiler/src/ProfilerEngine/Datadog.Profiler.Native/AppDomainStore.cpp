// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <memory>
#include "AppDomainStore.h"

#include "shared/src/native-src/string.h"


AppDomainStore::AppDomainStore(ICorProfilerInfo4* pProfilerInfo)
    :
    _pProfilerInfo{pProfilerInfo}
{
}


bool AppDomainStore::GetInfo(AppDomainID appDomainId, ProcessID& pid, std::string& appDomainName)
{
    std::unique_lock lock{_lock};

    auto it = _appDomainToInfo.find(appDomainId);

    if (it != _appDomainToInfo.cend())
    {
        std::tie(pid, appDomainName) = it->second;
        return true;
    }

    // Get the size of the buffer to allocate and then get the name into the buffer
    // see https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/icorprofilerinfo-getappdomaininfo-method for more details
    ULONG characterCount;
    HRESULT hr = _pProfilerInfo->GetAppDomainInfo(appDomainId, 0, &characterCount, nullptr, &pid);
    if (FAILED(hr)) { return false; }

    auto pBuffer = std::make_unique<WCHAR[]>(characterCount);

    hr = _pProfilerInfo->GetAppDomainInfo(appDomainId, characterCount, &characterCount, pBuffer.get(), &pid);
    if (FAILED(hr)) { return false; }

    // convert from UTF16 to UTF8
    appDomainName = shared::ToString(shared::WSTRING(pBuffer.get()));

    _appDomainToInfo[appDomainId] = std::make_pair(pid, appDomainName);

    return true;
}
