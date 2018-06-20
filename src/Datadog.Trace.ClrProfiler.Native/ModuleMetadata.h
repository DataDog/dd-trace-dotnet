#pragma once

#include <map>
#include <corhlpr.h>
#include "ComPtr.h"
#include "TypeReference.h"
#include "MemberReference.h"
#include "Integration.h"

class ModuleMetadata
{
private:
    std::map<TypeReference, mdTypeRef> m_TypeMap{};
    std::map<MemberReference, mdMemberRef> m_MemberMap{};
    ComPtr<IMetaDataImport> metadataImport{};

public:
    std::wstring assemblyName = L"";
    std::vector<Integration> m_Integrations = {};

    ModuleMetadata() = default;

    ModuleMetadata(ComPtr<IMetaDataImport> metadata_import,
                   std::wstring assembly_name,
                   std::vector<Integration> integration_bases)
        : metadataImport(std::move(metadata_import)),
          assemblyName(std::move(assembly_name)),
          m_Integrations(std::move(integration_bases))
    {
    }

    bool TryGetRef(const TypeReference& keyIn, mdTypeRef& valueOut) const;

    bool TryGetRef(const MemberReference& keyIn, mdMemberRef& valueOut) const;

    void SetRef(const TypeReference& keyIn, const mdTypeRef& valueIn);

    void SetRef(const MemberReference& keyIn, const mdMemberRef& valueIn);

    void GetClassAndFunctionNamesFromMethodDef(mdMethodDef methodDef,
                                               LPWSTR wszTypeDefName,
                                               ULONG cchTypeDefName,
                                               LPWSTR wszMethodDefName,
                                               ULONG cchMethodDefName) const;
};
