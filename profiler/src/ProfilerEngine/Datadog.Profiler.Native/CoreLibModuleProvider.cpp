// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CoreLibModuleProvider.h"

#include "FrameStore.h"
#include "HResultConverter.h"
#include "Log.h"

#include <utility>

CoreLibModuleProvider::CoreLibModuleProvider(ICorProfilerInfo4* pCorProfilerInfo) :
    _pCorProfilerInfo{pCorProfilerInfo},
    _moduleId{0}
{
}

bool CoreLibModuleProvider::OnModuleLoaded(ModuleID moduleId)
{
    if (_moduleId.load(std::memory_order_acquire) != 0)
    {
        return false;
    }

    std::string assemblyName;
    if (!FrameStore::GetAssemblyName(_pCorProfilerInfo, moduleId, assemblyName))
    {
        Log::Warn("Failed to retrieve assembly name for module ", moduleId);
        return false;
    }

    if (assemblyName != "System.Private.CoreLib" && assemblyName != "mscorlib")
    {
        return false;
    }

    ModuleID noModule = 0;
    if (!_moduleId.compare_exchange_strong(noModule, moduleId, std::memory_order_acq_rel))
    {
        // another thread got there first
        return false;
    }

    Log::Debug("Core library module found: ", assemblyName, " (module ", moduleId, ")");
    return true;
}

ModuleID CoreLibModuleProvider::GetModuleId() const
{
    return _moduleId.load(std::memory_order_acquire);
}

ComPtr<IMetaDataImport2> CoreLibModuleProvider::GetMetadata()
{
    std::lock_guard<std::mutex> lock(_lock);

    if (GetMetadataNoLock() == nullptr)
    {
        return {};
    }

    return _pMetadataImport;
}

ClassID CoreLibModuleProvider::ResolveTypeInCoreLib(const WCHAR* fullTypeName)
{
    if (fullTypeName == nullptr)
    {
        return 0;
    }

    ModuleID moduleId = _moduleId.load(std::memory_order_acquire);
    if (moduleId == 0)
    {
        return 0;
    }

    std::lock_guard<std::mutex> lock(_lock);

    shared::WSTRING typeName(fullTypeName);
    auto entry = _resolvedTypes.find(typeName);
    if (entry != _resolvedTypes.end())
    {
        return entry->second;
    }

    ClassID classId = ResolveTypeNoLock(moduleId, fullTypeName);

    if (classId != 0)
    {
        _resolvedTypes.emplace(std::move(typeName), classId);
    }

    return classId;
}

IMetaDataImport2* CoreLibModuleProvider::GetMetadataNoLock()
{
    if (_pMetadataImport.Get() != nullptr)
    {
        return _pMetadataImport.Get();
    }

    ModuleID moduleId = _moduleId.load(std::memory_order_acquire);
    if (moduleId == 0)
    {
        return nullptr;
    }

    HRESULT hr = _pCorProfilerInfo->GetModuleMetaData(
        moduleId, CorOpenFlags::ofRead, IID_IMetaDataImport2,
        reinterpret_cast<IUnknown**>(_pMetadataImport.GetAddressOf()));

    if (FAILED(hr))
    {
        Log::Debug("GetModuleMetaData() failed for the core library with HRESULT = ", HResultConverter::ToStringWithCode(hr));
        _pMetadataImport.Reset();
        return nullptr;
    }

    return _pMetadataImport.Get();
}

ClassID CoreLibModuleProvider::ResolveTypeNoLock(ModuleID moduleId, const WCHAR* fullTypeName)
{
    IMetaDataImport2* pMetadataImport = GetMetadataNoLock();
    if (pMetadataImport == nullptr)
    {
        return 0;
    }

    mdTypeDef typeDef = mdTokenNil;
    HRESULT hr = pMetadataImport->FindTypeDefByName(fullTypeName, mdTokenNil, &typeDef);
    if (FAILED(hr) || TypeFromToken(typeDef) != mdtTypeDef)
    {
        return 0;
    }

    ClassID classId = 0;
    hr = _pCorProfilerInfo->GetClassFromTokenAndTypeArgs(moduleId, typeDef, 0, nullptr, &classId);

    return SUCCEEDED(hr) ? classId : 0;
}
