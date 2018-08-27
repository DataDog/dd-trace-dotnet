#include "metadata_builder.h"
#include <fstream>
#include <string>
#include "Macros.h"
#include "iterators.h"

HRESULT MetadataBuilder::emit_assembly_ref(
    const std::wstring& assembly_name,
    const ASSEMBLYMETADATA& assembly_metadata, BYTE public_key_token[],
    const ULONG public_key_token_length,
    mdAssemblyRef& assembly_ref_out) const {
  const HRESULT hr = assemblyEmit->DefineAssemblyRef(
      static_cast<void*>(public_key_token), public_key_token_length,
      assembly_name.c_str(), &assembly_metadata,
      // hash blob
      nullptr,
      // cb of hash blob
      0,
      // flags
      0, &assembly_ref_out);

  LOG_IFFAILEDRET(hr, L"DefineAssemblyRef failed");
  return S_OK;
}

HRESULT MetadataBuilder::find_assembly_ref(
    const std::wstring& assembly_name, mdAssemblyRef* assembly_ref_out) const {
  for (mdAssemblyRef assembly_ref : trace::EnumAssemblyRefs(assemblyImport)) {
    auto hr = find_assembly_ref_iterator(assembly_name, &assembly_ref, 1,
                                         assembly_ref_out);
    if (SUCCEEDED(hr)) {
      return hr;
    }
  }

  return S_OK;
}

HRESULT MetadataBuilder::find_assembly_ref_iterator(
    const std::wstring& assembly_name, mdAssemblyRef assembly_refs[],
    ULONG assembly_ref_count, mdAssemblyRef* assembly_ref_out) const {
  for (ULONG i = 0; i < assembly_ref_count; i++) {
    const void* pvPublicKeyOrToken;
    ULONG cbPublicKeyOrToken;
    WCHAR wszName[512];
    ULONG cchNameReturned;
    ASSEMBLYMETADATA asmMetaData{};
    // ZeroMemory(&asmMetaData, sizeof(asmMetaData));
    const void* pbHashValue;
    ULONG cbHashValue;
    DWORD asmRefFlags;

    const HRESULT hr = assemblyImport->GetAssemblyRefProps(
        assembly_refs[i], &pvPublicKeyOrToken, &cbPublicKeyOrToken, wszName,
        _countof(wszName), &cchNameReturned, &asmMetaData, &pbHashValue,
        &cbHashValue, &asmRefFlags);

    LOG_IFFAILEDRET(hr, L"GetAssemblyRefProps failed, hr = " << HEX(hr));

    if (assembly_name == wszName) {
      *assembly_ref_out = assembly_refs[i];
      return S_OK;
    }
  }

  return E_FAIL;
}

HRESULT MetadataBuilder::find_wrapper_type_ref(
    const method_replacement& method_replacement,
    mdTypeRef& type_ref_out) const {
  const auto& cache_key =
      method_replacement.wrapper_method.get_type_cache_key();
  mdTypeRef type_ref = mdTypeRefNil;

  if (metadata.TryGetWrapperParentTypeRef(cache_key, type_ref)) {
    // this type was already resolved
    type_ref_out = type_ref;
    return S_OK;
  }

  HRESULT hr;
  type_ref = mdTypeRefNil;

  const LPCWSTR wrapper_type_name =
      method_replacement.wrapper_method.type_name.c_str();

  if (metadata.assemblyName ==
      method_replacement.wrapper_method.assembly_name) {
    // type is defined in this assembly
    hr =
        metadataEmit->DefineTypeRefByName(module, wrapper_type_name, &type_ref);
  } else {
    // type is defined in another assembly,
    // find a reference to the assembly where type lives
    mdAssemblyRef assembly_ref = mdAssemblyRefNil;
    hr = find_assembly_ref(method_replacement.wrapper_method.assembly_name,
                           &assembly_ref);
    RETURN_IF_FAILED(hr);

    // TODO: emit assembly reference if not found?

    // search for an existing reference to the type
    hr =
        metadataImport->FindTypeRef(assembly_ref, wrapper_type_name, &type_ref);

    if (hr == HRESULT(0x80131130) /* record not found on lookup */) {
      // if typeRef not found, create a new one by emiting a metadata token
      hr = metadataEmit->DefineTypeRefByName(assembly_ref, wrapper_type_name,
                                             &type_ref);
    }
  }

  RETURN_IF_FAILED(hr);

  // cache the typeRef in case we need it again
  metadata.SetWrapperParentTypeRef(cache_key, type_ref);
  type_ref_out = type_ref;
  return S_OK;
}

HRESULT MetadataBuilder::store_wrapper_method_ref(
    const method_replacement& method_replacement) const {
  const auto& cache_key =
      method_replacement.wrapper_method.get_method_cache_key();
  mdMemberRef member_ref = mdMemberRefNil;

  if (metadata.TryGetWrapperMemberRef(cache_key, member_ref)) {
    // this member was already resolved
    return S_OK;
  }

  mdTypeRef type_ref = mdTypeRefNil;
  HRESULT hr = find_wrapper_type_ref(method_replacement, type_ref);
  RETURN_IF_FAILED(hr);

  const auto wrapper_method_name =
      method_replacement.wrapper_method.method_name.c_str();
  const auto wrapper_method_signature_data =
      method_replacement.wrapper_method.method_signature.data();
  const auto wrapper_method_signature_size = static_cast<ULONG>(
      method_replacement.wrapper_method.method_signature.size());
  member_ref = mdMemberRefNil;

  hr = metadataImport->FindMemberRef(
      type_ref, wrapper_method_name, wrapper_method_signature_data,
      wrapper_method_signature_size, &member_ref);

  if (hr == HRESULT(0x80131130) /* record not found on lookup */) {
    // if memberRef not found, create it by emiting a metadata token
    hr = metadataEmit->DefineMemberRef(
        type_ref, wrapper_method_name, wrapper_method_signature_data,
        wrapper_method_signature_size, &member_ref);
  }

  RETURN_IF_FAILED(hr);

  metadata.SetWrapperMemberRef(cache_key, member_ref);
  return S_OK;
}
