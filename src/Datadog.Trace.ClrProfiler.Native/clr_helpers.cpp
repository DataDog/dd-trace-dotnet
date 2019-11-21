#include "clr_helpers.h"

#include <cstring>

#include <set>
#include <stack>
#include "environment_variables.h"
#include "logging.h"
#include "macros.h"
#include "pal.h"

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
  const auto clr_optimizations_enabled =
      GetEnvironmentValue(environment::clr_disable_optimizations);

  if (clr_optimizations_enabled == "1"_W ||
      clr_optimizations_enabled == "true"_W) {
    return true;
  }

  if (clr_optimizations_enabled == "0"_W ||
      clr_optimizations_enabled == "false"_W) {
    return false;
  }

#ifdef _WIN32
  // default to false on Windows
  return false;
#else
  // default to true on Linux
  return true;
#endif
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

int ParseType(PCCOR_SIGNATURE p_sig) {
  const auto cor_element_type = CorElementType(*p_sig);

  switch (cor_element_type) {
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
    case ELEMENT_TYPE_OBJECT:
      return 1;
    case ELEMENT_TYPE_PTR:
      return 1;  // FIX LATER
    case ELEMENT_TYPE_VALUETYPE:
    case ELEMENT_TYPE_CLASS:
      mdToken type_token;
      return 1 + CorSigUncompressToken((p_sig + 1), &type_token);
    case ELEMENT_TYPE_VAR:
    case ELEMENT_TYPE_MVAR:
      return 1 + CorSigUncompressedDataSize(p_sig + 1);
    case ELEMENT_TYPE_FNPTR:
      return 1;  // FIX LATER
    case ELEMENT_TYPE_ARRAY:
      return 1;  // FIX LATER
    case ELEMENT_TYPE_SZARRAY:
      return 1;  // FIX LATER
    case ELEMENT_TYPE_GENERICINST:
      return 1;  // FIX LATER
    default:
      return 0;
  }
}

