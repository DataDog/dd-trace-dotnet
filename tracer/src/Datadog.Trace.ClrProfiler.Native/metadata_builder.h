﻿#pragma once

#include <corhlpr.h>

#include "com_ptr.h"
#include "logger.h"
#include "module_metadata.h"

namespace trace
{

class MetadataBuilder
{
private:
    ModuleMetadata& metadata_;
    const mdModule module_ = mdModuleNil;
    const ComPtr<IMetaDataImport2> metadata_import_{};
    const ComPtr<IMetaDataEmit> metadata_emit_{};
    const ComPtr<IMetaDataAssemblyImport> assembly_import_{};
    const ComPtr<IMetaDataAssemblyEmit> assembly_emit_{};

    HRESULT FindWrapperTypeRef(const MethodReplacement& method_replacement, mdTypeRef& type_ref_out) const;

public:
    MetadataBuilder(ModuleMetadata& metadata, const mdModule module, ComPtr<IMetaDataImport2> metadata_import,
                    ComPtr<IMetaDataEmit> metadata_emit, ComPtr<IMetaDataAssemblyImport> assembly_import,
                    ComPtr<IMetaDataAssemblyEmit> assembly_emit) :
        metadata_(metadata),
        module_(module),
        metadata_import_(metadata_import),
        metadata_emit_(metadata_emit),
        assembly_import_(assembly_import),
        assembly_emit_(assembly_emit)
    {
    }

    HRESULT StoreWrapperMethodRef(const MethodReplacement& method_replacement) const;

    HRESULT EmitAssemblyRef(const trace::AssemblyReference& assembly_ref) const;
};

} // namespace trace
