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
#include "livedebugger_tokens.h"

namespace trace
{

class ModuleMetadata
{
private:
    std::mutex wrapper_mutex;
    std::unique_ptr<std::unordered_map<WSTRING, mdTypeRef>> integration_types = nullptr;
    std::unique_ptr<CallTargetTokens> calltargetTokens = nullptr;
    std::unique_ptr<std::vector<IntegrationDefinition>> integrations = nullptr;
    std::unique_ptr<LiveDebuggerTokens> livedebuggerTokens = nullptr;

public:
    const ComPtr<IMetaDataImport2> metadata_import{};
    const ComPtr<IMetaDataEmit2> metadata_emit{};
    const ComPtr<IMetaDataAssemblyImport> assembly_import{};
    const ComPtr<IMetaDataAssemblyEmit> assembly_emit{};
    const WSTRING assemblyName = EmptyWStr;
    const AppDomainID app_domain_id;
    const GUID module_version_id;
    const AssemblyProperty* corAssemblyProperty = nullptr;
    const bool enable_by_ref_instrumentation = false;
    const bool enable_calltarget_state_by_ref = false;

    ModuleMetadata(ComPtr<IMetaDataImport2> metadata_import, ComPtr<IMetaDataEmit2> metadata_emit,
                   ComPtr<IMetaDataAssemblyImport> assembly_import, ComPtr<IMetaDataAssemblyEmit> assembly_emit,
                   const WSTRING& assembly_name, const AppDomainID app_domain_id, const GUID module_version_id,
                   std::unique_ptr<std::vector<IntegrationDefinition>>&& integrations,
                   const AssemblyProperty* corAssemblyProperty, const bool enableByRefInstrumentation,
                   const bool enableCallTargetStateByRef) :
        metadata_import(metadata_import),
        metadata_emit(metadata_emit),
        assembly_import(assembly_import),
        assembly_emit(assembly_emit),
        assemblyName(assembly_name),
        app_domain_id(app_domain_id),
        module_version_id(module_version_id),
        integrations(std::move(integrations)),
        corAssemblyProperty(corAssemblyProperty),
        enable_by_ref_instrumentation(enableByRefInstrumentation),
        enable_calltarget_state_by_ref(enableCallTargetStateByRef)
    {
    }

    ModuleMetadata(ComPtr<IMetaDataImport2> metadata_import, ComPtr<IMetaDataEmit2> metadata_emit,
                   ComPtr<IMetaDataAssemblyImport> assembly_import, ComPtr<IMetaDataAssemblyEmit> assembly_emit,
                   const WSTRING& assembly_name, const AppDomainID app_domain_id,
                   const AssemblyProperty* corAssemblyProperty, const bool enableByRefInstrumentation,
                   const bool enableCallTargetStateByRef) :
        metadata_import(metadata_import),
        metadata_emit(metadata_emit),
        assembly_import(assembly_import),
        assembly_emit(assembly_emit),
        assemblyName(assembly_name),
        app_domain_id(app_domain_id),
        module_version_id(),
        corAssemblyProperty(corAssemblyProperty),
        enable_by_ref_instrumentation(enableByRefInstrumentation),
        enable_calltarget_state_by_ref(enableCallTargetStateByRef)
    {
    }

    bool TryGetIntegrationTypeRef(const WSTRING& keyIn, mdTypeRef& valueOut) const
    {
        if (integration_types == nullptr)
        {
            return false;
        }

        const auto search = integration_types->find(keyIn);

        if (search != integration_types->end())
        {
            valueOut = search->second;
            return true;
        }

        return false;
    }

    void SetIntegrationTypeRef(const WSTRING& keyIn, const mdTypeRef valueIn)
    {
        std::scoped_lock<std::mutex> lock(wrapper_mutex);
        if (integration_types == nullptr)
        {
            integration_types = std::make_unique<std::unordered_map<WSTRING, mdTypeRef>>();
        }

        (*integration_types)[keyIn] = valueIn;
    }

    CallTargetTokens* GetCallTargetTokens()
    {
        if (calltargetTokens == nullptr)
        {
            calltargetTokens = std::make_unique<CallTargetTokens>(this, enable_by_ref_instrumentation, enable_calltarget_state_by_ref);
        }
        return calltargetTokens.get();
    }

    LiveDebuggerTokens* GetLiveDebuggerTokens()
    {
        if (livedebuggerTokens == nullptr)
        {
            livedebuggerTokens = std::make_unique<LiveDebuggerTokens>(this, enable_by_ref_instrumentation);
        }
        return livedebuggerTokens.get();
    }
};

} // namespace trace

#endif // DD_CLR_PROFILER_MODULE_METADATA_H_
