#pragma once

#include <corhlpr.h>

#include "com_ptr.h"
#include "module_metadata.h"

namespace trace {

class MetadataBuilder {
 private:
  ModuleMetadata& metadata_;
  const mdModule module_ = mdModuleNil;
  const ComPtr<IMetaDataImport2> metadata_import_{};
  const ComPtr<IMetaDataEmit> metadata_emit_{};
  const ComPtr<IMetaDataAssemblyImport> assembly_import_{};
  const ComPtr<IMetaDataAssemblyEmit> assembly_emit_{};

  HRESULT FindTypeReference(const TypeReference& type_reference,
                            mdTypeRef& type_ref_out) const;

 public:
  MetadataBuilder(ModuleMetadata& metadata, const mdModule module,
                  ComPtr<IMetaDataImport2> metadata_import,
                  ComPtr<IMetaDataEmit> metadata_emit,
                  ComPtr<IMetaDataAssemblyImport> assembly_import,
                  ComPtr<IMetaDataAssemblyEmit> assembly_emit)
      : metadata_(metadata),
        module_(module),
        metadata_import_(std::move(metadata_import)),
        metadata_emit_(std::move(metadata_emit)),
        assembly_import_(std::move(assembly_import)),
        assembly_emit_(std::move(assembly_emit)) {}

  HRESULT StoreMethodAdvice(const MethodAdvice& method_advice) const;

  HRESULT StoreMethodReference(const MethodReference& method_reference) const;

  HRESULT EmitAssemblyRef(const trace::AssemblyReference& assembly_ref) const;
};

}  // namespace trace