bool UnboxReturnValue(const ComPtr<IMetaDataImport2>& metadata_import,
                      const ComPtr<IMetaDataEmit2>& metadata_emit,
                      const FunctionInfo& caller_function_info,
                      const FunctionInfo& target_function_info,
                      mdToken* ret_type_token) {

  try {
    // MethodDefSig Format: [[HASTHIS] [EXPLICITTHIS]] (DEFAULT|VARARG|GENERIC GenParamCount) ParamCount RetType Param* [SENTINEL Param+]
    const auto generic_count = target_function_info.signature.NumberOfTypeArguments();
    size_t current_index = 2;  // Initialize the index to the non-Generic index of RetType: 2

    if (generic_count > 0) {
      current_index++;  // Increment the index to account for the additional GenParamCount in the signature
    }

    mdToken type_token;
    ULONG token_length;
    auto param_piece = target_function_info.signature.data[current_index];
    const auto cor_element_type = CorElementType(param_piece);

    switch (cor_element_type) {
      case ELEMENT_TYPE_VAR: {
        // Format: VAR number
        // The return type is defined as a generic type on the target object's type,
        // which needs to be translated to a type that the caller method understands.

        // Extract the number which is an index into the generic type arguments
        current_index++; // Advance the current_index to point to "number"
        ULONG type_arg_index;
        CorSigUncompressData(
            PCCOR_SIGNATURE(
                &target_function_info.signature.data[current_index]),
            &type_arg_index);
        size_t i = 0;

        // Get the token for the owning type, which has the generic type arguments
        const auto token_type = TypeFromToken(target_function_info.id);
        mdToken parent_token = mdTokenNil;
        HRESULT hr;

        switch (token_type) {
          case mdtMemberRef:
            hr = metadata_import->GetMemberRefProps(target_function_info.id,
                                                    &parent_token, nullptr, 0,
                                                    nullptr, nullptr, nullptr);
            break;
          case mdtMethodDef:
            hr = metadata_import->GetMemberProps(
                target_function_info.id, &parent_token, nullptr, 0, nullptr,
                nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr,
                nullptr);
            break;
          default:
            Warn("[trace::UnboxReturnValue] UNHANDLED CASE: ELEMENT_TYPE_VAR: function token was not a mdtMemberRef or mdtMethodDef");
            break;  // TODO see if anything hits this
        }

        // parent_token should be a TypeSpec, so extract its signature
        PCCOR_SIGNATURE signature{};
        ULONG signature_length{};

        hr = metadata_import->GetTypeSpecFromToken(parent_token, &signature,
                                                   &signature_length);
        if (FAILED(hr)) {
          Warn("[trace::UnboxReturnValue] UNHANDLED CASE: ELEMENT_TYPE_VAR: GetTypeSpecFromToken failed");
          return false;
        }

        // Format: GENERICINST (CLASS | VALUETYPE) TypeDefOrRefEncoded GenArgCount Type Type* Skip to the type def
        // Skip to the GenArgCount
        current_index = 2;
        mdToken dummy_token;
        token_length = CorSigUncompressToken(
            PCCOR_SIGNATURE(&signature[current_index]), &dummy_token);
        current_index += token_length;

        // Read value of GenArgCount
        ULONG num_generic_arguments;
        current_index += CorSigUncompressData(
            PCCOR_SIGNATURE(&signature[current_index]), &num_generic_arguments);

        // Iterate to specified generic type argument index and return the appropriate class token
        for (i = 0; i < num_generic_arguments; i++) {
          CorElementType element_type = CorElementType();

          if (i != type_arg_index) {
            current_index += ParseType(&signature[current_index]);
          } else if (signature[current_index] == ELEMENT_TYPE_MVAR ||
                     signature[current_index] == ELEMENT_TYPE_VAR) {
            // Retrieve the corresponding token for the `M#` TypeSpec, if defined on the caller method, or
            // Retrieve the corresponding token for the `T#` TypeSpec, if defined on the caller method's owning type
            hr = metadata_emit->GetTokenFromTypeSpec(&signature[current_index], 2,
                                                ret_type_token);
            return SUCCEEDED(hr);
          } else {
            Warn("[trace::UnboxReturnValue] UNHANDLED CASE: element type of matching generic argument was: ",
                signature[current_index]);
            return false;
          }
        }

        return false;
      }
      case ELEMENT_TYPE_MVAR: {
        // Format: MVAR number
        // The return type is defined as a generic type for the caller method.
        // No additional translation necessary, just retrieve the corresponding
        // token for the `M#` TypeSpec
        auto hr = metadata_emit->GetTokenFromTypeSpec(
            &target_function_info.signature.data[current_index], 2,
            ret_type_token);
        return SUCCEEDED(hr);
      }

      case ELEMENT_TYPE_VOID:            // 0x01
      case ELEMENT_TYPE_BOOLEAN:         // 0x02
      case ELEMENT_TYPE_CHAR:            // 0x03
      case ELEMENT_TYPE_I1:              // 0x04
      case ELEMENT_TYPE_U1:              // 0x05
      case ELEMENT_TYPE_I2:              // 0x06
      case ELEMENT_TYPE_U2:              // 0x07
      case ELEMENT_TYPE_I4:              // 0x08
      case ELEMENT_TYPE_U4:              // 0x09
      case ELEMENT_TYPE_I8:              // 0x0a
      case ELEMENT_TYPE_U8:              // 0x0b
      case ELEMENT_TYPE_R4:              // 0x0c
      case ELEMENT_TYPE_R8:              // 0x0d
      case ELEMENT_TYPE_STRING:          // 0x0e
        return false;

      // case ELEMENT_TYPE_PTR:         // 0x0f
      // case ELEMENT_TYPE_BYREF:       // 0x10

      // case ELEMENT_TYPE_VALUETYPE:   // 0x11  // HANDLED SEPARATELY
      case ELEMENT_TYPE_CLASS:          // 0x12
        return false;
      // case ELEMENT_TYPE_VAR:         // 0x13  // HANDLED SEPARATELY
      // case ELEMENT_TYPE_ARRAY:          // 0x14
      case ELEMENT_TYPE_GENERICINST:    // 0X15  // Ex. Task<HttpResponseMessage>. It would be nice to figure out when this is a VALUETYPE
        current_index++;
        if (target_function_info.signature.data[current_index] == ELEMENT_TYPE_VALUETYPE) {
            Warn("[trace::UnboxReturnValue] UNHANDLED CASE: ELEMENT_TYPE_GENERICINST found whose type is a VALUETYPE");
        }
        return false;
        
      // case ELEMENT_TYPE_TYPEDBYREF:  // 0X16

      // case ELEMENT_TYPE_I:           // 0x18
      // case ELEMENT_TYPE_U:           // 0x19
      // case ELEMENT_TYPE_FNPTR:       // 0x1b
      case ELEMENT_TYPE_OBJECT:         // 0x1c
        return false;
      // case ELEMENT_TYPE_SZARRAY:     // 0x1d

      // case ELEMENT_TYPE_MVAR:        // 0x1e // HANDLED SEPARATELY
      default: {
        Warn("[trace::UnboxReturnValue] UNHANDLED CASE: unexpected CorElementType found: ",
             cor_element_type);
        return false;
      }
    }
  } catch (...) {
    // TODO: Add precise exceptions and log
    // We were unable to parse for some reason
    // Return that we've failed
    return false;
  }

  return false;
}
}  // namespace trace
