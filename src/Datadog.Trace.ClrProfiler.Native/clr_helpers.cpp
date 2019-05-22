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

  return AssemblyMetadata(module_id, assembly_name, current,
                          assembly_m.usMajorVersion, assembly_m.usMinorVersion,
                          assembly_m.usBuildNumber,
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
                                         name, kNameMaxSize, &name_len,
                                         &assembly_metadata, &assembly_flags);
  if (FAILED(hr) || name_len == 0) {
    return {};
  }
  return AssemblyMetadata(
      placeholder_module_id, name, current, assembly_metadata.usMajorVersion,
      assembly_metadata.usMinorVersion, assembly_metadata.usBuildNumber,
      assembly_metadata.usRevisionNumber);
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
      assembly_ref, nullptr, nullptr, name, kNameMaxSize, &name_len,
      &assembly_metadata, nullptr, nullptr, &assembly_flags);
  if (FAILED(hr) || name_len == 0) {
    return {};
  }
  return AssemblyMetadata(
      module_id_placeholder, name, assembly_ref,
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
          MethodSignature(signature_data), raw_signature};
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

std::vector<IntegrationMethod> FlattenIntegrations(
    const std::vector<Integration>& integrations) {
  std::vector<IntegrationMethod> flattened;

  for (auto& i : integrations) {
    for (auto& mr : i.method_replacements) {
      flattened.emplace_back(i.integration_name, mr);
    }
  }

  return flattened;
}

