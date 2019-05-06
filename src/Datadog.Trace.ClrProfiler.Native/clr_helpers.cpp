#include "clr_helpers.h"

#include <cstring>

#include "logging.h"
#include "macros.h"

namespace trace {

AssemblyInfo GetAssemblyInfo(ICorProfilerInfo3* info,
                             const AssemblyID& assembly_id) {
  WCHAR name[kNameMaxSize];
  DWORD name_len = 0;
  auto hr = info->GetAssemblyInfo(assembly_id, kNameMaxSize, &name_len, name,
                                  nullptr, nullptr);
  if (FAILED(hr) || name_len == 0) {
    return {};
  }
  return {assembly_id, WSTRING(name)};
}

AssemblyMetadata GetAssemblyMetadata(
    const ModuleID& module_id,
    const ComPtr<IMetaDataAssemblyImport>& assembly_import) {
  mdAssembly current = mdAssemblyNil;
  auto hr = assembly_import->GetAssemblyFromScope(&current);

  if (FAILED(hr)) {
    return {};
  }

  WCHAR name[kNameMaxSize];
  WSTRING assembly_name = ""_W;
  DWORD name_len = 0;
  ASSEMBLYMETADATA assembly_m{};
  DWORD assembly_flags = 0;
  hr = assembly_import->GetAssemblyProps(current, nullptr, nullptr, nullptr,
                                         name, kNameMaxSize, &name_len,
                                         &assembly_m, &assembly_flags);
  if (!FAILED(hr) && name_len > 0) {
    assembly_name = WSTRING(name);
  }

  return AssemblyMetadata(
      module_id, assembly_name, current, assembly_m.usMajorVersion,
      assembly_m.usMinorVersion, assembly_m.usBuildNumber,
      assembly_m.usRevisionNumber);
}

AssemblyMetadata GetAssemblyImportMetadata(
    const ComPtr<IMetaDataAssemblyImport>& assembly_import) {
  mdAssembly current = mdAssemblyNil;
  auto hr = assembly_import->GetAssemblyFromScope(&current);
  if (FAILED(hr)) {
    return {};
  }
  WCHAR name[kNameMaxSize];
  DWORD name_len = 0;
  ASSEMBLYMETADATA assembly_metadata{};
  DWORD assembly_flags = 0;
  const ModuleID placeholder_module_id = 0;

  hr = assembly_import->GetAssemblyProps(current, nullptr, nullptr, nullptr,
                                         name, kNameMaxSize, &name_len, &assembly_metadata,
                                         &assembly_flags);
  if (FAILED(hr) || name_len == 0) {
    return {};
  }
  return AssemblyMetadata(placeholder_module_id, name, current,
                          assembly_metadata.usMajorVersion, assembly_metadata.usMinorVersion,
                          assembly_metadata.usBuildNumber, assembly_metadata.usRevisionNumber);
}

AssemblyMetadata GetReferencedAssemblyMetadata(
    const ComPtr<IMetaDataAssemblyImport>& assembly_import,
    const mdAssemblyRef& assembly_ref) {
  WCHAR name[kNameMaxSize];
  DWORD name_len = 0;
  ASSEMBLYMETADATA assembly_metadata{};
  DWORD assembly_flags = 0;
  const ModuleID module_id_placeholder = 0;
  const auto hr = assembly_import->GetAssemblyRefProps(
      assembly_ref, nullptr, nullptr, name, kNameMaxSize, &name_len, &assembly_metadata,
      nullptr, nullptr, &assembly_flags);
  if (FAILED(hr) || name_len == 0) {
    return {};
  }
  return AssemblyMetadata(module_id_placeholder, name, assembly_ref,
                          assembly_metadata.usMajorVersion, assembly_metadata.usMinorVersion,
                          assembly_metadata.usBuildNumber, assembly_metadata.usRevisionNumber);
}

FunctionInfo GetFunctionInfo(const ComPtr<IMetaDataImport2>& metadata_import,
                             const mdToken& token) {
  mdToken parent_token = mdTokenNil;
  WCHAR function_name[kNameMaxSize]{};
  DWORD function_name_len = 0;

  PCCOR_SIGNATURE raw_signature;
  ULONG raw_signature_len;

  HRESULT hr = E_FAIL;
  const auto token_type = TypeFromToken(token);
  switch (token_type) {
    case mdtMemberRef:
      hr = metadata_import->GetMemberRefProps(
          token, &parent_token, function_name, kNameMaxSize, &function_name_len,
          &raw_signature, &raw_signature_len);
      break;
    case mdtMethodDef:
      hr = metadata_import->GetMemberProps(
          token, &parent_token, function_name, kNameMaxSize, &function_name_len,
          nullptr, &raw_signature, &raw_signature_len, nullptr, nullptr,
          nullptr, nullptr, nullptr);
      break;
    case mdtMethodSpec: {
      hr = metadata_import->GetMethodSpecProps(
          token, &parent_token, &raw_signature, &raw_signature_len);
      if (FAILED(hr)) {
        return {};
      }
      auto generic_info = GetFunctionInfo(metadata_import, parent_token);
      std::memcpy(function_name, generic_info.name.c_str(),
                  sizeof(WCHAR) * (generic_info.name.length() + 1));
      function_name_len = (DWORD)(generic_info.name.length() + 1);
    } break;
    default:
      Warn("[trace::GetFunctionInfo] unknown token type: {}", token_type);
  }
  if (FAILED(hr) || function_name_len == 0) {
    return {};
  }

  std::vector<BYTE> signature_data(raw_signature_len);
  for (ULONG i = 0; i < raw_signature_len; i++) {
    signature_data[i] = raw_signature[i];
  }

  // parent_token could be: TypeDef, TypeRef, TypeSpec, ModuleRef, MethodDef
  const auto type_info = GetTypeInfo(metadata_import, parent_token);

  return {token, WSTRING(function_name), type_info,
          MethodSignature(signature_data)};
}

ModuleInfo GetModuleInfo(ICorProfilerInfo3* info, const ModuleID& module_id) {
  const DWORD module_path_size = 260;
  WCHAR module_path[module_path_size]{};
  DWORD module_path_len = 0;
  LPCBYTE base_load_address;
  AssemblyID assembly_id = 0;
  DWORD module_flags = 0;
  const HRESULT hr = info->GetModuleInfo2(
      module_id, &base_load_address, module_path_size, &module_path_len,
      module_path, &assembly_id, &module_flags);
  if (FAILED(hr) || module_path_len == 0) {
    return {};
  }
  return {module_id, WSTRING(module_path), GetAssemblyInfo(info, assembly_id),
          module_flags};
}

TypeInfo GetTypeInfo(const ComPtr<IMetaDataImport2>& metadata_import,
                     const mdToken& token) {
  mdToken parent_token = mdTokenNil;
  WCHAR type_name[kNameMaxSize]{};
  DWORD type_name_len = 0;

  HRESULT hr = E_FAIL;
  const auto token_type = TypeFromToken(token);

  switch (token_type) {
    case mdtTypeDef:
      hr = metadata_import->GetTypeDefProps(token, type_name, kNameMaxSize,
                                            &type_name_len, nullptr, nullptr);
      break;
    case mdtTypeRef:
      hr = metadata_import->GetTypeRefProps(token, &parent_token, type_name,
                                            kNameMaxSize, &type_name_len);
      break;
    case mdtTypeSpec: {
      PCCOR_SIGNATURE signature{};
      ULONG signature_length{};

      hr = metadata_import->GetTypeSpecFromToken(token, &signature,
                                                 &signature_length);

      if (FAILED(hr) || signature_length < 3) {
        return {};
      }

      if (signature[0] & ELEMENT_TYPE_GENERICINST) {
        mdToken type_token;
        CorSigUncompressToken(&signature[2], &type_token);
        return GetTypeInfo(metadata_import, type_token);
      }
    } break;
    case mdtModuleRef:
      metadata_import->GetModuleRefProps(token, type_name, kNameMaxSize,
                                         &type_name_len);
      break;
    case mdtMemberRef:
      return GetFunctionInfo(metadata_import, token).type;
      break;
    case mdtMethodDef:
      return GetFunctionInfo(metadata_import, token).type;
      break;
  }
  if (FAILED(hr) || type_name_len == 0) {
    return {};
  }

  return {token, WSTRING(type_name)};
}

mdAssemblyRef FindAssemblyRef(
    const ComPtr<IMetaDataAssemblyImport>& assembly_import,
    const WSTRING& assembly_name) {
  for (mdAssemblyRef assembly_ref : EnumAssemblyRefs(assembly_import)) {
    if (GetReferencedAssemblyMetadata(assembly_import, assembly_ref).name ==
        assembly_name) {
      return assembly_ref;
    }
  }
  return mdAssemblyRefNil;
}

std::vector<Integration> FilterIntegrationsByName(
    const std::vector<Integration>& integrations,
    const std::vector<WSTRING> integration_names) {
  std::vector<Integration> enabled;

  for (auto& i : integrations) {
    bool disabled = false;

    for (auto& disabled_integration : integration_names) {
      if (i.integration_name == disabled_integration) {
        // this integration is disabled, skip it
        disabled = true;
        break;
      }
    }

    if (!disabled) {
      enabled.push_back(i);
    }
  }

  return enabled;
}

std::vector<Integration> FilterIntegrationsByCaller(
    const std::vector<Integration>& integrations, const AssemblyInfo assembly) {
  std::vector<Integration> enabled;

  for (auto& i : integrations) {
    bool found = false;
    for (auto& mr : i.method_replacements) {
      if (mr.caller_method.assembly.name.empty() ||
          mr.caller_method.assembly.name == assembly.name) {
        found = true;
        break;
      }
    }
    if (found) {
      enabled.push_back(i);
    }
  }

  return enabled;
}

bool AssemblyMeetsIntegrationRequirements(
    const AssemblyMetadata metadata,
    const MethodReplacement method_replacement) {

  const auto target = method_replacement.target_method;

  if (target.assembly.name != metadata.name) {
    // not the expected assembly
    return false;
  }

  if (target.min_v_major > metadata.majorVersion) {
    // below major version requirements
    return false;
  }

  if (target.max_v_major < metadata.majorVersion) {
    // above major version requirements
    return false;
  }

  if (target.min_v_major == metadata.majorVersion &&
      target.min_v_minor > metadata.minorVersion) {
    // below minimum version requirements
    return false;
  }

  if (target.max_v_major == metadata.majorVersion &&
      target.max_v_minor < metadata.minorVersion) {
    // above minimum version requirements
    return false;
  }

  return true;
}

std::vector<Integration> FilterIntegrationsByTarget(
    const std::vector<Integration>& integrations,
    const ComPtr<IMetaDataAssemblyImport>& assembly_import) {
  std::vector<Integration> enabled;

  const auto assembly_metadata = GetAssemblyImportMetadata(assembly_import);

  for (auto& i : integrations) {
    bool found = false;
    // ReSharper disable once CppRangeBasedForIncompatibleReference
    for (auto& mr : i.method_replacements) {
      if (AssemblyMeetsIntegrationRequirements(assembly_metadata, mr)) {
        found = true;
        break;
      }
      for (auto& assembly_ref : EnumAssemblyRefs(assembly_import)) {
        const auto metadata_ref =
            GetReferencedAssemblyMetadata(assembly_import, assembly_ref);
        // Info(L"-- assembly ref: " , assembly_name , " to " , ref_name);
        if (AssemblyMeetsIntegrationRequirements(metadata_ref, mr)) {
          found = true;
          break;
        }
      }
    }
    if (found) {
      enabled.push_back(i);
    }
  }

  return enabled;
}

mdMethodSpec DefineMethodSpec(const ComPtr<IMetaDataEmit2>& metadata_emit,
                              const mdToken& token,
                              const MethodSignature& signature) {
  mdMethodSpec spec = mdMethodSpecNil;
  auto hr = metadata_emit->DefineMethodSpec(
      token, signature.data.data(), ULONG(signature.data.size()), &spec);
  if (FAILED(hr)) {
    Warn("[DefineMethodSpec] failed to define method spec");
  }
  return spec;
}

}  // namespace trace
