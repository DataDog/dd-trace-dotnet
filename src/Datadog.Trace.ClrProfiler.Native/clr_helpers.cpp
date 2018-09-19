#include "clr_helpers.h"
#include "logging.h"
#include "macros.h"
#include "util.h"

namespace trace {

AssemblyInfo GetAssemblyInfo(ICorProfilerInfo3* info,
                             const AssemblyID& assembly_id) {
  std::wstring name(kNameMaxSize, 0);
  DWORD name_len = 0;
  auto hr = info->GetAssemblyInfo(assembly_id, (DWORD)(name.size()), &name_len,
                                  name.data(), nullptr, nullptr);
  if (FAILED(hr) || name_len == 0) {
    return {};
  }
  name = name.substr(0, name_len - 1);
  return {assembly_id, name};
}

std::wstring GetAssemblyName(
    const ComPtr<IMetaDataAssemblyImport>& assembly_import) {
  mdAssembly current = mdAssemblyNil;
  auto hr = assembly_import->GetAssemblyFromScope(&current);
  if (FAILED(hr)) {
    return L"";
  }
  std::wstring name(kNameMaxSize, 0);
  DWORD name_len = 0;
  ASSEMBLYMETADATA assembly_metadata{};
  DWORD assembly_flags = 0;
  hr = assembly_import->GetAssemblyProps(
      current, nullptr, nullptr, nullptr, name.data(), (DWORD)(name.size()),
      &name_len, &assembly_metadata, &assembly_flags);
  if (FAILED(hr) || name_len == 0) {
    return L"";
  }
  return name.substr(0, name_len - 1);
}

std::wstring GetAssemblyName(
    const ComPtr<IMetaDataAssemblyImport>& assembly_import,
    const mdAssemblyRef& assembly_ref) {
  std::wstring name(kNameMaxSize, 0);
  DWORD name_len = 0;
  ASSEMBLYMETADATA assembly_metadata{};
  DWORD assembly_flags = 0;
  const auto hr = assembly_import->GetAssemblyRefProps(
      assembly_ref, nullptr, nullptr, name.data(), (DWORD)(name.size()),
      &name_len, &assembly_metadata, nullptr, nullptr, &assembly_flags);
  if (FAILED(hr) || name_len == 0) {
    return L"";
  }
  return name.substr(0, name_len - 1);
}

FunctionInfo GetFunctionInfo(const ComPtr<IMetaDataImport2>& metadata_import,
                             const mdToken& token) {
  mdToken parent_token = mdTokenNil;
  std::wstring function_name(kNameMaxSize, 0);
  DWORD function_name_len = 0;

  PCCOR_SIGNATURE raw_signature;
  ULONG raw_signature_len;

  HRESULT hr = E_FAIL;
  switch (TypeFromToken(token)) {
    case mdtMemberRef:
      hr = metadata_import->GetMemberRefProps(
          token, &parent_token, function_name.data(),
          (DWORD)(function_name.size()), &function_name_len, &raw_signature,
          &raw_signature_len);
      break;
    case mdtMethodDef:
      hr = metadata_import->GetMemberProps(
          token, &parent_token, function_name.data(),
          (DWORD)(function_name.size()), &function_name_len, nullptr,
          &raw_signature, &raw_signature_len, nullptr, nullptr, nullptr,
          nullptr, nullptr);
      break;
    case mdtMethodSpec:
      hr = metadata_import->GetMethodSpecProps(token, &parent_token, nullptr,
                                               nullptr);
      if (!FAILED(hr)) {
        return GetFunctionInfo(metadata_import, parent_token);
      }
    default:
      logger->error("unknown token type: {:x}", TypeFromToken(token));
      break;
  }
  if (FAILED(hr) || function_name_len == 0) {
    return {};
  }
  function_name = function_name.substr(0, function_name_len - 1);

  std::vector<BYTE> signature(raw_signature_len);
  for (ULONG i = 0; i < raw_signature_len; i++) {
    signature[i] = raw_signature[i];
  }

  // parent_token could be: TypeDef, TypeRef, TypeSpec, ModuleRef, MethodDef

  return {token, function_name, GetTypeInfo(metadata_import, parent_token),
          signature};
}

ModuleInfo GetModuleInfo(ICorProfilerInfo3* info, const ModuleID& module_id) {
  std::wstring module_path(260, 0);
  DWORD module_path_len = 0;
  LPCBYTE base_load_address;
  AssemblyID assembly_id = 0;
  DWORD module_flags = 0;
  const HRESULT hr = info->GetModuleInfo2(
      module_id, &base_load_address, (DWORD)(module_path.size()),
      &module_path_len, module_path.data(), &assembly_id, &module_flags);
  if (FAILED(hr) || module_path_len == 0) {
    return {};
  }
  return {module_id, module_path, GetAssemblyInfo(info, assembly_id),
          module_flags};
}

TypeInfo GetTypeInfo(const ComPtr<IMetaDataImport2>& metadata_import,
                     const mdToken& token) {
  mdToken parent_token = mdTokenNil;
  std::wstring type_name(kNameMaxSize, 0);
  DWORD type_name_len = 0;

  HRESULT hr = E_FAIL;
  const auto token_type = TypeFromToken(token);
  switch (token_type) {
    case mdtTypeDef:
      hr = metadata_import->GetTypeDefProps(token, type_name.data(),
                                            (DWORD)(type_name.size()),
                                            &type_name_len, nullptr, nullptr);
      break;
    case mdtTypeRef:
      hr = metadata_import->GetTypeRefProps(
          token, &parent_token, type_name.data(), (DWORD)(type_name.size()),
          &type_name_len);
      break;
    case mdtTypeSpec:
      // do we need to handle this case?
      break;
    case mdtModuleRef:
      metadata_import->GetModuleRefProps(
          token, type_name.data(), (DWORD)(type_name.size()), &type_name_len);
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
  type_name = type_name.substr(0, type_name_len - 1);

  return {token, type_name};
}

mdAssemblyRef FindAssemblyRef(
    const ComPtr<IMetaDataAssemblyImport>& assembly_import,
    const std::wstring& assembly_name) {
  for (mdAssemblyRef assembly_ref : EnumAssemblyRefs(assembly_import)) {
    if (GetAssemblyName(assembly_import, assembly_ref) == assembly_name) {
      return assembly_ref;
    }
  }
  return mdAssemblyRefNil;
}

std::vector<Integration> FilterIntegrationsByCaller(
    const std::vector<Integration>& integrations,
    const std::wstring& assembly_name) {
  std::vector<Integration> enabled;

  for (auto& i : integrations) {
    bool found = false;
    for (auto& mr : i.method_replacements) {
      if (mr.caller_method.assembly.name.empty() ||
          mr.caller_method.assembly.name == assembly_name) {
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

std::vector<Integration> FilterIntegrationsByTarget(
    const std::vector<Integration>& integrations,
    const ComPtr<IMetaDataAssemblyImport>& assembly_import) {
  std::vector<Integration> enabled;

  const auto assembly_name = GetAssemblyName(assembly_import);

  for (auto& i : integrations) {
    bool found = false;
    for (auto& mr : i.method_replacements) {
      if (mr.target_method.assembly.name == assembly_name) {
        found = true;
        break;
      }
      for (auto& assembly_ref : EnumAssemblyRefs(assembly_import)) {
        auto ref_name = GetAssemblyName(assembly_import, assembly_ref);
        if (mr.target_method.assembly.name == ref_name) {
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

}  // namespace trace
