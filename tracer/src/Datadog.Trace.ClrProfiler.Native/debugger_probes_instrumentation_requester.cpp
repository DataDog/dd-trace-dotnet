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
#include "debugger_rejit_handler_module_method.h"
#include "probes_tracker.h"

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
void DebuggerProbesInstrumentationRequester::PerformInstrumentAllIfNeeded(const ModuleID& module_id, const mdToken& function_token)
{
    if (!IsDebuggerInstrumentAllEnabled())
    {
        return;
    }

    const auto corProfiler = trace::profiler;
    const auto& module_info = GetModuleInfo(corProfiler->info_, module_id);
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
        
        const auto& methodProbe = MethodProbeDefinition(
            WStr("ProbeId"),
            MethodReference(targetAssembly, caller.type.name, caller.name, minVersion, maxVersion, signatureTypes),
            /* is_exact_signature_match */ false);

        const auto numReJITs = m_debugger_rejit_preprocessor->RequestRejitForLoadedModules(
            std::vector{module_id},
            std::vector{methodProbe},
            /* enqueueInSameThread */ true);
        Logger::Debug("Instrument-All: Total number of ReJIT Requested: ", numReJITs);
    }
}


DebuggerProbesInstrumentationRequester::DebuggerProbesInstrumentationRequester(
    std::shared_ptr<trace::RejitHandler> rejit_handler, 
    std::shared_ptr<trace::RejitWorkOffloader> work_offloader) :
    m_rejit_handler(rejit_handler), m_work_offloader(work_offloader)
{
    m_debugger_rejit_preprocessor = std::make_unique<DebuggerRejitPreprocessor>(rejit_handler, work_offloader);
}

void DebuggerProbesInstrumentationRequester::RemoveProbes(debugger::DebuggerRemoveProbesDefinition* removeProbes,
    int removeProbesLength,
    std::set<MethodIdentifier>& revertRequests)
{
    if (removeProbes != nullptr)
    {
        Logger::Info("LiveDebugger: received request to remove ", removeProbesLength, " probes from the managed side.");

        if (removeProbesLength <= 0) return;

        std::vector<WSTRING> probeIdsToRemove;

        for (int i = 0; i < removeProbesLength; i++)
        {
            const DebuggerRemoveProbesDefinition& current = removeProbes[i];
            probeIdsToRemove.emplace_back(WSTRING(current.probeId));
        }

        for (const auto& probeIdToRemove : probeIdsToRemove)
        {
            // Remove from `DebuggerRejitHandlerModuleMethod`
            std::shared_ptr<ProbeMetadata> probeMetadata;
            if (ProbesMetadataTracker::Instance()->TryGetMetadata(probeIdToRemove, probeMetadata))
            {
                for (const auto& method : probeMetadata->methods)
                {
                    const auto moduleHandler = m_rejit_handler->GetOrAddModule(method.moduleId);
                    if (moduleHandler == nullptr)
                    {
                        Logger::Warn("Module handler is returned as null while tried to RemoveProbes, this only "
                                     "happens if the RejitHandler has been shutdown. Exiting early from RemoveProbes.");
                        return; // Exit from RemoveProbes
                    }

                    if (moduleHandler->GetModuleMetadata() == nullptr)
                    {
                        Logger::Error("Could not find the module metadata of method mdToken", method.methodToken,
                                      ", probeId: ", probeIdToRemove, " while trying to remove a probe");
                        continue;
                    }

                    RejitHandlerModuleMethod* methodHandler = nullptr;
                    if (!moduleHandler->TryGetMethod(method.methodToken, &methodHandler))
                    {
                        Logger::Error("Could not find the correct method mdToken", method.methodToken,
                                      ", probeId: ", probeIdToRemove, " while trying to remove a probe");
                        continue;
                    }

                    const auto debuggerMethodHandler = dynamic_cast<DebuggerRejitHandlerModuleMethod*>(methodHandler);

                    if (debuggerMethodHandler == nullptr)
                    {
                        Logger::Error("The method handler of the probe Id we're trying to remove is not of the correct "
                                      "type. Probe Id: ",
                                      probeIdToRemove);
                        continue;
                    }

                    if (!debuggerMethodHandler->RemoveProbe(probeIdToRemove))
                    {
                        Logger::Error("Could not remove from the method handler Probe Id: ", probeIdToRemove);
                        continue;
                    }
                    else
                    {
                        Logger::Info("Removed from the method handler Probe Id: ", probeIdToRemove);
                    }

                    revertRequests.emplace(method);   
                }
            }
            else
            {
                Logger::Error("Received probeId that does not exist in MethodIdentifier mapping. Probe Id: ", probeIdToRemove);
                continue;
            }

            // Remove from probes_

            auto removedFromProbes_ = false;
            for (auto probeIter = m_probes.begin(); probeIter != m_probes.end(); ++probeIter)
            {
                if ((*probeIter)->probeId == probeIdToRemove)
                {
                    probeIter->reset();
                    m_probes.erase(probeIter);
                    removedFromProbes_ = true;
                    break;
                }
            }

            if (!removedFromProbes_)
            {
                Logger::Error("Could not find Probe Id", probeIdToRemove, " in probes_.");
            }
        }

        // Remove from ProbesTracker
        const auto removedProbesCount = ProbesMetadataTracker::Instance()->RemoveProbes(probeIdsToRemove);
        Logger::Info("Successfully removed ", removedProbesCount, " probes from the ProbesTracker.");
    }
}


