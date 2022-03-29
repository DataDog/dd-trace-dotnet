#pragma once

#include <corhlpr.h>

#include "../../../shared/src/native-src/com_ptr.h"

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

        HRESULT FindIntegrationTypeRef(const IntegrationDefinition& integration_definition, mdTypeRef& type_ref_out) const;
        HRESULT EmitAssemblyRef(const trace::AssemblyReference& assembly_ref) const;
    };

} // namespace trace