std::vector<IntegrationMethod> FilterIntegrationsByCaller(
    const std::vector<IntegrationMethod>& integrations,
    const AssemblyInfo assembly) {
  std::vector<IntegrationMethod> enabled;

  for (auto& i : integrations) {
    if (i.replacement.caller_method.assembly.name.empty() ||
        i.replacement.caller_method.assembly.name == assembly.name) {
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

  if (target.min_version > metadata.version) {
    return false;
  }

  if (target.max_version < metadata.version) {
    return false;
  }

  return true;
}

std::vector<IntegrationMethod> FilterIntegrationsByTarget(
    const std::vector<IntegrationMethod>& integrations,
    const ComPtr<IMetaDataAssemblyImport>& assembly_import) {
  std::vector<IntegrationMethod> enabled;

  const auto assembly_metadata = GetAssemblyImportMetadata(assembly_import);

  for (auto& i : integrations) {
    bool found = false;
    if (AssemblyMeetsIntegrationRequirements(assembly_metadata,
                                             i.replacement)) {
      found = true;
    }
    for (auto& assembly_ref : EnumAssemblyRefs(assembly_import)) {
      const auto metadata_ref =
          GetReferencedAssemblyMetadata(assembly_import, assembly_ref);
      // Info(L"-- assembly ref: " , assembly_name , " to " , ref_name);
      if (AssemblyMeetsIntegrationRequirements(metadata_ref, i.replacement)) {
        found = true;
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

WSTRING getTypeName(const ComPtr<IMetaDataImport2>& metadata_import,
                    mdToken token) {
  WCHAR type_name[kNameMaxSize]{};
  DWORD type_name_len = 0;
  HRESULT hr = E_FAIL;
  switch (TypeFromToken(token)) {
    case mdtTypeDef:
      hr = metadata_import->GetTypeDefProps(token, type_name, kNameMaxSize,
                                            &type_name_len, nullptr, nullptr);
      break;

    case mdtTypeRef:
      hr = metadata_import->GetTypeRefProps(token, nullptr, type_name,
                                            kNameMaxSize, &type_name_len);
      break;

    default:
      break;
  }

  if (FAILED(hr)) {
    return ""_W;
  }
  return {type_name, type_name_len};
}

PCCOR_SIGNATURE consumeType(PCCOR_SIGNATURE& signature) {
  const PCCOR_SIGNATURE start = signature;

  const CorElementType elementType = CorSigUncompressElementType(signature);
  switch (elementType) {
    case ELEMENT_TYPE_VOID:
    case ELEMENT_TYPE_BOOLEAN:
    case ELEMENT_TYPE_CHAR:
    case ELEMENT_TYPE_I1:
    case ELEMENT_TYPE_U1:
    case ELEMENT_TYPE_I2:
    case ELEMENT_TYPE_U2:
    case ELEMENT_TYPE_I4:
    case ELEMENT_TYPE_U4:
    case ELEMENT_TYPE_I8:
    case ELEMENT_TYPE_U8:
    case ELEMENT_TYPE_R4:
    case ELEMENT_TYPE_R8:
    case ELEMENT_TYPE_STRING:
      return start;

    case ELEMENT_TYPE_VALUETYPE:
      CorSigUncompressToken(signature);
      return start;

    case ELEMENT_TYPE_CLASS:
      CorSigUncompressToken(signature);
      return start;

    case ELEMENT_TYPE_OBJECT:
      return start;

    case ELEMENT_TYPE_SZARRAY:
      consumeType(signature);
      return start;

    case ELEMENT_TYPE_VAR:
      CorSigUncompressData(signature);
      return start;

    case ELEMENT_TYPE_GENERICINST: {
      CorSigUncompressElementType(signature);
      CorSigUncompressToken(signature);

      const ULONG genericArgumentsCount = CorSigUncompressData(signature);
      for (size_t i = 0; i < genericArgumentsCount; ++i) {
        consumeType(signature);
      }

      return start;
    }

    case ELEMENT_TYPE_BYREF:
      consumeType(signature);
      return start;

    default:
      return start;  // TODO: WHAT EVEN HAPPENS
  }
}

bool SignatureFuzzyMatch(
    const ComPtr<IMetaDataImport2>& metadata_import,
                           MethodSignature signature) {
  auto signature_size = signature.data.size();

  // We skip signature size for the typical format because we've already
  // evaluated to a vector

  const auto calling_convention = CorCallingConvention(signature.data[0]);

  const auto instance_convention = IMAGE_CEE_CS_CALLCONV_HASTHIS;
  const auto generic_convention = IMAGE_CEE_CS_CALLCONV_GENERIC;

  const auto is_instance = (calling_convention & instance_convention) != 0;
  const auto is_generic = (calling_convention & generic_convention) != 0;

  auto generic_count = 0;
  auto param_count = 0;
  auto current_index = 2; // Where the parameters actually start

  if (is_generic) {
    generic_count = signature.data[1];
    param_count = signature.data[2];
    current_index = 3; // offset by one because the method is generic
  } else {
    param_count = signature.data[1];
  }

  std::vector<WSTRING> type_names(param_count + 1); // plus one to account for the return type

  WSTRING current_type_name;

  for (; current_index < signature_size; current_index++) {

    auto param_piece = signature.data[current_index];

    auto cor_element_type = CorElementType(param_piece);

    switch (cor_element_type) {
      case ELEMENT_TYPE_VOID:
        current_type_name += "Void"_W;
        break;

      case ELEMENT_TYPE_BOOLEAN:
        current_type_name += "Boolean"_W;
        break;

      case ELEMENT_TYPE_CHAR:
        current_type_name += "Char16"_W;
        break;

      case ELEMENT_TYPE_I1:
        current_type_name += "Int8"_W;
        break;

      case ELEMENT_TYPE_U1:
        current_type_name += "UInt8"_W;
        break;

      case ELEMENT_TYPE_I2:
        current_type_name += "Int16"_W;
        break;

      case ELEMENT_TYPE_U2:
        current_type_name += "UInt16"_W;
        break;

      case ELEMENT_TYPE_I4:
        current_type_name += "Int32"_W;
        break;

      case ELEMENT_TYPE_U4:
        current_type_name += "UInt32"_W;
        break;

      case ELEMENT_TYPE_I8:
        current_type_name += "Int64"_W;
        break;

      case ELEMENT_TYPE_U8:
        current_type_name += "UInt64"_W;
        break;

      case ELEMENT_TYPE_R4:
        current_type_name += "Single"_W;
        break;

      case ELEMENT_TYPE_R8:
        current_type_name += "Double"_W;
        break;

      case ELEMENT_TYPE_STRING:
        current_type_name += "String"_W;
        break;

      case ELEMENT_TYPE_VALUETYPE: {
        /*const mdToken token = CorSigUncompressToken(param_piece);
        const WSTRING className = getTypeName(metadata_import, token);
        if (className == "System.Guid"_W) {
          result += "Guid"_W;
        } else {
          result += className;
        }*/
        // TODO
        current_type_name += "SomeValueType"_W;
        break;
      }

      case ELEMENT_TYPE_CLASS: {
        /*const mdToken token = CorSigUncompressToken(signature);
        result += getTypeName(metadata_import, token);*/
        // TODO
        current_type_name += "SomeClass"_W;
        break;
      }

      case ELEMENT_TYPE_OBJECT:
        current_type_name += "Object"_W;
        break;

      case ELEMENT_TYPE_SZARRAY:
        /*SignatureToWSTRING(metadata_import, signature, result);
        result += "[]"_W;*/
        // TODO
        current_type_name += "SomeArray"_W;
        break;

      case ELEMENT_TYPE_VAR: {
        /*const ULONG index = CorSigUncompressData(signature);
        result += "Var!"_W;
        result += ToWSTRING(index);*/
        // TODO
        current_type_name += "SomeVar!"_W;
        break;
      }

      case ELEMENT_TYPE_GENERICINST: {
        //const CorElementType genericType =
        //    CorSigUncompressElementType(signature);
        //if (genericType != ELEMENT_TYPE_CLASS) {
        //  // TODO: Let's dive into a type lookup?
        //  break;
        //}

        //const mdToken token = CorSigUncompressToken(signature);
        //result += getTypeName(metadata_import, token);

        //result += "<"_W;

        //const ULONG genericArgumentsCount = CorSigUncompressData(signature);
        //for (size_t i = 0; i < genericArgumentsCount; ++i) {
        //  PCCOR_SIGNATURE type = consumeType(signature);
        //  SignatureToWSTRING(metadata_import, type, result);

        //  if (i != genericArgumentsCount - 1) {
        //    result += ", "_W;
        //  }
        //}

        //result += ">"_W;
        // TODO
        current_type_name += "<SomeGeneric>"_W;
        break;
      }

      case ELEMENT_TYPE_BYREF:
        /*result += "ByRef "_W;
        SignatureToWSTRING(metadata_import, signature, result);*/
        // TODO
        current_type_name += "SomeByRef"_W;
        break;

      default:
        // TODO: We couldn't figure out the type to inspect, so... add a thing?
        // Should probably default to null and skip the check
        break;
    }
  }

  return true;
}

}  // namespace trace
