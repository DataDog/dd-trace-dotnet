#include "clr_helpers.h"

#include <cstring>

#include <set>
#include <stack>
#include "environment_variables.h"
#include "logging.h"
#include "macros.h"
#include "pal.h"
#include "sig_helpers.h"

namespace trace {

RuntimeInformation GetRuntimeInformation(ICorProfilerInfo3* info) {
  COR_PRF_RUNTIME_TYPE runtime_type;
  USHORT major_version;
  USHORT minor_version;
  USHORT build_version;
  USHORT qfe_version;

  auto hr = info->GetRuntimeInformation(nullptr, &runtime_type, &major_version, &minor_version, &build_version, &qfe_version, 0, nullptr, nullptr);
  if (FAILED(hr)) {
    return {};
  }

  return {runtime_type, major_version, minor_version, build_version, qfe_version};
}

AssemblyInfo GetAssemblyInfo(ICorProfilerInfo3* info,
                             const AssemblyID& assembly_id) {
  WCHAR assembly_name[kNameMaxSize];
  DWORD assembly_name_len = 0;
  AppDomainID app_domain_id;
  ModuleID manifest_module_id;

  auto hr = info->GetAssemblyInfo(assembly_id, kNameMaxSize, &assembly_name_len,
                                  assembly_name, &app_domain_id, &manifest_module_id);

  if (FAILED(hr) || assembly_name_len == 0) {
    return {};
  }

  WCHAR app_domain_name[kNameMaxSize];
  DWORD app_domain_name_len = 0;

  hr = info->GetAppDomainInfo(app_domain_id, kNameMaxSize, &app_domain_name_len,
                              app_domain_name, nullptr);

  if (FAILED(hr) || app_domain_name_len == 0) {
    return {};
  }

  return {assembly_id, WSTRING(assembly_name), manifest_module_id, app_domain_id,
          WSTRING(app_domain_name)};
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

std::vector<BYTE> GetSignatureByteRepresentation(
    ULONG signature_length, PCCOR_SIGNATURE raw_signature) {
  std::vector<BYTE> signature_data(signature_length);
  for (ULONG i = 0; i < signature_length; i++) {
    signature_data[i] = raw_signature[i];
  }

  return signature_data;
}

FunctionInfo GetFunctionInfo(const ComPtr<IMetaDataImport2>& metadata_import,
                             const mdToken& token) {
  mdToken parent_token = mdTokenNil;
  mdToken method_spec_token = mdTokenNil;
  mdToken method_def_token = mdTokenNil;
  WCHAR function_name[kNameMaxSize]{};
  DWORD function_name_len = 0;

  PCCOR_SIGNATURE raw_signature;
  ULONG raw_signature_len;
  BOOL is_generic = false;
  std::vector<BYTE> final_signature_bytes;
  std::vector<BYTE> method_spec_signature;

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
      is_generic = true;
      if (FAILED(hr)) {
        return {};
      }
      const auto generic_info = GetFunctionInfo(metadata_import, parent_token);
      final_signature_bytes = generic_info.signature.data;
      method_spec_signature =
          GetSignatureByteRepresentation(raw_signature_len, raw_signature);
      std::memcpy(function_name, generic_info.name.c_str(),
                  sizeof(WCHAR) * (generic_info.name.length() + 1));
      function_name_len = DWORD(generic_info.name.length() + 1);
      method_spec_token = token;
      method_def_token = generic_info.id;
    } break;
    default:
      Warn("[trace::GetFunctionInfo] unknown token type: {}", token_type);
      return {};
  }
  if (FAILED(hr) || function_name_len == 0) {
    return {};
  }

  // parent_token could be: TypeDef, TypeRef, TypeSpec, ModuleRef, MethodDef
  const auto type_info = GetTypeInfo(metadata_import, parent_token);

  if (is_generic) {
    // use the generic constructor and feed both method signatures
    return {method_spec_token,
            WSTRING(function_name),
            type_info,
            MethodSignature(final_signature_bytes),
            MethodSignature(method_spec_signature),
            method_def_token};
  }

  final_signature_bytes =
      GetSignatureByteRepresentation(raw_signature_len, raw_signature);

