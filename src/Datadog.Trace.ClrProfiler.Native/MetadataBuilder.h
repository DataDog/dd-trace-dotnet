#pragma once

#include <corhlpr.h>
#include "ModuleMetadata.h"

class MetadataBuilder
{
private:
    ModuleMetadata& metadata;
    mdModule module = mdModuleNil;
    const ComPtr<IMetaDataImport> metadataImport{};
    const ComPtr<IMetaDataEmit> metadataEmit{};
    const ComPtr<IMetaDataAssemblyImport> assemblyImport{};
    const ComPtr<IMetaDataAssemblyEmit> assemblyEmit{};

    HRESULT FindAssemblyRef(const std::wstring& assemblyName, mdAssemblyRef* assemblyRef);

    HRESULT FindAssemblyRefIterator(const std::wstring& assemblyName,
                                    mdAssemblyRef* rgAssemblyRefs,
                                    ULONG cAssemblyRefs,
                                    mdAssemblyRef* assemblyRef) const;

    HRESULT CreateSignature(const MemberReference& member,
                            PCOR_SIGNATURE pSignature,
                            ULONG maxSignatureLength,
                            ULONG& signatureLength);

    HRESULT AddElementTypeToSignature(PCOR_SIGNATURE pSignature,
                                      ULONG maxSignatureLength,
                                      ULONG& signatureLength,
                                      const TypeReference& type);

public:
    MetadataBuilder(ModuleMetadata& metadata,
                    const mdModule module,
                    ComPtr<IMetaDataImport> metadata_import,
                    ComPtr<IMetaDataEmit> metadata_emit,
                    ComPtr<IMetaDataAssemblyImport> assembly_import,
                    ComPtr<IMetaDataAssemblyEmit> assembly_emit)
        : metadata(metadata),
          module(module),
          metadataImport(std::move(metadata_import)),
          metadataEmit(std::move(metadata_emit)),
          assemblyImport(std::move(assembly_import)),
          assemblyEmit(std::move(assembly_emit))
    {
    }

    HRESULT ResolveType(const TypeReference& type);

    HRESULT ResolveType(const TypeReference& type, mdTypeRef& typeRefOut);

    HRESULT ResolveMember(const MemberReference& member);

    HRESULT ResolveMember(const MemberReference& member, mdMemberRef& memberRefOut);

    HRESULT EmitAssemblyRef(const std::wstring& assemblyName,
                            const ASSEMBLYMETADATA& assemblyMetadata,
                            BYTE publicKeyToken[],
                            ULONG publicKeyTokenLength,
                            mdAssemblyRef& assemblyRef) const;
};
