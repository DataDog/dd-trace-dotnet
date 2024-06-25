#ifndef DD_CLR_PROFILER_MODULE_METADATA_H_
#define DD_CLR_PROFILER_MODULE_METADATA_H_

#include <corhlpr.h>
#include <mutex>
#include <unordered_map>
#include <unordered_set>

#include "calltarget_tokens.h"
#include "clr_helpers.h"
#include "debugger_tokens.h"
#include "integration.h"
#include "tracer_integration_definition.h"
#include "tracer_tokens.h"
#include "fault_tolerant_tokens.h"
#include "../../../shared/src/native-src/com_ptr.h"
#include "../../../shared/src/native-src/string.h"

namespace trace
{

class ModuleMetadata : public ModuleMetadataBase
{
private:
    std::mutex wrapper_mutex;
    std::once_flag tracer_tokens_once_flag;
    std::once_flag debugger_tokens_once_flag;
    std::once_flag fault_tolerant_tokens_once_flag;
    std::unique_ptr<std::unordered_map<shared::WSTRING, mdTypeRef>> integration_types = nullptr;
    std::unique_ptr<TracerTokens> tracerTokens = nullptr;
    std::unique_ptr<debugger::DebuggerTokens> debuggerTokens = nullptr;
    std::unique_ptr<fault_tolerant::FaultTolerantTokens> faultTolerantTokens = nullptr;
    std::unique_ptr<std::vector<IntegrationDefinition>> integrations = nullptr;
    mdTypeSpec moduleSpecSanityToken = mdTypeSpecNil;

public:
    const shared::WSTRING assemblyName = shared::EmptyWStr;
    const AppDomainID app_domain_id;
    const GUID module_version_id;
    const AssemblyProperty* corAssemblyProperty = nullptr;
    const bool enable_by_ref_instrumentation = false;
    const bool enable_calltarget_state_by_ref = false;

    ModuleMetadata(ComPtr<IMetaDataImport2> metadata_import, ComPtr<IMetaDataEmit2> metadata_emit,
                   ComPtr<IMetaDataAssemblyImport> assembly_import, ComPtr<IMetaDataAssemblyEmit> assembly_emit,
                   const shared::WSTRING& assembly_name, const AppDomainID app_domain_id, const GUID module_version_id,
                   std::unique_ptr<std::vector<IntegrationDefinition>>&& integrations,
                   const AssemblyProperty* corAssemblyProperty, const bool enableByRefInstrumentation,
                   const bool enableCallTargetStateByRef) :
        ModuleMetadataBase(metadata_import, metadata_emit, assembly_import, assembly_emit),
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
                   const shared::WSTRING& assembly_name, const AppDomainID app_domain_id,
                   const AssemblyProperty* corAssemblyProperty, const bool enableByRefInstrumentation,
                   const bool enableCallTargetStateByRef) :
        ModuleMetadataBase(metadata_import, metadata_emit, assembly_import, assembly_emit),
        assemblyName(assembly_name),
        app_domain_id(app_domain_id),
        module_version_id(),
        corAssemblyProperty(corAssemblyProperty),
        enable_by_ref_instrumentation(enableByRefInstrumentation),
        enable_calltarget_state_by_ref(enableCallTargetStateByRef)
    {
    }

    bool TryGetIntegrationTypeRef(const shared::WSTRING& keyIn, mdTypeRef& valueOut)
    {
        if (integration_types == nullptr)
        {
            return false;
        }

        std::scoped_lock<std::mutex> lock(wrapper_mutex);

        const auto search = integration_types->find(keyIn);

        if (search != integration_types->end())
        {
            valueOut = search->second;
            return true;
        }

        return false;
    }

    void SetIntegrationTypeRef(const shared::WSTRING& keyIn, const mdTypeRef valueIn)
    {
        std::scoped_lock<std::mutex> lock(wrapper_mutex);
        if (integration_types == nullptr)
        {
            integration_types = std::make_unique<std::unordered_map<shared::WSTRING, mdTypeRef>>();
        }

        (*integration_types)[keyIn] = valueIn;
    }

    TracerTokens* GetTracerTokens()
    {
        std::call_once(tracer_tokens_once_flag,
            [this] {
            tracerTokens = std::make_unique<TracerTokens>(this, enable_by_ref_instrumentation,
                                                                  enable_calltarget_state_by_ref);
            });

        return tracerTokens.get();
    }

    debugger::DebuggerTokens* GetDebuggerTokens()
    {
        std::call_once(debugger_tokens_once_flag,
            [this] {
               debuggerTokens = std::make_unique<debugger::DebuggerTokens>(this);
            });

        return debuggerTokens.get();
    }

    fault_tolerant::FaultTolerantTokens* GetFaultTolerantTokens()
    {
        std::call_once(fault_tolerant_tokens_once_flag,
                       [this] { faultTolerantTokens = std::make_unique<fault_tolerant::FaultTolerantTokens>(this); });

        return faultTolerantTokens.get();
    }
};

} // namespace trace

#endif // DD_CLR_PROFILER_MODULE_METADATA_H_