  return {token, WSTRING(function_name), type_info,
          MethodSignature(final_signature_bytes)};
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
    const std::vector<WSTRING>& integration_names) {
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

bool DisableOptimizations() {
  const auto disable_optimizations =
      GetEnvironmentValue(environment::clr_disable_optimizations);

  if (disable_optimizations == "1"_W ||
      disable_optimizations == "true"_W) {
    return true;
  }

  // default to false: don't disable JIT optimizations
  return false;
}

TypeInfo RetrieveTypeForSignature(
    const ComPtr<IMetaDataImport2>& metadata_import,
    const FunctionInfo& function_info, const size_t current_index,
    ULONG& token_length) {
  mdToken type_token;
  const auto type_token_start =
      PCCOR_SIGNATURE(&function_info.signature.data[current_index]);
  token_length = CorSigUncompressToken(type_token_start, &type_token);
  auto type_data = GetTypeInfo(metadata_import, type_token);
  return type_data;
}

bool TryParseSignatureTypes(const ComPtr<IMetaDataImport2>& metadata_import,
                            const FunctionInfo& function_info,
                            std::vector<WSTRING>& signature_result) {
  try {
    const auto signature_size = function_info.signature.data.size();
    const auto generic_count = function_info.signature.NumberOfTypeArguments();
    const auto param_count = function_info.signature.NumberOfArguments();
    size_t current_index = 2;  // Where the parameters actually start

    if (generic_count > 0) {
      current_index++;  // offset by one because the method is generic
    }

    const auto expected_number_of_types = param_count + 1;
    size_t current_type_index = 0;
    std::vector<WSTRING> type_names(expected_number_of_types);

    std::stack<int> generic_arg_stack;
    WSTRING append_to_type = ""_W;
    WSTRING current_type_name = ""_W;

    for (; current_index < signature_size; current_index++) {
      mdToken type_token;
      ULONG token_length;
      auto param_piece = function_info.signature.data[current_index];
      const auto cor_element_type = CorElementType(param_piece);

      switch (cor_element_type) {
        case ELEMENT_TYPE_VOID: {
          current_type_name.append("System.Void"_W);
          break;
        }

        case ELEMENT_TYPE_BOOLEAN: {
          current_type_name.append("System.Boolean"_W);
          break;
        }

        case ELEMENT_TYPE_CHAR: {
          current_type_name.append("System.Char16"_W);
          break;
        }

        case ELEMENT_TYPE_I1: {
          current_type_name.append("System.SByte"_W);
          break;
        }

        case ELEMENT_TYPE_U1: {
          current_type_name.append("System.Byte"_W);
          break;
        }

        case ELEMENT_TYPE_I2: {
          current_type_name.append("System.Int16"_W);
          break;
        }

        case ELEMENT_TYPE_U2: {
          current_type_name.append("System.UInt16"_W);
          break;
        }

        case ELEMENT_TYPE_I4: {
          current_type_name.append("System.Int32"_W);
          break;
        }

        case ELEMENT_TYPE_U4: {
          current_type_name.append("System.UInt32"_W);
          break;
        }

        case ELEMENT_TYPE_I8: {
          current_type_name.append("System.Int64"_W);
          break;
        }

        case ELEMENT_TYPE_U8: {
          current_type_name.append("System.UInt64"_W);
          break;
        }

        case ELEMENT_TYPE_R4: {
          current_type_name.append("System.Single"_W);
          break;
        }

        case ELEMENT_TYPE_R8: {
          current_type_name.append("System.Double"_W);
          break;
        }

        case ELEMENT_TYPE_STRING: {
          current_type_name.append("System.String"_W);
          break;
        }

        case ELEMENT_TYPE_OBJECT: {
          current_type_name.append("System.Object"_W);
          break;
        }

        case ELEMENT_TYPE_VALUETYPE:
        case ELEMENT_TYPE_CLASS: {
          current_index++;
          auto type_data = RetrieveTypeForSignature(
              metadata_import, function_info, current_index, token_length);

          mdToken examined_type_token = type_data.id;
          auto examined_type_name = type_data.name;
          auto ongoing_type_name = examined_type_name;

          // check for whether this may be a nested class
          while (examined_type_name.find_first_of("."_W) == std::string::npos) {
            // This may possibly be a nested class, check for the parent
            mdToken potentialParentToken;
            metadata_import->GetNestedClassProps(examined_type_token,
                                                 &potentialParentToken);

            if (potentialParentToken == mdTokenNil) {
              break;
            }

            auto nesting_type =
                GetTypeInfo(metadata_import, potentialParentToken);

            examined_type_token = nesting_type.id;
            examined_type_name = nesting_type.name;

            ongoing_type_name = examined_type_name + "+"_W + ongoing_type_name;
          }

          // index will be moved up one on every loop
          // handle tokens which have more than one byte
          current_index += token_length - 1;
          current_type_name.append(ongoing_type_name);
          break;
        }

        case ELEMENT_TYPE_SZARRAY: {
          append_to_type.append("[]"_W);
          while (function_info.signature.data[(current_index + 1)] ==
                 ELEMENT_TYPE_SZARRAY) {
            append_to_type.append("[]"_W);
            current_index++;
          }
          // Next will be the type of the array(s)
          continue;
        }

        case ELEMENT_TYPE_MVAR: {
          // We are likely parsing a standalone generic param
          token_length = CorSigUncompressToken(
              PCCOR_SIGNATURE(&function_info.signature.data[current_index]),
              &type_token);
          current_type_name.append("T"_W);
          current_index += token_length;
          // TODO: implement conventions for generics (eg., TC1, TC2, TM1, TM2)
          // current_type_name.append(std::to_wstring(type_token));
          break;
        }

        case ELEMENT_TYPE_VAR: {
          // We are likely within a generic variant
          token_length = CorSigUncompressToken(
              PCCOR_SIGNATURE(&function_info.signature.data[current_index]),
              &type_token);
          current_type_name.append("T"_W);
          current_index += token_length;
          // TODO: implement conventions for generics (eg., TC1, TC2, TM1, TM2)
          // current_type_name.append(std::to_wstring(type_token));
          break;
        }

        case ELEMENT_TYPE_GENERICINST: {
          // skip past generic type indicator token
          current_index++;
          // skip past actual generic type token (probably a class)
          current_index++;
          const auto generic_type_data = RetrieveTypeForSignature(
              metadata_import, function_info, current_index, token_length);
          auto type_name = generic_type_data.name;
          current_type_name.append(type_name);
          current_type_name.append("<"_W);  // Begin generic args

          // Because we are starting a new generic, decrement any existing level
          if (!generic_arg_stack.empty()) {
            generic_arg_stack.top()--;
          }

          // figure out how many generic args this type has
          const auto index_of_tick = type_name.find_last_of('`');
          auto num_args_text = ToString(type_name.substr(index_of_tick + 1));
          auto actual_arg_count = std::stoi(num_args_text, nullptr);
          generic_arg_stack.push(actual_arg_count);
          current_index += token_length;
          // Next will be the variants
          continue;
        }

        case ELEMENT_TYPE_BYREF: {
          // TODO: This hasn't been encountered yet
          current_type_name.append("ref"_W);
          break;
        }

        case ELEMENT_TYPE_END: {
          // we already handle the generic by counting args
          continue;
        }

        default: {
          // This is unexpected and we should report that, and not instrument
          current_type_name.append(ToWSTRING(ToString(cor_element_type)));
          break;
        }
      }

      if (!append_to_type.empty()) {
        current_type_name.append(append_to_type);
        append_to_type = ""_W;
      }

      if (!generic_arg_stack.empty()) {
        // decrement this level's args
        generic_arg_stack.top()--;

        if (generic_arg_stack.top() > 0) {
          // we're in the middle of generic type args
          current_type_name.append(", "_W);
        }
      }

      while (!generic_arg_stack.empty() && generic_arg_stack.top() == 0) {
        // unwind the generics with no args left
        generic_arg_stack.pop();
        current_type_name.append(">"_W);

        if (!generic_arg_stack.empty() && generic_arg_stack.top() > 0) {
          // We are in a nested generic and we need a comma to separate args
          current_type_name.append(", "_W);
        }
      }

      if (!generic_arg_stack.empty()) {
        continue;
      }

      if (current_type_index >= expected_number_of_types) {
        // We missed something, drop out for safety
        return false;
      }

      type_names[current_type_index] = current_type_name;
      current_type_name = ""_W;
      current_type_index++;
    }

    signature_result = type_names;

  } catch (...) {
    // TODO: Add precise exceptions and log
    // We were unable to parse for some reason
    // Return that we've failed
    return false;
  }

  return true;
}

HRESULT CreateAssemblyRefToMscorlib(const ComPtr<IMetaDataAssemblyEmit>& assembly_emit, mdAssemblyRef* mscorlib_ref) {
  // Define an AssemblyRef to mscorlib, needed to create TypeRefs later
  ASSEMBLYMETADATA metadata{};
  metadata.usMajorVersion = 4;
  metadata.usMinorVersion = 0;
  metadata.usBuildNumber = 0;
  metadata.usRevisionNumber = 0;
  BYTE public_key[] = {0xB7, 0x7A, 0x5C, 0x56, 0x19, 0x34, 0xE0, 0x89};
  HRESULT hr = assembly_emit->DefineAssemblyRef(public_key, sizeof(public_key),
                                   "mscorlib"_W.c_str(), &metadata, NULL, 0, 0,
                                   mscorlib_ref);

  return hr;
}

bool ReturnTypeTokenforValueTypeElementType(PCCOR_SIGNATURE p_sig,
                                            const ComPtr<IMetaDataEmit2>& metadata_emit,
                                            const ComPtr<IMetaDataAssemblyEmit>& assembly_emit,
                                            mdToken* ret_type_token) {
  const auto cor_element_type = CorElementType(*p_sig);
  WSTRING managed_type_name = ""_W;

  switch (cor_element_type) {
    case ELEMENT_TYPE_VALUETYPE: {
      ULONG result;
      result = CorSigUncompressToken(p_sig + 1, ret_type_token);
      if (result == -1) {
        Warn("[trace::ReturnTypeTokenforElementType] ELEMENT_TYPE_VALUETYPE failed to find uncompress TypeRef or TypeDef");
        return false;
      }

      return true;
    }

    case ELEMENT_TYPE_VOID:     // 0x01  // System.Void (struct)
      managed_type_name = "System.Void"_W;
      break;
    case ELEMENT_TYPE_BOOLEAN:  // 0x02  // System.Boolean (struct)
      managed_type_name = "System.Boolean"_W;
      break;
    case ELEMENT_TYPE_CHAR:     // 0x03  // System.Char (struct)
      managed_type_name = "System.Char"_W;
      break;
    case ELEMENT_TYPE_I1:       // 0x04  // System.SByte (struct)
      managed_type_name = "System.SByte"_W;
      break;
    case ELEMENT_TYPE_U1:       // 0x05  // System.Byte (struct)
      managed_type_name = "System.Byte"_W;
      break;
    case ELEMENT_TYPE_I2:       // 0x06  // System.Int16 (struct)
      managed_type_name = "System.Int16"_W;
      break;
    case ELEMENT_TYPE_U2:       // 0x07  // System.UInt16 (struct)
      managed_type_name = "System.UInt16"_W;
      break;
    case ELEMENT_TYPE_I4:       // 0x08  // System.Int32 (struct)
      managed_type_name = "System.Int32"_W;
      break;
    case ELEMENT_TYPE_U4:       // 0x09  // System.UInt32 (struct)
      managed_type_name = "System.UInt32"_W;
      break;
    case ELEMENT_TYPE_I8:       // 0x0a  // System.Int64 (struct)
      managed_type_name = "System.Int64"_W;
      break;
    case ELEMENT_TYPE_U8:       // 0x0b  // System.UInt64 (struct)
      managed_type_name = "System.UInt64"_W;
      break;
    case ELEMENT_TYPE_R4:       // 0x0c  // System.Single (struct)
      managed_type_name = "System.Single"_W;
      break;
    case ELEMENT_TYPE_R8:       // 0x0d  // System.Double (struct)
      managed_type_name = "System.Double"_W;
      break;
    case ELEMENT_TYPE_TYPEDBYREF:  // 0X16  // System.TypedReference (struct)
      managed_type_name = "System.TypedReference"_W;
      break;
    case ELEMENT_TYPE_I:           // 0x18  // System.IntPtr (struct)
      managed_type_name = "System.IntPtr"_W;
      break;
    case ELEMENT_TYPE_U:           // 0x19  // System.UIntPtr (struct)
      managed_type_name = "System.UIntPtr"_W;
      break;
    default:
      return false;
  }

  // Create reference to Mscorlib
  mdModuleRef mscorlib_ref;
  HRESULT hr;
  hr = CreateAssemblyRefToMscorlib(assembly_emit, &mscorlib_ref);

  if (FAILED(hr)) {
    Warn("[trace::ReturnTypeTokenforElementType] failed to define AssemblyRef to mscorlib");
    return false;
  }

  // Create/Get TypeRef to the listed type
  if (managed_type_name == ""_W) {
    Warn("[trace::ReturnTypeTokenforElementType] no managed type name given");
    return false;
  }

  hr = metadata_emit->DefineTypeRefByName(
      mscorlib_ref, managed_type_name.c_str(), ret_type_token);

  if (FAILED(hr)) {
    Warn("[trace::ReturnTypeTokenforElementType] unable to create type ref for managed_type_name=", managed_type_name);
    return false;
  }

  return true;
}

bool ReturnTypeIsValueTypeOrGeneric(
                      const ComPtr<IMetaDataImport2>& metadata_import,
                      const ComPtr<IMetaDataEmit2>& metadata_emit,
                      const ComPtr<IMetaDataAssemblyEmit>& assembly_emit,
                      const mdToken targetFunctionToken,
                      const MethodSignature targetFunctionSignature,
                      mdToken* ret_type_token) {

  // MethodDefSig Format: [[HASTHIS] [EXPLICITTHIS]] (DEFAULT|VARARG|GENERIC GenParamCount) ParamCount RetType Param* [SENTINEL Param+]
  const auto generic_count = targetFunctionSignature.NumberOfTypeArguments();
  size_t method_def_sig_index = generic_count == 0 ? 2 : 3;  // Initialize the index to point to RetType
  auto ret_type_byte = targetFunctionSignature.data[method_def_sig_index];
  const auto ret_type = CorElementType(ret_type_byte);

  switch (ret_type) {
    case ELEMENT_TYPE_VOID:
      // No object is returned, so return false.
      return false;

    case ELEMENT_TYPE_GENERICINST: {
      // Format: GENERICINST (CLASS | VALUETYPE) TypeDefOrRefEncoded GenArgCount Type *
      // Example: Task<HttpResponseMessage>. Return true if the type is a VALUETYPE
      if (targetFunctionSignature.data[method_def_sig_index + 1] != ELEMENT_TYPE_VALUETYPE) {
        return false;
      }

      PCCOR_SIGNATURE p_start_byte = PCCOR_SIGNATURE(&targetFunctionSignature.data[method_def_sig_index]);
      PCCOR_SIGNATURE p_end_byte = p_start_byte;
      if (!ParseType(&p_end_byte)) {
        return false;
      }

      size_t length = p_end_byte - p_start_byte;
      HRESULT hr = metadata_emit->GetTokenFromTypeSpec(p_start_byte, (ULONG) length, ret_type_token);
      return SUCCEEDED(hr);
    }

    case ELEMENT_TYPE_VAR:
    case ELEMENT_TYPE_MVAR: {
      // Format: VAR number
      // Format: MVAR number

      // Extract the number, which is an index into the generic type arguments of the method or the type
      method_def_sig_index++; // Advance the current_index to point to "number"
      ULONG generic_type_index;
      if (CorSigUncompressData(PCCOR_SIGNATURE(&targetFunctionSignature.data[method_def_sig_index]),
                                &generic_type_index) == -1) {
        Warn("[trace::ReturnTypeIsValueTypeOrGeneric] element_type=", ret_type, ": unable to read VAR|MVAR index");
        return false;
      }

      // Get the signature of the MethodSpec or the method's parent TypeSpec
      // Each spec will clearly list the types used for the generic type variables
      const auto token_type = TypeFromToken(targetFunctionToken);
      mdToken parent_token = mdTokenNil;
      HRESULT hr;
      PCCOR_SIGNATURE spec_signature{};
      ULONG spec_signature_length{};

      switch (token_type) {
        case mdtMemberRef:
          // The compiler will never make method calls to generic methods without
          // the generic context, so we never expect to hit this at
          // run-time. If we are evaluating the MethodDef/MethodRef of a generic
          // method return false because it is invalid.
          if (generic_count > 0) {
            return false;
          }

          hr = metadata_import->GetMemberRefProps(targetFunctionToken,
                                                  &parent_token, nullptr, 0,
                                                  nullptr, nullptr, nullptr);
          if (SUCCEEDED(hr)) {
            hr = metadata_import->GetTypeSpecFromToken(parent_token, &spec_signature,
                                                  &spec_signature_length);
          }
          break;
        case mdtMethodDef:
          // The compiler will never make method calls to generic methods without
          // the generic context, so we never expect to hit this at
          // run-time. If we are evaluating the MethodDef/MethodRef of a generic
          // method return false because it is invalid.
          if (generic_count > 0) {
            return false;
          }

          hr = metadata_import->GetMemberProps(
              targetFunctionToken, &parent_token, nullptr, 0, nullptr,
              nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr,
              nullptr);
          if (SUCCEEDED(hr)) {
            hr = metadata_import->GetTypeSpecFromToken(parent_token, &spec_signature,
                                                  &spec_signature_length);
          }
          break;
        case mdtMethodSpec:
          hr = metadata_import->GetMethodSpecProps(targetFunctionToken,
                                                  &parent_token, &spec_signature, &spec_signature_length);
          break;
        default:
          Warn("[trace::ReturnTypeIsValueTypeOrGeneric] element_type=", ret_type, ": function token was not a MemberRef, MethodDef, or MethodSpec");
          return false;
      }

      if (FAILED(hr)) {
        Warn("[trace::ReturnTypeIsValueTypeOrGeneric] element_type=", ret_type, ": failed to get parent token or signature");
        return false;
      }

      // Determine the index of GenArgCount in the signature
      size_t parent_token_index;
      if (token_type == mdtMemberRef || token_type == mdtMethodDef) {
        // TypeSpec Format: GENERICINST (CLASS | VALUETYPE) TypeDefOrRefEncoded GenArgCount Type Type*
        // Skip over TypeDefOrRefEncoded by parsing the signature at index 2
        parent_token_index = 2;
        mdToken dummy_token;
        ULONG token_length = CorSigUncompressToken(
            &spec_signature[parent_token_index], &dummy_token);
        parent_token_index += token_length;
      } else if (token_type == mdtMethodSpec) {
        // MethodSpec Format: GENRICINST GenArgCount Type Type*
        parent_token_index = 1;
      } else {
        Warn("[trace::ReturnTypeIsValueTypeOrGeneric] element_type=", ret_type, ": token_type (", token_type , ") not recognized");
        return false;
      }

      // Read the value of GenArgCount in the signature
      ULONG num_generic_arguments;
      parent_token_index += CorSigUncompressData(
          &spec_signature[parent_token_index], &num_generic_arguments);

      // Get a pointer to first type after GenArgCount that we can increment to read the signature
      PCCOR_SIGNATURE p_current_byte = spec_signature + parent_token_index;

      // Iterate to specified generic type argument index and return the appropriate class token or TypeSpec
      for (size_t i = 0; i < num_generic_arguments; i++) {
        if (i != generic_type_index) {
          if (!ParseType(&p_current_byte)) {
            Warn(
                "[trace::ReturnTypeIsValueTypeOrGeneric] element_type=", ret_type, ": Unable to parse "
                "generic type argument ", i,
                "from signature of parent_token:", parent_token);
            return false;
          }
        } else if (*p_current_byte == ELEMENT_TYPE_MVAR ||
                    *p_current_byte == ELEMENT_TYPE_VAR) {
          // The method was defined with a method-level generic type argument from the caller. Return the TypeSpec token for the `M#` MVAR description, or
          // The method was defined with a type-level generic type argument from the caller. Return the TypeSpec token for the `T#` VAR description
          hr = metadata_emit->GetTokenFromTypeSpec(p_current_byte, 2,
                                              ret_type_token);
          return SUCCEEDED(hr);
        } else if (*p_current_byte == ELEMENT_TYPE_GENERICINST) {
          // Format: GENERICINST (CLASS | VALUETYPE) TypeDefOrRefEncoded GenArgCount Type *
          // Example: Task<HttpResponseMessage>. Return true if the type is a VALUETYPE
          if (*(p_current_byte + 1) != ELEMENT_TYPE_VALUETYPE) {
            return false;
          }

          PCCOR_SIGNATURE p_start_byte = p_current_byte;
          PCCOR_SIGNATURE p_end_byte = p_start_byte;
          if (!ParseType(&p_end_byte)) {
            return false;
          }

          size_t length = p_end_byte - p_start_byte;
          HRESULT hr = metadata_emit->GetTokenFromTypeSpec(p_start_byte, (ULONG) length,
                                                           ret_type_token);
          return SUCCEEDED(hr);
        } else {
          return ReturnTypeTokenforValueTypeElementType(
              p_current_byte,
              metadata_emit,
              assembly_emit,
              ret_type_token);
        }
      }

      return false;
    }

    default:
      return ReturnTypeTokenforValueTypeElementType(
          PCCOR_SIGNATURE(&targetFunctionSignature.data[method_def_sig_index]),
          metadata_emit,
          assembly_emit,
          ret_type_token);
  }
}
}  // namespace trace
