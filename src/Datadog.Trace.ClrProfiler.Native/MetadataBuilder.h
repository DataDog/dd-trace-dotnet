#pragma once

#include <corhlpr.h>
#include "TypeReference.h"
#include "MemberReference.h"
#include "ModuleMetadata.h"

class MetadataBuilder
{
private:
    ModuleMetadata& metadata;
    mdModule module = mdModuleNil;
    IMetaDataImport* metadataImport = nullptr;
    IMetaDataEmit* metadataEmit = nullptr;
    IMetaDataAssemblyImport* assemblyImport = nullptr;
    IMetaDataAssemblyEmit* assemblyEmit = nullptr;

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
                    IMetaDataImport* const metadata_import,
                    IMetaDataEmit* const metadata_emit,
                    IMetaDataAssemblyImport* const assembly_import,
                    IMetaDataAssemblyEmit* const assembly_emit)
        : metadata(metadata),
          module(module),
          metadataImport(metadata_import),
          metadataEmit(metadata_emit),
          assemblyImport(assembly_import),
          assemblyEmit(assembly_emit)
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
