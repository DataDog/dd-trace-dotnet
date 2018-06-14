#pragma once

#include <map>
#include <corhlpr.h>
#include "TypeReference.h"
#include "MemberReference.h"

// forwards declarations
class IntegrationBase;

class ModuleMetadata
{
private:
    std::map<TypeReference, mdTypeRef> m_TypeMap{};
    std::map<MemberReference, mdMemberRef> m_MemberMap{};
    mdModule module = mdModuleNil;
    IMetaDataImport* metadataImport = nullptr;
    IMetaDataEmit* metadataEmit = nullptr;
    IMetaDataAssemblyImport* assemblyImport = nullptr;
    IMetaDataAssemblyEmit* assemblyEmit = nullptr;

    HRESULT AddElementTypeToSignature(PCOR_SIGNATURE pSignature,
                                      const ULONG maxSignatureLength,
                                      ULONG& signatureLength,
                                      const TypeReference& type);

    HRESULT FindAssemblyRefIterator(const std::wstring& assemblyName,
                                    mdAssemblyRef* rgAssemblyRefs,
                                    ULONG cAssemblyRefs,
                                    mdAssemblyRef* assemblyRef) const;

public:
     std::wstring assemblyName = L"";
     std::vector<const IntegrationBase*> m_Integrations = {};

    ModuleMetadata() = default;

    ModuleMetadata(std::wstring assembly_name,
                   std::vector<const IntegrationBase*> integrations,
                   mdModule module,
                   IMetaDataImport* metadata_import,
                   IMetaDataEmit* metadata_emit,
                   IMetaDataAssemblyImport* assembly_import,
                   IMetaDataAssemblyEmit* assembly_emit);

    bool TryGetRef(const TypeReference& keyIn, mdTypeRef& valueOut) const;

    bool TryGetRef(const MemberReference& keyIn, mdMemberRef& valueOut) const;

    void SetRef(const TypeReference& keyIn, const mdTypeRef& valueIn);

    void SetRef(const MemberReference& keyIn, const mdMemberRef& valueIn);

    HRESULT ResolveType(const TypeReference& type);

    HRESULT ResolveType(const TypeReference& type, mdTypeRef& typeRefOut);

    HRESULT ResolveMember(const MemberReference& member);

    HRESULT ResolveMember(const MemberReference& member, mdMemberRef& memberRefOut);

    HRESULT EmitAssemblyRef(const std::wstring& assemblyName,
                            const ASSEMBLYMETADATA& assemblyMetadata,
                            BYTE publicKeyToken[],
                            ULONG publicKeyTokenLength,
                            mdAssemblyRef& assemblyRef) const;

    HRESULT FindAssemblyRef(const std::wstring& assemblyName, mdAssemblyRef* assemblyRef);

    HRESULT CreateSignature(const MemberReference& member, PCOR_SIGNATURE const pSignature, const ULONG maxSignatureLength, ULONG& signatureLength);

    void GetClassAndFunctionNamesFromMethodDef(mdMethodDef methodDef,
                                               LPWSTR wszTypeDefName,
                                               ULONG cchTypeDefName,
                                               LPWSTR wszMethodDefName,
                                               ULONG cchMethodDefName) const;
};
