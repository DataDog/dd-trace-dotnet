#include <cmath>
#include <fstream>
#include <string>

#include "clr_helpers.h"
#include "logging.h"
#include "macros.h"
#include "metadata_builder.h"
#include "util.h"

namespace trace {

HRESULT MetadataBuilder::EmitAssemblyRef(
    const trace::AssemblyReference& assembly_ref) const {
  ASSEMBLYMETADATA assembly_metadata{};
  assembly_metadata.usMajorVersion = assembly_ref.version.major;
  assembly_metadata.usMinorVersion = assembly_ref.version.minor;
  assembly_metadata.usBuildNumber = assembly_ref.version.build;
  assembly_metadata.usRevisionNumber = assembly_ref.version.revision;
  if (assembly_ref.locale == u"neutral") {
    assembly_metadata.szLocale = nullptr;
    assembly_metadata.cbLocale = 0;
  } else {
    assembly_metadata.szLocale =
        const_cast<char16_t*>(assembly_ref.locale.data());
    assembly_metadata.cbLocale = (DWORD)(assembly_ref.locale.size());
  }

  logger_->info("EmitAssemblyRef {}", ToU8(assembly_ref.str()));

  DWORD public_key_size = 8;
  if (assembly_ref.public_key == trace::PublicKey()) {
    public_key_size = 0;
  }

  mdAssemblyRef assembly_ref_out;
  const HRESULT hr = assembly_emit_->DefineAssemblyRef(
      &assembly_ref.public_key.data[0], public_key_size,
      assembly_ref.name.c_str(), &assembly_metadata,
      // hash blob
      nullptr,
      // cb of hash blob
      0,
      // flags
      0, &assembly_ref_out);

  if (FAILED(hr)) {
    logger_->error("DefineAssemblyRef failed");
  }
  return S_OK;
}

HRESULT MetadataBuilder::FindWrapperTypeRef(
    const MethodReplacement& method_replacement,
    mdTypeRef& type_ref_out) const {
  const auto& cache_key =
      method_replacement.wrapper_method.get_type_cache_key();
  mdTypeRef type_ref = mdTypeRefNil;

  if (metadata_.TryGetWrapperParentTypeRef(cache_key, type_ref)) {
    // this type was already resolved
    type_ref_out = type_ref;
    return S_OK;
  }

  HRESULT hr;
  type_ref = mdTypeRefNil;

  const LPCWSTR wrapper_type_name =
      method_replacement.wrapper_method.type_name.c_str();

  if (metadata_.assemblyName ==
      method_replacement.wrapper_method.assembly.name) {
    // type is defined in this assembly
    hr = metadata_emit_->DefineTypeRefByName(module_, wrapper_type_name,
                                             &type_ref);
  } else {
    // type is defined in another assembly,
    // find a reference to the assembly where type lives
    const auto assembly_ref = FindAssemblyRef(
        assembly_import_, method_replacement.wrapper_method.assembly.name);
    if (assembly_ref == mdAssemblyRefNil) {
      // TODO: emit assembly reference if not found?
      logger_->error("Assembly reference for {} not found.",
                     ToU8(method_replacement.wrapper_method.assembly.name));
      return E_FAIL;
    }

    // search for an existing reference to the type
    hr = metadata_import_->FindTypeRef(assembly_ref, wrapper_type_name,
                                       &type_ref);

    if (hr == HRESULT(0x80131130) /* record not found on lookup */) {
      // if typeRef not found, create a new one by emitting a metadata token
      hr = metadata_emit_->DefineTypeRefByName(assembly_ref, wrapper_type_name,
                                               &type_ref);
    }
  }

  RETURN_IF_FAILED(hr);

  // cache the typeRef in case we need it again
  metadata_.SetWrapperParentTypeRef(cache_key, type_ref);
  type_ref_out = type_ref;
  return S_OK;
}

HRESULT MetadataBuilder::StoreWrapperMethodRef(
    const MethodReplacement& method_replacement) const {
  const auto& cache_key =
      method_replacement.wrapper_method.get_method_cache_key();
  mdMemberRef member_ref = mdMemberRefNil;

  if (metadata_.TryGetWrapperMemberRef(cache_key, member_ref)) {
    // this member was already resolved
    return S_OK;
  }

  mdTypeRef type_ref = mdTypeRefNil;
  HRESULT hr = FindWrapperTypeRef(method_replacement, type_ref);
  RETURN_IF_FAILED(hr);

  const auto wrapper_method_name =
      method_replacement.wrapper_method.method_name.c_str();
  member_ref = mdMemberRefNil;

  hr = metadata_import_->FindMemberRef(
      type_ref, wrapper_method_name,
      method_replacement.wrapper_method.method_signature.data.data(),
      (DWORD)(method_replacement.wrapper_method.method_signature.data.size()),
      &member_ref);

  if (hr == HRESULT(0x80131130) /* record not found on lookup */) {
    // if memberRef not found, create it by emitting a metadata token
    hr = metadata_emit_->DefineMemberRef(
        type_ref, wrapper_method_name,
        method_replacement.wrapper_method.method_signature.data.data(),
        (DWORD)(method_replacement.wrapper_method.method_signature.data.size()),
        &member_ref);
  }

  RETURN_IF_FAILED(hr);

  metadata_.SetWrapperMemberRef(cache_key, member_ref);
  return S_OK;
}

}  // namespace trace
