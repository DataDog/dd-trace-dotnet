#ifndef DD_CLR_PROFILER_MODULE_METADATA_H_
#define DD_CLR_PROFILER_MODULE_METADATA_H_

#include <corhlpr.h>
#include <unordered_map>

#include "clr_helpers.h"
#include "com_ptr.h"
#include "integration.h"

namespace trace {

class ModuleMetadata {
 private:
  std::unordered_map<std::wstring, mdMemberRef> wrapper_refs{};
  std::unordered_map<std::wstring, mdTypeRef> wrapper_parent_type{};

 public:
  const ComPtr<IMetaDataImport2> metadata_import{};
  const ComPtr<IMetaDataEmit2> metadata_emit{};
  std::wstring assemblyName = L"";
  std::vector<Integration> integrations = {};

  ModuleMetadata(ComPtr<IMetaDataImport2> metadata_import,
                 ComPtr<IMetaDataEmit2> metadata_emit,
                 std::wstring assembly_name,
                 std::vector<Integration> integrations)
      : metadata_import(metadata_import),
        metadata_emit(metadata_emit),
        assemblyName(assembly_name),
        integrations(integrations) {}

  bool TryGetWrapperMemberRef(const std::wstring& keyIn,
                              mdMemberRef& valueOut) const {
    const auto search = wrapper_refs.find(keyIn);

    if (search != wrapper_refs.end()) {
      valueOut = search->second;
      return true;
    }

    return false;
  }

  bool TryGetWrapperParentTypeRef(const std::wstring& keyIn,
                                  mdTypeRef& valueOut) const {
    const auto search = wrapper_parent_type.find(keyIn);

    if (search != wrapper_parent_type.end()) {
      valueOut = search->second;
      return true;
    }

    return false;
  }

  void SetWrapperMemberRef(const std::wstring& keyIn,
                           const mdMemberRef valueIn) {
    wrapper_refs[keyIn] = valueIn;
  }

  void SetWrapperParentTypeRef(const std::wstring& keyIn,
                               const mdTypeRef valueIn) {
    wrapper_parent_type[keyIn] = valueIn;
  }

  inline std::vector<MethodReplacement> GetMethodReplacementsForCaller(
      const trace::FunctionInfo& caller) {
    std::vector<MethodReplacement> enabled;
    for (auto& i : integrations) {
      for (auto& mr : i.method_replacements) {
        if ((mr.caller_method.type_name.empty() ||
             mr.caller_method.type_name == caller.type.name) &&
            (mr.caller_method.method_name.empty() ||
             mr.caller_method.method_name == caller.name)) {
          enabled.push_back(mr);
        }
      }
    }
    return enabled;
  }
};

}  // namespace trace

#endif  // DD_CLR_PROFILER_MODULE_METADATA_H_
