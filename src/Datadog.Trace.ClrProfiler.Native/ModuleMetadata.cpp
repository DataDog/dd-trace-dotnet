#include <fstream>
#include <utility>
#include "ModuleMetadata.h"
#include "Macros.h"
#include "IntegrationBase.h"

bool ModuleMetadata::TryGetRef(const TypeReference& keyIn, mdTypeRef& valueOut) const
{
    const auto search = m_TypeMap.find(keyIn);

    if (search != m_TypeMap.end())
    {
        valueOut = search->second;
        return true;
    }

    return false;
}

bool ModuleMetadata::TryGetRef(const MemberReference& keyIn, mdMemberRef& valueOut) const
{
    const auto search = m_MemberMap.find(keyIn);

    if (search != m_MemberMap.end())
    {
        valueOut = search->second;
        return true;
    }

    return false;
}

void ModuleMetadata::SetRef(const TypeReference& keyIn, const mdTypeRef& valueIn)
{
    m_TypeMap[keyIn] = valueIn;
}

void ModuleMetadata::SetRef(const MemberReference& keyIn, const mdMemberRef& valueIn)
{
    m_MemberMap[keyIn] = valueIn;
}

void ModuleMetadata::GetClassAndFunctionNamesFromMethodDef(mdMethodDef methodDef,
                                                           LPWSTR wszTypeDefName,
                                                           ULONG cchTypeDefName,
                                                           LPWSTR wszMethodDefName,
                                                           ULONG cchMethodDefName) const
{
    mdTypeDef typeDef;
    ULONG cchMethodDefActual;
    DWORD dwMethodAttr;
    ULONG cchTypeDefActual;
    DWORD dwTypeDefFlags;
    mdTypeDef typeDefBase;

    HRESULT hr = metadataImport->GetMethodProps(methodDef,
                                                &typeDef,
                                                wszMethodDefName,
                                                cchMethodDefName,
                                                &cchMethodDefActual,
                                                &dwMethodAttr,
                                                // [OUT] point to the blob value of meta data
                                                nullptr,
                                                // [OUT] actual size of signature blob
                                                nullptr,
                                                // [OUT] codeRVA
                                                nullptr,
                                                // [OUT] Impl. Flags
                                                nullptr);

    if (FAILED(hr))
    {
        LOG_APPEND(L"GetMethodProps failed for methodDef = " << HEX(methodDef) << L", hr = " << HEX(hr));
    }

    hr = metadataImport->GetTypeDefProps(typeDef,
                                         wszTypeDefName,
                                         cchTypeDefName,
                                         &cchTypeDefActual,
                                         &dwTypeDefFlags,
                                         &typeDefBase);

    if (FAILED(hr))
    {
        LOG_APPEND(L"GetTypeDefProps failed for typeDef = " << HEX(typeDef) << L", hr = " << HEX(hr));
    }
}
