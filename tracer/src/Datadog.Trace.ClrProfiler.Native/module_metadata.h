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
    std::unique_ptr<std::unordered_map<WSTRING, mdTypeRef>> wrapper_parent_type = nullptr;
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

    void SetWrapperParentTypeRef(const WSTRING& keyIn, const mdTypeRef valueIn)
    {
        std::scoped_lock<std::mutex> lock(wrapper_mutex);
        if (wrapper_parent_type == nullptr)
        {
            wrapper_parent_type = std::make_unique<std::unordered_map<WSTRING, mdTypeRef>>();
        }

        (*wrapper_parent_type)[keyIn] = valueIn;
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
