#include "metadata_builder.h"
#include <fstream>
#include <string>
#include "Macros.h"
#include "iterators.h"

HRESULT MetadataBuilder::EmitAssemblyRef(
    const trace::AssemblyReference& assembly_ref) const {
  ASSEMBLYMETADATA assembly_metadata{};
  assembly_metadata.usMajorVersion = assembly_ref.version.major;
  assembly_metadata.usMinorVersion = assembly_ref.version.minor;
  assembly_metadata.usBuildNumber = assembly_ref.version.build;
  assembly_metadata.usRevisionNumber = assembly_ref.version.revision;
  assembly_metadata.szLocale = const_cast<wchar_t*>(assembly_ref.locale.data());
  assembly_metadata.cbLocale = (unsigned long)(assembly_ref.locale.size());

  LOG_APPEND("EmitAssemblyRef " << assembly_ref.str());

  unsigned long public_key_size = 8;
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

  LOG_IFFAILEDRET(hr, L"DefineAssemblyRef failed");
  return S_OK;
}

HRESULT MetadataBuilder::FindAssemblyRef(
    const std::wstring& assembly_name, mdAssemblyRef& assembly_ref_out) const {
  for (mdAssemblyRef assembly_ref : trace::EnumAssemblyRefs(assembly_import_)) {
    if (GetAssemblyName(assembly_ref) == assembly_name) {
      assembly_ref_out = assembly_ref;
      return S_OK;
    }
  }

  return S_OK;
}

std::wstring MetadataBuilder::GetAssemblyName(
    const mdAssemblyRef& assembly_ref) const {
  const unsigned long str_max = 512;
  std::wstring str(str_max, 0);
  unsigned long str_len = 0;
  ASSEMBLYMETADATA assembly_metadata{};
  DWORD assembly_flags = 0;
  auto hr = assembly_import_->GetAssemblyRefProps(
      assembly_ref, nullptr, nullptr, str.data(), str_max, &str_len,
      &assembly_metadata, nullptr, nullptr, &assembly_flags);
  if (FAILED(hr)) {
    return L"";
  }
  str = str.substr(0, str_len - 1);
  return str;
}

HRESULT MetadataBuilder::FindWrapperTypeRef(
    const method_replacement& method_replacement,
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
    mdAssemblyRef assembly_ref = mdAssemblyRefNil;
    hr = FindAssemblyRef(method_replacement.wrapper_method.assembly.name,
                         assembly_ref);
    RETURN_IF_FAILED(hr);

    // TODO: emit assembly reference if not found?

    // search for an existing reference to the type
    hr = metadata_import_->FindTypeRef(assembly_ref, wrapper_type_name,
                                       &type_ref);

    if (hr == HRESULT(0x80131130) /* record not found on lookup */) {
      // if typeRef not found, create a new one by emiting a metadata token
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
    const method_replacement& method_replacement) const {
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
      (unsigned long)(method_replacement.wrapper_method.method_signature.data
                          .size()),
      &member_ref);

  if (hr == HRESULT(0x80131130) /* record not found on lookup */) {
    // if memberRef not found, create it by emiting a metadata token
    hr = metadata_emit_->DefineMemberRef(
        type_ref, wrapper_method_name,
        method_replacement.wrapper_method.method_signature.data.data(),
        (unsigned long)(method_replacement.wrapper_method.method_signature.data
                            .size()),
        &member_ref);
  }

  RETURN_IF_FAILED(hr);

  metadata_.SetWrapperMemberRef(cache_key, member_ref);
  return S_OK;
}
