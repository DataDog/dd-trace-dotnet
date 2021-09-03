#ifndef DD_CLR_PROFILER_MODULE_METADATA_H_
#define DD_CLR_PROFILER_MODULE_METADATA_H_

#include <corhlpr.h>
#include <mutex>
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
    std::mutex wrapper_mutex;
    std::unique_ptr<std::unordered_map<WSTRING, mdMemberRef>> wrapper_refs = nullptr;
    std::unique_ptr<std::unordered_map<WSTRING, mdTypeRef>> wrapper_parent_type = nullptr;
    std::unique_ptr<std::unordered_set<WSTRING>> failed_wrapper_keys = nullptr;
    std::unique_ptr<CallTargetTokens> calltargetTokens = nullptr;
    std::unique_ptr<std::vector<IntegrationMethod>> integrations = nullptr;

public:
    const ComPtr<IMetaDataImport2> metadata_import{};
    const ComPtr<IMetaDataEmit2> metadata_emit{};
    const ComPtr<IMetaDataAssemblyImport> assembly_import{};
    const ComPtr<IMetaDataAssemblyEmit> assembly_emit{};
    const WSTRING assemblyName = EmptyWStr;
    const AppDomainID app_domain_id;
    const GUID module_version_id;
    const AssemblyProperty* corAssemblyProperty = nullptr;

    ModuleMetadata(ComPtr<IMetaDataImport2> metadata_import, ComPtr<IMetaDataEmit2> metadata_emit,
                   ComPtr<IMetaDataAssemblyImport> assembly_import, ComPtr<IMetaDataAssemblyEmit> assembly_emit,
                   const WSTRING& assembly_name, const AppDomainID app_domain_id, const GUID module_version_id,
                   std::unique_ptr<std::vector<IntegrationMethod>>&& integrations,
                   const AssemblyProperty* corAssemblyProperty) :
        metadata_import(metadata_import),
        metadata_emit(metadata_emit),
        assembly_import(assembly_import),
        assembly_emit(assembly_emit),
        assemblyName(assembly_name),
        app_domain_id(app_domain_id),
        module_version_id(module_version_id),
        integrations(std::move(integrations)),
        corAssemblyProperty(corAssemblyProperty)
    {
    }

    ModuleMetadata(ComPtr<IMetaDataImport2> metadata_import, ComPtr<IMetaDataEmit2> metadata_emit,
                   ComPtr<IMetaDataAssemblyImport> assembly_import, ComPtr<IMetaDataAssemblyEmit> assembly_emit,
                   const WSTRING& assembly_name, const AppDomainID app_domain_id, const AssemblyProperty* corAssemblyProperty) :
        metadata_import(metadata_import),
        metadata_emit(metadata_emit),
        assembly_import(assembly_import),
        assembly_emit(assembly_emit),
        assemblyName(assembly_name),
        app_domain_id(app_domain_id),
        module_version_id(),
        corAssemblyProperty(corAssemblyProperty)
    {
    }

    bool TryGetWrapperMemberRef(const WSTRING& keyIn, mdMemberRef& valueOut) const
    {
        if (wrapper_refs == nullptr)
        {
            return false;
        }

        const auto search = wrapper_refs->find(keyIn);

        if (search != wrapper_refs->end())
        {
            valueOut = search->second;
            return true;
        }

        return false;
    }

    bool TryGetWrapperParentTypeRef(const WSTRING& keyIn, mdTypeRef& valueOut) const
    {
        if (wrapper_parent_type == nullptr)
        {
            return false;
        }

        const auto search = wrapper_parent_type->find(keyIn);

        if (search != wrapper_parent_type->end())
        {
            valueOut = search->second;
            return true;
        }

        return false;
    }

    bool IsFailedWrapperMemberKey(const WSTRING& key) const
    {
        if (failed_wrapper_keys == nullptr)
        {
            return false;
        }

        const auto search = failed_wrapper_keys->find(key);

        if (search != failed_wrapper_keys->end())
        {
            return true;
        }

        return false;
    }

    void SetWrapperMemberRef(const WSTRING& keyIn, const mdMemberRef valueIn)
    {
        std::scoped_lock<std::mutex> lock(wrapper_mutex);
        if (wrapper_refs == nullptr)
        {
            wrapper_refs = std::make_unique<std::unordered_map<WSTRING, mdMemberRef>>();
        }

        (*wrapper_refs)[keyIn] = valueIn;
    }

    void SetWrapperParentTypeRef(const WSTRING& keyIn, const mdTypeRef valueIn)
    {
        std::scoped_lock<std::mutex> lock(wrapper_mutex);
        if (wrapper_parent_type == nullptr)
        {
            wrapper_parent_type = std::make_unique<std::unordered_map<WSTRING, mdTypeRef>>();
        }

        (*wrapper_parent_type)[keyIn] = valueIn;
    }

    void SetFailedWrapperMemberKey(const WSTRING& key)
    {
        std::scoped_lock<std::mutex> lock(wrapper_mutex);
        if (failed_wrapper_keys == nullptr)
        {
            failed_wrapper_keys = std::make_unique<std::unordered_set<WSTRING>>();
        }

        failed_wrapper_keys->insert(key);
    }

    std::vector<MethodReplacement> GetMethodReplacementsForCaller(const trace::FunctionInfo& caller)
    {
        std::vector<MethodReplacement> enabled;
        if (integrations == nullptr)
        {
            return enabled;
        }

        for (auto& i : *integrations.get())
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