void DebuggerProbesInstrumentationRequester::AddMethodProbes(
    debugger::DebuggerMethodProbeDefinition* methodProbes, 
    int methodProbesLength,
    std::set<MethodIdentifier>& rejitRequests)
{
    if (methodProbes != nullptr)
    {
        Logger::Info("InitializeLiveDebugger: received ", methodProbesLength, " integrations from managed side.");

        if (methodProbesLength <= 0) 
            return;

        const auto corProfiler = trace::profiler;

        std::vector<MethodProbeDefinition> methodProbeDefinitions;

        for (int i = 0; i < methodProbesLength; i++)
        {
            const DebuggerMethodProbeDefinition& current = methodProbes[i];

            const shared::WSTRING& probeId = shared::WSTRING(current.probeId);
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

            // If the Method Probe request has a signature associated with it, then find that exact match.
            // Otherwise, instrument all overloads of that method regardless of their signature.
            bool isExactSignatureMatch = current.targetParameterTypesLength > 0;

            // In the Debugger product, we don't care about module versioning. Thus we intentionally avoid it.
            const static Version& minVersion = Version(0, 0, 0, 0);
            const static Version& maxVersion = Version(65535, 65535, 65535, 0);

            const auto& methodProbe = MethodProbeDefinition(
                probeId,
                MethodReference({}, targetType, targetMethod, minVersion, maxVersion, signatureTypes),
                isExactSignatureMatch);

            if (Logger::IsDebugEnabled())
            {
                Logger::Debug("  * Target: ", targetType, ".", targetMethod, "(",
                              signatureTypes.size(), ")");
            }

            methodProbeDefinitions.push_back(methodProbe);
            ProbesMetadataTracker::Instance()->CreateNewProbeIfNotExists(probeId);
        }

        std::scoped_lock<std::mutex> moduleLock(trace::profiler->module_ids_lock_);

        std::promise<std::vector<MethodIdentifier>> promise;
        std::future<std::vector<MethodIdentifier>> future = promise.get_future();
        m_debugger_rejit_preprocessor->EnqueuePreprocessRejitRequests(corProfiler->module_ids_, methodProbeDefinitions, &promise);

        const auto& methodProbeRequests = future.get();

        if (!methodProbeRequests.empty())
        {
            rejitRequests.insert(methodProbeRequests.begin(), methodProbeRequests.end());
        }
        else
        {
            Logger::Warn(
                "Received empty list of method probe requests from EnqueuePreprocessRejitRequests after enqueuing ",
                methodProbesLength, " method probes.");
        }
        
        m_probes.reserve(m_probes.size() + methodProbeDefinitions.size());
        for (const auto& methodProbe : methodProbeDefinitions)
        {
            m_probes.push_back(std::make_shared<MethodProbeDefinition>(methodProbe));
        }
    }
}

