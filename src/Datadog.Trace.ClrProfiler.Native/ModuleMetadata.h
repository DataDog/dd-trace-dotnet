#pragma once

#include <corhlpr.h>
#include <unordered_map>
#include "ComPtr.h"
#include "clr_helpers.h"
#include "integration.h"

class ModuleMetadata {
 private:
  std::unordered_map<std::wstring, mdMemberRef> wrapper_refs{};
  std::unordered_map<std::wstring, mdTypeRef> wrapper_parent_type{};

 public:
  const ComPtr<IMetaDataImport> metadata_import{};
  std::wstring assemblyName = L"";
  std::vector<integration> integrations = {};

  ModuleMetadata(ComPtr<IMetaDataImport> metadata_import,
                 std::wstring assembly_name,
                 std::vector<integration> integrations)
      : metadata_import(std::move(metadata_import)),
        assemblyName(std::move(assembly_name)),
        integrations(std::move(integrations)) {}

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

  inline std::vector<method_replacement> GetMethodReplacementsForCaller(
      const trace::FunctionInfo& caller) {
    std::vector<method_replacement> enabled;
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
