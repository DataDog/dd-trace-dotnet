#pragma once

#include <corhlpr.h>
#include "ModuleMetadata.h"

class MetadataBuilder
{
private:
    ModuleMetadata& metadata;
    const mdModule module = mdModuleNil;
    const ComPtr<IMetaDataImport> metadataImport{};
    const ComPtr<IMetaDataEmit> metadataEmit{};
    const ComPtr<IMetaDataAssemblyImport> assemblyImport{};
    const ComPtr<IMetaDataAssemblyEmit> assemblyEmit{};

    HRESULT find_assembly_ref(const std::wstring& assembly_name,
                              mdAssemblyRef* assembly_ref_out) const;

    HRESULT find_assembly_ref_iterator(const std::wstring& assembly_name,
                                       mdAssemblyRef assembly_refs[],
                                       ULONG assembly_ref_count,
                                       mdAssemblyRef* assembly_ref_out) const;

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

    HRESULT store_wrapper_type_ref(const method_replacement& method_replacement, mdTypeRef& type_ref_out) const;

    HRESULT store_wrapper_method_ref(const method_replacement& method_replacement) const;

    HRESULT emit_assembly_ref(const std::wstring& assembly_name,
                              const ASSEMBLYMETADATA& assembly_metadata,
                              BYTE public_key_token[],
                              ULONG public_key_token_length,
                              mdAssemblyRef& assembly_ref_out) const;
};
