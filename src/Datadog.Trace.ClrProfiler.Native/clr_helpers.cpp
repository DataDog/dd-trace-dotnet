#include "clr_helpers.h"

namespace trace {

std::wstring GetAssemblyName(ICorProfilerInfo3* info,
                             const AssemblyID& assembly_id) {
  std::wstring name(kNameMaxSize, 0);
  unsigned long name_len = 0;
  auto hr = info->GetAssemblyInfo(assembly_id, (unsigned long)(name.size()),
                                  &name_len, name.data(), nullptr, nullptr);
  if (FAILED(hr) || name_len == 0) {
    return L"";
  }
  return name.substr(0, name_len - 1);
}

std::wstring GetAssemblyName(
    const ComPtr<IMetaDataAssemblyImport>& assembly_import,
    const mdAssemblyRef& assembly_ref) {
  std::wstring name(kNameMaxSize, 0);
  unsigned long name_len = 0;
  ASSEMBLYMETADATA assembly_metadata{};
  unsigned long assembly_flags = 0;
  auto hr = assembly_import->GetAssemblyRefProps(
      assembly_ref, nullptr, nullptr, name.data(), (unsigned long)(name.size()),
      &name_len, &assembly_metadata, nullptr, nullptr, &assembly_flags);
  if (FAILED(hr) || name_len == 0) {
    return L"";
  }
  return name.substr(0, name_len - 1);
}

FunctionInfo GetFunctionInfo(const ComPtr<IMetaDataImport>& metadata_import,
                             const mdToken& function_id) {
  mdTypeDef type_def = mdTypeDefNil;
  std::wstring function_name(kNameMaxSize, 0);
  unsigned long function_name_len = 0;
  auto hr = metadata_import->GetMemberProps(
      function_id, &type_def, function_name.data(),
      (unsigned long)(function_name.size()), &function_name_len, nullptr,
      nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr);
  if (FAILED(hr) || function_name_len == 0) {
    return {};
  }
  function_name = function_name.substr(0, function_name_len - 1);
  return {function_id, function_name, GetTypeInfo(metadata_import, type_def)};
}

TypeInfo GetTypeInfo(const ComPtr<IMetaDataImport>& metadata_import,
                     const mdToken& type_id) {
  std::wstring type_name(kNameMaxSize, 0);
  unsigned long type_name_len = 0;
  auto hr = metadata_import->GetTypeDefProps(type_id, type_name.data(),
                                             (unsigned long)(type_name.size()),
                                             &type_name_len, nullptr, nullptr);
  if (FAILED(hr) || type_name_len == 0) {
    return {};
  }
  type_name = type_name.substr(0, type_name_len - 1);
  return {type_id, type_name};
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

}  // namespace trace
