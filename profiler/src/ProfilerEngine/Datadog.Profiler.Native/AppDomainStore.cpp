// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <memory>
#include "AppDomainStore.h"

#include "shared/src/native-src/string.h"
#include "Log.h"

AppDomainStore::AppDomainStore(ICorProfilerInfo4* pProfilerInfo)
    :
    _pProfilerInfo{pProfilerInfo}
{
}

std::string_view AppDomainStore::GetName(AppDomainID appDomainId)
{
    // check for null AppDomainId (garbage collection for example)
    if (appDomainId == 0)
    {
        return "CLR";
    }

    std::unique_lock lock{_lock};

    auto it = _appDomainToName.find(appDomainId);

    if (it != _appDomainToName.cend())
    {
        return it->second;
    }

    return {};
}

void AppDomainStore::Register(AppDomainID appDomainId)
{
    // Get the size of the buffer to allocate and then get the name into the buffer
    // see https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/icorprofilerinfo-getappdomaininfo-method for more details
    ULONG characterCount;
    HRESULT hr = _pProfilerInfo->GetAppDomainInfo(appDomainId, 0, &characterCount, nullptr, nullptr);
    if (FAILED(hr)) { return; }
    auto pBuffer = std::make_unique<WCHAR[]>(characterCount);

    hr = _pProfilerInfo->GetAppDomainInfo(appDomainId, characterCount, &characterCount, pBuffer.get(), nullptr);
    if (FAILED(hr)) { return; }

    auto appDomainName = shared::ToString(shared::WSTRING(pBuffer.get()));

    std::unique_lock lock{_lock};
    _appDomainToName[appDomainId] = std::move(appDomainName);
}
