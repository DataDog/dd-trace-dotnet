﻿#ifndef DD_CLR_PROFILER_MODULE_METADATA_H_
#define DD_CLR_PROFILER_MODULE_METADATA_H_

#include <corhlpr.h>
#include <unordered_map>
#include <unordered_set>

#include "calltarget_tokens.h"
#include "clr_helpers.h"
#include "com_ptr.h"
#include "integration.h"
#include "string.h"

namespace trace
{

class ModuleMetadata
{
private:
    std::unordered_map<WSTRING, mdMemberRef> wrapper_refs{};
    std::unordered_map<WSTRING, mdTypeRef> wrapper_parent_type{};
    std::unordered_set<WSTRING> failed_wrapper_keys{};
    std::unique_ptr<CallTargetTokens> calltargetTokens = nullptr;

public:
    const ComPtr<IMetaDataImport2> metadata_import{};
    const ComPtr<IMetaDataEmit2> metadata_emit{};
    const ComPtr<IMetaDataAssemblyImport> assembly_import{};
    const ComPtr<IMetaDataAssemblyEmit> assembly_emit{};
    const WSTRING assemblyName = EmptyWStr;
    const AppDomainID app_domain_id;
    const GUID module_version_id;
    const std::vector<IntegrationMethod> integrations = {};
    const AssemblyProperty* corAssemblyProperty = nullptr;

    ModuleMetadata(ComPtr<IMetaDataImport2> metadata_import, ComPtr<IMetaDataEmit2> metadata_emit,
                   ComPtr<IMetaDataAssemblyImport> assembly_import, ComPtr<IMetaDataAssemblyEmit> assembly_emit,
                   const WSTRING& assembly_name, const AppDomainID app_domain_id, const GUID module_version_id,
                   const std::vector<IntegrationMethod>& integrations, AssemblyProperty* corAssemblyProperty) :
        metadata_import(metadata_import),
        metadata_emit(metadata_emit),
        assembly_import(assembly_import),
        assembly_emit(assembly_emit),
        assemblyName(assembly_name),
        app_domain_id(app_domain_id),
        module_version_id(module_version_id),
        integrations(integrations),
        corAssemblyProperty(corAssemblyProperty)
    {
    }

    bool TryGetWrapperMemberRef(const WSTRING& keyIn, mdMemberRef& valueOut) const
    {
        const auto search = wrapper_refs.find(keyIn);

        if (search != wrapper_refs.end())
        {
            valueOut = search->second;
            return true;
        }

        return false;
    }

    bool TryGetWrapperParentTypeRef(const WSTRING& keyIn, mdTypeRef& valueOut) const
    {
        const auto search = wrapper_parent_type.find(keyIn);

        if (search != wrapper_parent_type.end())
        {
            valueOut = search->second;
            return true;
        }

        return false;
    }

    bool IsFailedWrapperMemberKey(const WSTRING& key) const
    {
        const auto search = failed_wrapper_keys.find(key);

        if (search != failed_wrapper_keys.end())
        {
            return true;
        }

        return false;
    }

    void SetWrapperMemberRef(const WSTRING& keyIn, const mdMemberRef valueIn)
    {
        wrapper_refs[keyIn] = valueIn;
    }

    void SetWrapperParentTypeRef(const WSTRING& keyIn, const mdTypeRef valueIn)
    {
        wrapper_parent_type[keyIn] = valueIn;
    }

    void SetFailedWrapperMemberKey(const WSTRING& key)
    {
        failed_wrapper_keys.insert(key);
    }

    std::vector<MethodReplacement> GetMethodReplacementsForCaller(const trace::FunctionInfo& caller)
    {
        std::vector<MethodReplacement> enabled;
        for (auto& i : integrations)
        {
            if ((i.replacement.caller_method.type_name.empty() ||
                 i.replacement.caller_method.type_name == caller.type.name) &&
                (i.replacement.caller_method.method_name.empty() ||
                 i.replacement.caller_method.method_name == caller.name))
            {
                enabled.push_back(i.replacement);
            }
        }
        return enabled;
    }

    CallTargetTokens* GetCallTargetTokens()
    {
        if (calltargetTokens == nullptr)
        {
            calltargetTokens = std::make_unique<CallTargetTokens>(this);
        }
        return calltargetTokens.get();
    }
};

} // namespace trace

#endif // DD_CLR_PROFILER_MODULE_METADATA_H_
