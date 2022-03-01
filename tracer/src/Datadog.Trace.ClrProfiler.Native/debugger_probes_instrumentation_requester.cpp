#include "debugger_probes_instrumentation_requester.h"

#include "debugger_members.h"
#include "cor_profiler.h"
#include "il_rewriter_wrapper.h"
#include "logger.h"
#include "stats.h"
#include "version.h"
#include "debugger_rejit_preprocessor.h"
#include "debugger_constants.h"
#include "debugger_environment_variables_util.h"

namespace debugger
{

/**
 * \brief For testing purposes only. This method is used to determine if we are in "instrument-all mode", in which case we should instrument every single method on the given assembly. 
 * \This mode is enabled when the environment variable `DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL` is set to true.
 * \param assemblyName the name of the assembly we're trying to rewrite using Debugger's Instrumentation
 * \return true if the given assembly is viable for instrument-all, false otherwise.
 */
bool DebuggerProbesInstrumentationRequester::ShouldPerformInstrumentAll(const WSTRING& assemblyName)
{
    for (auto&& skip_assembly_pattern : skip_assembly_prefixes)
    {
        if (assemblyName.rfind(skip_assembly_pattern, 0) == 0)
        {
            Logger::Debug("DebuggerInstrumentAllHelper::ShouldPerformInstrumentAll skipping module by pattern: Name=",
                          assemblyName);
            return false;
        }
    }

    for (auto&& skip_assembly : skip_assemblies)
    {
        if (assemblyName == skip_assembly)
        {
            Logger::Debug("DebuggerInstrumentAllHelper::ShouldPerformInstrumentAll skipping module by exact name: Name= ",
                          assemblyName);
            return false;
        }
    }

    return true;
}

/**
 * \brief For Testing-Purposes. Requests ReJIT for the given method if certain checks are met. Relevant when the environment variable `DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL` is set to true.
 * \param module_info the ModuleInfo of the module entering into instrumentation-all.
 * \param module_id the ModuleID of the module entering into instrumentation-all.
 * \param function_token the mdToken of the method entering into instrumentation-all.
 */
void DebuggerProbesInstrumentationRequester::PerformInstrumentAllIfNeeded(const ModuleInfo& module_info,
                                                                          const ModuleID& module_id,
                                                                          const mdToken& function_token)
{
    if (!IsDebuggerInstrumentAllEnabled())
    {
        return;
    }

    const auto corProfiler = trace::profiler;

    const auto assembly_name = module_info.assembly.name;

    if (ShouldPerformInstrumentAll(assembly_name))
    {
        ComPtr<IUnknown> metadataInterfaces;
        auto hr = corProfiler->info_->GetModuleMetaData(module_id, ofRead | ofWrite, IID_IMetaDataImport2,
                                                        metadataInterfaces.GetAddressOf());

        auto metadataImport = metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
        auto metadataEmit = metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
        auto assemblyImport = metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
        auto assemblyEmit = metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

        Logger::Debug("Temporaly allocating the ModuleMetadata for injection. ModuleId=", module_id,
                      " ModuleName=", module_info.assembly.name);

        std::unique_ptr<ModuleMetadata> module_metadata = std::make_unique<ModuleMetadata>(
            metadataImport, metadataEmit, assemblyImport, assemblyEmit, module_info.assembly.name,
            module_info.assembly.app_domain_id, &corProfiler->corAssemblyProperty,
            corProfiler->enable_by_ref_instrumentation, corProfiler->enable_calltarget_state_by_ref);

        // get function info
        auto caller = GetFunctionInfo(module_metadata->metadata_import, function_token);
        if (!caller.IsValid())
        {
            return;
        }

        hr = caller.method_signature.TryParse();
        if (FAILED(hr))
        {
            Logger::Warn(" * DebuggerProbesInstrumentationRequester::PerformInstrumentAllIfNeeded: The method signature: ", caller.method_signature.str(), " cannot be parsed.");
            return;
        }

        Logger::Debug("About to perform instrument all for ModuleId=", module_id,
                      " ModuleName=", module_info.assembly.name,
                     " MethodName=", caller.name, " TypeName=", caller.type.name);

        // In the Debugger product, we don't care about module versioning. Thus we intentionally avoid it.
        const static Version& minVersion = Version(0, 0, 0, 0);
        const static Version& maxVersion = Version(65535, 65535, 65535, 0);

        const auto targetAssembly = module_info.assembly.name;
        
        const auto numOfArgs = caller.method_signature.NumberOfArguments();
        const auto& methodArguments = caller.method_signature.GetMethodArguments();
        std::vector<WSTRING> signatureTypes;
        
        // We should ALWAYS push something in front of the arguments list as the Preprocessor requires the return value to be there,
        // even if there are none (in which case, it should be System.Void).
        // The Preprocessor is not using the return value at all (and merely skipping it), so we insert an empty string.
        signatureTypes.push_back(WSTRING());

        Logger::Debug("    * Comparing signature for method: ", caller.type.name, ".", caller.name);
        for (unsigned int i = 0; i < numOfArgs; i++)
        {
            signatureTypes.push_back(methodArguments[i].GetTypeTokName(metadataImport));
        }

        const auto& methodProbe = MethodProbeDefinition(MethodReference(targetAssembly, caller.type.name, caller.name, minVersion, maxVersion, signatureTypes));
        const auto numReJITs = debugger_rejit_preprocessor->RequestRejitForLoadedModules(
            std::vector<ModuleID>{module_id},
            std::vector<MethodProbeDefinition>{methodProbe},
            /* enqueueInSameThread */ true);
        Logger::Debug("Instrument-All: Total number of ReJIT Requested: ", numReJITs);
    }
}


DebuggerProbesInstrumentationRequester::DebuggerProbesInstrumentationRequester(std::shared_ptr<trace::RejitHandler> rejit_handler, std::shared_ptr<trace::RejitWorkOffloader> work_offloader)
{
    debugger_rejit_preprocessor = std::make_unique<DebuggerRejitPreprocessor>(std::move(rejit_handler), std::move(work_offloader));
}

void DebuggerProbesInstrumentationRequester::InstrumentProbes(WCHAR* id, DebuggerMethodProbeDefinition* items, int size)
{
    if (size <= 0)
        return;

    // TODO:
    //auto _ = trace::Stats::Instance()->InitializeLiveDebuggerMeasure();

    const auto corProfiler = trace::profiler;

    shared::WSTRING definitionsId = shared::WSTRING(id);
    Logger::Info("InitializeLiveDebugger: received id: ", definitionsId, " from managed side with ", size,
                 " integrations.");

    if (items != nullptr)
    {
        std::vector<MethodProbeDefinition> methodProbeDefinitions;

        for (int i = 0; i < size; i++)
        {
            const DebuggerMethodProbeDefinition& current = items[i];

            const shared::WSTRING& targetAssembly = shared::WSTRING(current.targetAssembly);
            const shared::WSTRING& targetType = shared::WSTRING(current.targetType);
            const shared::WSTRING& targetMethod = shared::WSTRING(current.targetMethod);

            std::vector<shared::WSTRING> signatureTypes;
            for (int sIdx = 0; sIdx < current.targetParameterTypesLength; sIdx++)
            {
                const auto& currentSignature = current.targetParameterTypes[sIdx];
                if (currentSignature != nullptr)
                {
                    signatureTypes.push_back(shared::WSTRING(currentSignature));
                }
            }

            // In the Debugger product, we don't care about module versioning. Thus we intentionally avoid it.
            const static Version& minVersion = Version(0, 0, 0, 0);
            const static Version& maxVersion = Version(65535, 65535, 65535, 0);

            const auto& methodProbe = MethodProbeDefinition(
                MethodReference(targetAssembly, targetType, targetMethod, minVersion, maxVersion, signatureTypes));

            if (Logger::IsDebugEnabled())
            {
                Logger::Debug("  * Target: ", targetAssembly, " | ", targetType, ".", targetMethod, "(",
                              signatureTypes.size(), ")");
            }

            methodProbeDefinitions.push_back(methodProbe);
        }

        std::scoped_lock<std::mutex> moduleLock(corProfiler->module_ids_lock_);

        Logger::Info("Total number of modules to analyze: ", corProfiler->module_ids_.size());

        std::promise<ULONG> promise;
        std::future<ULONG> future = promise.get_future();
        debugger_rejit_preprocessor->EnqueueRequestRejitForLoadedModules(corProfiler->module_ids_,
                                                                          methodProbeDefinitions, &promise);

        // wait and get the value from the future<int>
        const auto& numReJITs = future.get();
        Logger::Debug("Total number of ReJIT Requested: ", numReJITs);

        method_probes_.reserve(method_probes_.size() + methodProbeDefinitions.size());
        for (const auto& methodProbe : methodProbeDefinitions)
        {
            method_probes_.push_back(methodProbe);
        }

        Logger::Info("InitializeLiveDebugger: Total startup method probes: ", method_probes_.size());
    }
}

const std::vector<MethodProbeDefinition>& DebuggerProbesInstrumentationRequester::GetProbes() const
{
    return method_probes_;
}

DebuggerRejitPreprocessor* DebuggerProbesInstrumentationRequester::GetPreprocessor()
{
    return debugger_rejit_preprocessor.get();
}

} // namespace debugger