void DebuggerProbesInstrumentationRequester::AddLineProbes(
    debugger::DebuggerLineProbeDefinition* lineProbes, 
    int lineProbesLength,
    std::set<MethodIdentifier>& rejitRequests)
{
    if (lineProbes != nullptr)
    {
        Logger::Info("InitializeLiveDebugger: received ", lineProbesLength, " integrations from managed side.");

        if (lineProbesLength <= 0)
            return;

        const auto corProfiler = trace::profiler;

        LineProbeDefinitions lineProbeDefinitions;

        for (int i = 0; i < lineProbesLength; i++)
        {
            const DebuggerLineProbeDefinition& current = lineProbes[i];

            const shared::WSTRING& probeId = shared::WSTRING(current.probeId);
            const shared::WSTRING& probeFilePath = shared::WSTRING(current.probeFilePath);
            const auto& lineProbe = std::make_shared<LineProbeDefinition>(LineProbeDefinition(probeId, current.bytecodeOffset, current.lineNumber, current.mvid, current.methodId, probeFilePath));
            
            lineProbeDefinitions.push_back(lineProbe);
        }

        std::scoped_lock<std::mutex> moduleLock(trace::profiler->module_ids_lock_);

        std::promise<std::vector<MethodIdentifier>> promise;
        std::future<std::vector<MethodIdentifier>> future = promise.get_future();
        m_debugger_rejit_preprocessor->EnqueuePreprocessLineProbes(corProfiler->module_ids_, lineProbeDefinitions, &promise);

        const auto& lineProbeRequests = future.get();

        if (!lineProbeRequests.empty())
        {
            rejitRequests.insert(lineProbeRequests.begin(), lineProbeRequests.end());
        }
        else
        {
            Logger::Warn(
                "Received empty list of line probe requests from EnqueuePreprocessLineProbes after enqueuing ",
                lineProbesLength, " line probes.");
        }

        m_probes.reserve(m_probes.size() + lineProbeDefinitions.size());
        for (const auto& lineProbe : lineProbeDefinitions)
        {
            m_probes.push_back(lineProbe);
        }

        Logger::Info("LiveDebugger: Total method probes added: ", m_probes.size());
    }
}

/// <summary>
/// Re-Instrument is the practice of requesting revert & reijt to restore method(s) to their original form and then re-instrument them.
/// In case a revert was requested for a method, we need to determine if that method have other probes associated with them,
/// in which case we should re-instrument them.
/// </summary>
/// <param name="revertRequests">Methods to revert.</param>
/// <param name="reInstrumentRequests">[OUT] Gets populated with methods that needs to go through re-instrumentation.</param>
void DebuggerProbesInstrumentationRequester::DetermineReInstrumentProbes(std::set<MethodIdentifier>& revertRequests,
                                                                    std::set<MethodIdentifier>& reInstrumentRequests) const
{
    if (revertRequests.empty()) 
        return;
    
    for (const auto& request : revertRequests)
    {
        const auto moduleHandler = m_rejit_handler->GetOrAddModule(request.moduleId);
        if (moduleHandler == nullptr)
        {
            Logger::Warn("Module handler is returned as null while tried to RemoveProbes, this only happens if "
                         "the RejitHandler has been shutdown. Exiting early from RemoveProbes.");
            return; // Exit from RemoveProbes
        }

        if (moduleHandler->GetModuleMetadata() == nullptr)
        {
            Logger::Error("Could not find the module metadata of method mdToken ", request.methodToken, " while trying to remove a probe");
            continue;
        }

        RejitHandlerModuleMethod* methodHandler = nullptr;
        if (!moduleHandler->TryGetMethod(request.methodToken, &methodHandler))
        {
            Logger::Error("Could not find the correct method mdToken ", request.methodToken, " while trying to remove a probe");
            continue;
        }

        const auto debuggerMethodHandler = dynamic_cast<DebuggerRejitHandlerModuleMethod*>(methodHandler);

        if (debuggerMethodHandler == nullptr)
        {
            Logger::Error("The method handler of the probe Id we're trying to remove is not of the correct type");
            continue;
        }

        if (!debuggerMethodHandler->GetProbes().empty())
        {
            reInstrumentRequests.emplace(request);
        }
    }
}

