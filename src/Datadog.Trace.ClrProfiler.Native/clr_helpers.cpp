#include "clr_helpers.h"

namespace trace {

std::wstring GetAssemblyName(ICorProfilerInfo3* info,
                             const AssemblyID& assembly_id) {
  std::wstring str(512, 0);
  DWORD str_len = 0;
  auto hr = info->GetAssemblyInfo(assembly_id, DWORD(str.size()), &str_len,
                                  str.data(), nullptr, nullptr);
  if (FAILED(hr) || str_len == 0) {
    return L"";
  }
  return str.substr(0, str_len - 1);
}

std::wstring GetAssemblyName(
    const ComPtr<IMetaDataAssemblyImport>& assembly_import,
    const mdAssemblyRef& assembly_ref) {
  const unsigned long str_max = 512;
  std::wstring str(str_max, 0);
  unsigned long str_len = 0;
  ASSEMBLYMETADATA assembly_metadata{};
  DWORD assembly_flags = 0;
  auto hr = assembly_import->GetAssemblyRefProps(
      assembly_ref, nullptr, nullptr, str.data(), str_max, &str_len,
      &assembly_metadata, nullptr, nullptr, &assembly_flags);
  if (FAILED(hr) || str_len == 0) {
    return L"";
  }
  str = str.substr(0, str_len - 1);
  return str;
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
