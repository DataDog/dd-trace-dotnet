// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "MockProfilerInfo.h"

#include "shared/src/native-src/string.h"

#include <cstring>
#include <unordered_map>
#include <vector>

// ICorProfilerInfo4 mock that knows how to answer the calls needed to detect the core library
// module (i.e. the ones made by FrameStore::GetAssemblyName) and that records what is asked to
// the runtime when a type is resolved.
//
// Each module is its own assembly: the AssemblyID is the ModuleID.
class CoreLibMockProfilerInfo : public MockProfilerInfo
{
public:
    void AddModule(ModuleID moduleId, const WCHAR* assemblyName)
    {
        _assemblyNames[moduleId] = assemblyName;
    }

    // modules whose metadata has been requested, in order
    std::vector<ModuleID> MetadataRequests;

    // tokens given to GetClassFromTokenAndTypeArgs, in order
    std::vector<mdToken> ResolvedTokens;

    ClassID ClassIdToReturn = 0;
    HRESULT ClassFromTokenResult = S_OK;

    // number of times a type has been inspected (i.e. GetClassIDInfo2 calls)
    size_t InspectedClassCount = 0;

    HRESULT STDMETHODCALLTYPE GetClassIDInfo2(
        ClassID /*classId*/, ModuleID* /*pModuleId*/, mdTypeDef* /*pTypeDefToken*/,
        ClassID* /*pParentClassId*/, ULONG32 /*cNumTypeArgs*/, ULONG32* /*pcNumTypeArgs*/,
        ClassID /*typeArgs*/[]) override
    {
        InspectedClassCount++;

        // no type description: the caller is expected to give up gracefully
        return E_FAIL;
    }

    HRESULT STDMETHODCALLTYPE GetModuleInfo(
        ModuleID moduleId, LPCBYTE* /*ppBaseLoadAddress*/, ULONG /*cchName*/,
        ULONG* /*pcchName*/, WCHAR /*szName*/[], AssemblyID* pAssemblyId) override
    {
        if (_assemblyNames.find(moduleId) == _assemblyNames.end())
        {
            return E_FAIL;
        }

        if (pAssemblyId != nullptr)
        {
            *pAssemblyId = static_cast<AssemblyID>(moduleId);
        }

        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetAssemblyInfo(
        AssemblyID assemblyId, ULONG cchName, ULONG* pcchName, WCHAR szName[],
        AppDomainID* /*pAppDomainId*/, ModuleID* /*pModuleId*/) override
    {
        auto entry = _assemblyNames.find(static_cast<ModuleID>(assemblyId));
        if (entry == _assemblyNames.end())
        {
            return E_FAIL;
        }

        const shared::WSTRING& name = entry->second;
        const ULONG charCount = static_cast<ULONG>(name.size()) + 1; // including the final '\0'

        if (pcchName != nullptr)
        {
            *pcchName = charCount;
        }

        if (szName == nullptr)
        {
            return S_OK;
        }

        if (cchName < charCount)
        {
            return E_FAIL;
        }

        std::memcpy(szName, name.c_str(), charCount * sizeof(WCHAR));

        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetModuleMetaData(
        ModuleID moduleId, DWORD /*dwOpenFlags*/, REFIID /*riid*/, IUnknown** /*ppOut*/) override
    {
        MetadataRequests.push_back(moduleId);

        // no metadata implementation: the caller is expected to give up gracefully
        return E_FAIL;
    }

    HRESULT STDMETHODCALLTYPE GetClassFromTokenAndTypeArgs(
        ModuleID /*moduleId*/, mdTypeDef typeDef, ULONG32 /*cTypeArgs*/,
        ClassID /*typeArgs*/[], ClassID* pClassId) override
    {
        ResolvedTokens.push_back(typeDef);

        if (pClassId != nullptr)
        {
            *pClassId = ClassIdToReturn;
        }

        return ClassFromTokenResult;
    }

private:
    std::unordered_map<ModuleID, shared::WSTRING> _assemblyNames;
};