void DebuggerProbesInstrumentationRequester::InstrumentProbes(debugger::DebuggerMethodProbeDefinition* methodProbes,
                                                              int methodProbesLength,
                                                              debugger::DebuggerLineProbeDefinition* lineProbes,
                                                              int lineProbesLength,
                                                              debugger::DebuggerRemoveProbesDefinition* removeProbes,
                                                              int removeProbesLength)
{
    std::lock_guard lock(m_probes_mutex);

    std::set<MethodIdentifier> revertRequests{};
    RemoveProbes(removeProbes, removeProbesLength, revertRequests);

    std::set<MethodIdentifier> rejitRequests{};
    AddMethodProbes(methodProbes, methodProbesLength, rejitRequests);
    AddLineProbes(lineProbes, lineProbesLength, rejitRequests);

    std::set<MethodIdentifier> reInstrumentRequests{};
    DetermineReInstrumentProbes(revertRequests, reInstrumentRequests);

    if (!rejitRequests.empty())
    {
        // Treat instrumentation requests as 're-instrument'.
        // Meaning, for each instrumentation request of a method we first request a revert.
        // We do that to restore methods to their original form, as our rejit logic assumes we are rewriting the method
        // from scratch. If a method was never instrumented before then it's NoOp.
        // In other words, calling revert on a method that was never rejitted before makes our lives easier, and doesn't
        // cause any harm.
        revertRequests.insert(rejitRequests.begin(), rejitRequests.end());   
    }

    if (!reInstrumentRequests.empty())
    {
        rejitRequests.insert(reInstrumentRequests.begin(), reInstrumentRequests.end());
    }

    // We offload the actual `RequestRejit` & `RequestRevert` to a separate thread because they are not permitted
    // to be called from managed land.

    if (!revertRequests.empty())
    {
        Logger::Info("About to RequestRevert for ", revertRequests.size(), " methods.");

        // RequestRevert
        std::vector<MethodIdentifier> requests(revertRequests.size());
        std::copy(revertRequests.begin(), revertRequests.end(), requests.begin());
        std::promise<void> promise;
        std::future<void> future = promise.get_future();
        m_debugger_rejit_preprocessor->EnqueueRequestRevert(requests, &promise);
        // wait and get the value from the future<void>
        future.get();   
    }

    if (!rejitRequests.empty())
    {
        Logger::Info("About to RequestRejit for ", rejitRequests.size(), " methods.");

        // RequestRejit
        std::promise<void> promise;
        std::future<void> future = promise.get_future();
        std::vector<MethodIdentifier> requests(rejitRequests.size());
        std::copy(rejitRequests.begin(), rejitRequests.end(), requests.begin());
        m_debugger_rejit_preprocessor->EnqueueRequestRejit(requests, &promise);
        // wait and get the value from the future<void>
        future.get();   
    }
}

int DebuggerProbesInstrumentationRequester::GetProbesStatuses(WCHAR** probeIds, int probeIdsLength, debugger::DebuggerProbeStatus* probeStatuses)
{
    if (probeIds == nullptr)
    {
        return 0;
    }

    Logger::Info("Received ", probeIdsLength, " probes (ids) from managed side for status.");

    if (probeIdsLength <= 0)
    {
        return 0;
    }

    int probeStatusesCount = 0;

    for (auto probeIndex = 0; probeIndex < probeIdsLength; probeIndex++)
    {
        const auto& probeId = shared::WSTRING(probeIds[probeIndex]);
        std::shared_ptr<ProbeMetadata> probeMetadata;
        if (ProbesMetadataTracker::Instance()->TryGetMetadata(probeId, probeMetadata))
        {
            probeStatuses[probeStatusesCount] = 
                {probeMetadata->probeId.c_str(), probeMetadata->status};
            probeStatusesCount++;
        }
        else
        {
            Logger::Warn("Failed to get probe metadata for probeId = ", probeId,
                            " while trying to obtain its probe status.");
        }
    }

    return probeStatusesCount;
}

const std::vector<std::shared_ptr<ProbeDefinition>>& DebuggerProbesInstrumentationRequester::GetProbes() const
{
    return m_probes;
}

DebuggerRejitPreprocessor* DebuggerProbesInstrumentationRequester::GetPreprocessor()
{
    return m_debugger_rejit_preprocessor.get();
}

ULONG DebuggerProbesInstrumentationRequester::RequestRejitForLoadedModule(const ModuleID moduleId)
{
    std::vector<MethodProbeDefinition> methodProbes;

    std::lock_guard lock(m_probes_mutex);

    for (const auto& probe : m_probes)
    {
        const auto methodProbe = std::dynamic_pointer_cast<MethodProbeDefinition>(probe);
        if (methodProbe != nullptr)
        {
            methodProbes.emplace_back(*methodProbe);
        }
    }

    if (methodProbes.empty())
    {
        return 0;
    }

    return m_debugger_rejit_preprocessor->RequestRejitForLoadedModules(std::vector<ModuleID>{moduleId}, methodProbes);
    // TODO do it also for line probes (scenario: module loaded (line probe request arrived) & unloaded & loaded)
}

HRESULT DebuggerProbesInstrumentationRequester::NotifyReJITError(ModuleID moduleId, mdMethodDef methodId,
                                                                 FunctionID functionId, HRESULT hrStatus)
{
    const auto probeIds = ProbesMetadataTracker::Instance()->GetProbeIds(moduleId, methodId);
    if (!probeIds.empty())
    {
        Logger::Info("Marking ", probeIds.size(), " probes as failed due to ReJITError notification.");
        for (const auto& probeId : probeIds)
        {
            Logger::Info("Marking ", probeId, " as Error.");
            ProbesMetadataTracker::Instance()->SetProbeStatus(probeId, ProbeStatus::_ERROR);
        }
    }

    return S_OK;
}

} // namespace debugger