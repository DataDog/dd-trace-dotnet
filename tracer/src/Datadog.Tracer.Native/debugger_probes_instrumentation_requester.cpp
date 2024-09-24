#include "debugger_probes_instrumentation_requester.h"

#include "cor_profiler.h"
#include "dd_profiler_constants.h"
#include "debugger_constants.h"
#include "debugger_environment_variables_util.h"
#include "debugger_members.h"
#include "debugger_method_rewriter.h"
#include "debugger_probes_tracker.h"
#include "debugger_rejit_handler_module_method.h"
#include "debugger_rejit_preprocessor.h"
#include "fault_tolerant_method_duplicator.h"
#include "fault_tolerant_tracker.h"
#include "iast/iast_util.h"
#include "logger.h"

#include <fstream>
#include <map>
#include <random>

namespace debugger
{

/**
 * \brief For testing purposes only. This method is used to determine if we are in "instrument-all mode", in which case
 * we should instrument every single method on the given assembly. \This mode is enabled when the environment variable
 * `DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL` is set to true. \param assemblyName the name of the assembly we're trying to
 * rewrite using Debugger's Instrumentation \return true if the given assembly is viable for instrument-all, false
 * otherwise.
 */
bool DebuggerProbesInstrumentationRequester::IsCoreLibOr3rdParty(const WSTRING& assemblyName)
{
    for (auto&& skip_assembly_pattern : skip_assembly_prefixes)
    {
        if (assemblyName.rfind(skip_assembly_pattern, 0) == 0)
        {
            Logger::Debug("DebuggerInstrumentAllHelper::IsCoreLibOr3rdParty skipping module by pattern: Name=",
                          assemblyName);
            return true;
        }
    }

    for (auto&& skip_assembly : skip_assemblies)
    {
        if (assemblyName == skip_assembly)
        {
            Logger::Debug("DebuggerInstrumentAllHelper::IsCoreLibOr3rdParty skipping module by exact name: Name= ",
                          assemblyName);
            return true;
        }
    }

    return false;
}

WSTRING DebuggerProbesInstrumentationRequester::GenerateRandomProbeId()
{
    std::random_device rd;
    std::mt19937 gen(rd());
    std::uniform_int_distribution<> dis(0, 15);
    std::uniform_int_distribution<> dis8(8, 11);

    std::wstringstream ss;
    int i;
    ss << std::hex;

    for (i = 0; i < 8; i++)
    {
        ss << dis(gen);
    }

    ss << L"-";

    for (i = 0; i < 4; i++)
    {
        ss << dis(gen);
    }

    ss << L"-4";

    for (i = 0; i < 3; i++)
    {
        ss << dis(gen);
    }

    ss << L"-a";

    for (i = 0; i < 3; i++)
    {
        ss << dis(gen);
    }

    ss << L"-";

    for (i = 0; i < 12; i++)
    {
        ss << dis(gen);
    }

    std::wstring temp = ss.str();
    WSTRING converted(temp.begin(), temp.end());
    return converted;
}

auto& DebuggerProbesInstrumentationRequester::GetExplorationTestLineProbes(const WSTRING& filename)
{
    std::call_once(explorationLinesInitFlag, [this, fn = filename] {
        DebuggerProbesInstrumentationRequester::InitializeExplorationTestLineProbes(fn);
    });
    return explorationLineProbes;
}

void DebuggerProbesInstrumentationRequester::InitializeExplorationTestLineProbes(const WSTRING& filename)
{
#ifdef _WIN32
    std::ifstream file(filename);
#else
    std::ifstream file(ToString(filename).c_str());
#endif

    if (!file.is_open())
    {
        Logger::Error("InitializeExplorationTestLineProbes: Unable to open file: ", filename);
        return;
    }

    std::string line;
    while (std::getline(file, line))
    {
        std::istringstream lineStream(line);
        std::string guid;
        std::string methodTokenStr;
        std::string bytecodeOffsetStr;

        // Split the line by commas
        if (std::getline(lineStream, guid, ',') && std::getline(lineStream, methodTokenStr, ',') &&
            std::getline(lineStream, bytecodeOffsetStr, ','))
        {
            auto methodToken = static_cast<mdToken>(std::stoul(methodTokenStr));
            int bytecodeOffset = std::stoi(bytecodeOffsetStr);

            auto key = std::make_pair(guid, methodToken);
            explorationLineProbes[key].push_back(bytecodeOffset);
        }
    }

    file.close();
}

/**
 * \brief For Testing-Purposes. Requests ReJIT for the given method if certain checks are met. Relevant when the
 * environment variable `DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL` is set to true. \param module_info the ModuleInfo of the
 * module entering into instrumentation-all. \param module_id the ModuleID of the module entering into
 * instrumentation-all. \param function_token the mdToken of the method entering into instrumentation-all.
 */
void DebuggerProbesInstrumentationRequester::PerformInstrumentAllIfNeeded(const ModuleID& module_id, mdToken& function_token)
{
    try
    {
        if (!IsDebuggerInstrumentAllEnabled())
        {
            return;
        }

        const auto& module_info = GetModuleInfo(m_corProfiler->info_, module_id);
        const auto assembly_name = module_info.assembly.name;

        if (IsCoreLibOr3rdParty(assembly_name))
        {
            return;
        }

        ComPtr<IUnknown> metadataInterfaces;
        auto hr = m_corProfiler->info_->GetModuleMetaData(module_id, ofRead | ofWrite, IID_IMetaDataImport2,
                                                          metadataInterfaces.GetAddressOf());

        auto metadataImport = metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
        auto metadataEmit = metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
        auto assemblyImport = metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
        auto assemblyEmit = metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

        Logger::Debug("Temporally allocating the ModuleMetadata for injection. ModuleId=", module_id,
                      " ModuleName=", module_info.assembly.name);

        std::unique_ptr<ModuleMetadata> module_metadata = std::make_unique<ModuleMetadata>(
            metadataImport, metadataEmit, assemblyImport, assemblyEmit, module_info.assembly.name,
            module_info.assembly.app_domain_id, &m_corProfiler->corAssemblyProperty,
            m_corProfiler->enable_by_ref_instrumentation, m_corProfiler->enable_calltarget_state_by_ref);

        // get function info
        auto caller = GetFunctionInfo(module_metadata->metadata_import, function_token);
        if (!caller.IsValid())
        {
            return;
        }

        hr = caller.method_signature.TryParse();
        if (FAILED(hr))
        {
            Logger::Warn("    * DebuggerProbesInstrumentationRequester::PerformInstrumentAllIfNeeded: [MethodDef=",
                         shared::TokenStr(&function_token), ", Type=", caller.type.name, ", Method=", caller.name, "]",
                         ": could not parse method signature.");
            Logger::Debug("    Method signature is: ", caller.method_signature.str());
            return;
        }

        Logger::Debug("About to perform instrument all for ModuleId=", module_id,
                      " ModuleName=", module_info.assembly.name, " MethodName=", caller.name,
                      " TypeName=", caller.type.name);

        // In the Debugger product, we don't care about module versioning. Thus we intentionally avoid it.
        const static Version& minVersion = Version(0, 0, 0, 0);
        const static Version& maxVersion = Version(65535, 65535, 65535, 0);

        const auto targetAssembly = module_info.assembly.name;

        const auto numOfArgs = caller.method_signature.NumberOfArguments();
        const auto& methodArguments = caller.method_signature.GetMethodArguments();
        std::vector<WSTRING> signatureTypes;

        // We should ALWAYS push something in front of the arguments list as the Preprocessor requires the return value
        // to be there, even if there are none (in which case, it should be System.Void). The Preprocessor is not using
        // the return value at all (and merely skipping it), so we insert an empty string.
        signatureTypes.push_back(WSTRING());

        Logger::Debug("    * Comparing signature for method: ", caller.type.name, ".", caller.name);
        for (unsigned int i = 0; i < numOfArgs; i++)
        {
            signatureTypes.push_back(methodArguments[i].GetTypeTokName(metadataImport));
        }

        const auto& methodProbe = std::make_shared<MethodProbeDefinition>(MethodProbeDefinition(
            GenerateRandomProbeId(),
            MethodReference(targetAssembly, caller.type.name, caller.name, minVersion, maxVersion, signatureTypes),
            /* is_exact_signature_match */ false));

        const auto numReJITs = m_debugger_rejit_preprocessor->RequestRejitForLoadedModules(
            std::vector{module_id}, std::vector{methodProbe}, /* enqueueInSameThread */ true);

        Logger::Debug("Instrument-All: ReJIT Requested for: ", methodProbe->target_method.method_name,
                      ". ProbeId:", methodProbe->probeId, ". Numbers of ReJits: ", numReJITs);

        if (!IsDebuggerInstrumentAllLinesEnabled())
        {
            return;
        }

        const auto lineProbesPath = GetEnvironmentValue(environment::internal_instrument_all_lines_path);
        const shared::WSTRING& probeFilePath = WStr("line_exploration_file_path");

        auto lineProbesMap = GetExplorationTestLineProbes(lineProbesPath);

        WCHAR moduleName[MAX_PACKAGE_NAME];
        ULONG nSize;
        GUID mvid;
        hr = metadataImport->GetScopeProps(moduleName, MAX_PACKAGE_NAME, &nSize, &mvid);

        if (FAILED(hr))
        {
            Logger::Warn("Can't read module scope props for module ID: ", module_id);
        }
        else
        {
            auto original_function_token = function_token;

            if (fault_tolerant::FaultTolerantTracker::Instance()->IsInstrumentedMethod(module_id, function_token))
            {
                function_token =
                    fault_tolerant::FaultTolerantTracker::Instance()->GetKickoffMethodFromInstrumentedMethod(
                        module_id, function_token);
            }

            auto lowerMvid = iast::ToLower(ToString(mvid));
            lowerMvid = lowerMvid.substr(1, lowerMvid.length() - 2);
            auto key = std::make_pair(lowerMvid, function_token);
            auto it = lineProbesMap.find(key);
            if (it == lineProbesMap.end())
            {
                Logger::Warn("No line probes found in method: ", caller.type.name, ".", caller.name);
            }
            else
            {
                std::vector<std::shared_ptr<LineProbeDefinition>> lineProbeDefinitions;
                auto bytecodeOffsets = it->second;
                for (const auto& offset : bytecodeOffsets)
                {
                    const auto& lineProbe = std::make_shared<LineProbeDefinition>(LineProbeDefinition(
                        GenerateRandomProbeId(), offset, offset, mvid, original_function_token, probeFilePath));
                    lineProbeDefinitions.push_back(lineProbe);
                }

                std::promise<std::vector<MethodIdentifier>> promise;
                std::future<std::vector<MethodIdentifier>> future = promise.get_future();
                m_debugger_rejit_preprocessor->EnqueuePreprocessLineProbes(std::vector{module_id}, lineProbeDefinitions,
                                                                           &promise);
                const auto& lineProbeRequests = future.get();

                // RequestRejit
                auto requestRejitPromise = std::make_shared<std::promise<void>>();
                std::future<void> requestRejitFuture = requestRejitPromise->get_future();
                std::vector<MethodIdentifier> requests(lineProbeRequests.size());
                std::copy(lineProbeRequests.begin(), lineProbeRequests.end(), requests.begin());
                m_debugger_rejit_preprocessor->EnqueueRequestRejit(requests, requestRejitPromise);
                // wait and get the value from the future<void>
                requestRejitFuture.get();
                Logger::Debug("Instrument-All: ReJIT Requested for: ", methodProbe->target_method.method_name,
                              ". Numbers of line probes: ", lineProbeDefinitions.size());
            }
        }
    }
    catch (...)
    {
        Logger::Error("PerformInstrumentAllIfNeeded: fail for module: ", module_id, ". method: ", function_token);
    }
}

DebuggerProbesInstrumentationRequester::DebuggerProbesInstrumentationRequester(
    CorProfiler* corProfiler, std::shared_ptr<trace::RejitHandler> rejit_handler,
    std::shared_ptr<trace::RejitWorkOffloader> work_offloader,
    std::shared_ptr<fault_tolerant::FaultTolerantMethodDuplicator> fault_tolerant_method_duplicator) :
    m_corProfiler(corProfiler),
    m_debugger_rejit_preprocessor(
        std::make_unique<DebuggerRejitPreprocessor>(corProfiler, rejit_handler, work_offloader)),
    m_rejit_handler(rejit_handler),
    m_work_offloader(work_offloader),
    m_fault_tolerant_method_duplicator(fault_tolerant_method_duplicator)
{
    is_debugger_or_exception_debugging_enabled = IsDebuggerEnabled() || IsExceptionReplayEnabled();
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
                for (const auto& methodToIndexPair : probeMetadata->methodIndexMap)
                {
                    const auto method = methodToIndexPair.first;
                    const auto moduleHandler = m_debugger_rejit_preprocessor->GetOrAddModule(method.moduleId);
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
                Logger::Error("Received probeId that does not exist in MethodIdentifier mapping. Probe Id: ",
                              probeIdToRemove);
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

void DebuggerProbesInstrumentationRequester::AddMethodProbes(debugger::DebuggerMethodProbeDefinition* methodProbes,
                                                             int methodProbesLength,
                                                             debugger::DebuggerMethodSpanProbeDefinition* spanProbes,
                                                             int spanProbesLength,
                                                             std::set<MethodIdentifier>& rejitRequests)
{
    std::vector<std::shared_ptr<MethodProbeDefinition>> methodProbeDefinitions;

    if (methodProbes != nullptr && methodProbesLength > 0)
    {
        Logger::Info("InitializeLiveDebugger: received ", methodProbesLength, " method probes from managed side.");

        for (int i = 0; i < methodProbesLength; i++)
        {
            const DebuggerMethodProbeDefinition& current = methodProbes[i];

            if (ProbeIdExists(current.probeId))
            {
                Logger::Debug("[AddMethodProbes] Method Probe Id: ", current.probeId, " is already processed.");
                continue;
            }

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
                probeId, MethodReference({}, targetType, targetMethod, minVersion, maxVersion, signatureTypes),
                isExactSignatureMatch);

            if (Logger::IsDebugEnabled())
            {
                Logger::Debug("  * Target: ", targetType, ".", targetMethod, "(", signatureTypes.size(), ")");
            }

            methodProbeDefinitions.push_back(std::make_shared<MethodProbeDefinition>(methodProbe));
            ProbesMetadataTracker::Instance()->CreateNewProbeIfNotExists(probeId);
        }
    }

    if (spanProbes != nullptr && spanProbesLength > 0)
    {
        Logger::Info("InitializeLiveDebugger: received ", methodProbesLength, " span probes from managed side.");

        for (int i = 0; i < spanProbesLength; i++)
        {
            const DebuggerMethodSpanProbeDefinition& current = spanProbes[i];

            if (ProbeIdExists(current.probeId))
            {
                Logger::Debug("[AddMethodProbes] Method Probe Id: ", current.probeId, " is already processed.");
                continue;
            }

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

            const auto& spanProbe = SpanProbeOnMethodDefinition(
                probeId, MethodReference({}, targetType, targetMethod, minVersion, maxVersion, signatureTypes),
                isExactSignatureMatch);

            if (Logger::IsDebugEnabled())
            {
                Logger::Debug("  * Target: ", targetType, ".", targetMethod, "(", signatureTypes.size(), ")");
            }

            methodProbeDefinitions.push_back(std::make_shared<SpanProbeOnMethodDefinition>(spanProbe));
            ProbesMetadataTracker::Instance()->CreateNewProbeIfNotExists(probeId);
        }
    }

    if (methodProbeDefinitions.empty())
    {
        Logger::Debug("[AddMethodProbes] Early exiting, there are no new method probes to be added.");
        return;
    }

    auto modules = m_corProfiler->module_ids.Get();

    auto promise = std::make_shared<std::promise<std::vector<MethodIdentifier>>>();
    std::future<std::vector<MethodIdentifier>> future = promise->get_future();
    m_debugger_rejit_preprocessor->EnqueuePreprocessRejitRequests(modules.Ref(), methodProbeDefinitions, promise);

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
        m_probes.push_back(methodProbe);
    }
}

void DebuggerProbesInstrumentationRequester::AddLineProbes(debugger::DebuggerLineProbeDefinition* lineProbes,
                                                           int lineProbesLength,
                                                           std::set<MethodIdentifier>& rejitRequests)
{
    if (lineProbes != nullptr)
    {
        Logger::Info("InitializeLiveDebugger: received ", lineProbesLength, " integrations from managed side.");

        if (lineProbesLength <= 0) return;

        std::vector<std::shared_ptr<LineProbeDefinition>> lineProbeDefinitions;

        for (int i = 0; i < lineProbesLength; i++)
        {
            const DebuggerLineProbeDefinition& current = lineProbes[i];

            if (ProbeIdExists(current.probeId))
            {
                Logger::Debug("[AddLineProbes] Method Probe Id: ", current.probeId, " is already processed.");
                continue;
            }

            const shared::WSTRING& probeId = shared::WSTRING(current.probeId);
            const shared::WSTRING& probeFilePath = shared::WSTRING(current.probeFilePath);
            const auto& lineProbe = std::make_shared<LineProbeDefinition>(LineProbeDefinition(
                probeId, current.bytecodeOffset, current.lineNumber, current.mvid, current.methodId, probeFilePath));

            lineProbeDefinitions.push_back(lineProbe);
        }

        if (lineProbeDefinitions.empty())
        {
            Logger::Debug("[AddLineProbes] Early exiting, there are no new line probes to be added.");
            return;
        }

        auto modules = m_corProfiler->module_ids.Get();

        std::promise<std::vector<MethodIdentifier>> promise;
        std::future<std::vector<MethodIdentifier>> future = promise.get_future();
        m_debugger_rejit_preprocessor->EnqueuePreprocessLineProbes(modules.Ref(), lineProbeDefinitions, &promise);

        const auto& lineProbeRequests = future.get();

        if (!lineProbeRequests.empty())
        {
            rejitRequests.insert(lineProbeRequests.begin(), lineProbeRequests.end());
        }
        else
        {
            Logger::Warn("Received empty list of line probe requests from EnqueuePreprocessLineProbes after enqueuing ",
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
/// Re-Instrument is the practice of requesting revert & reijt to restore method(s) to their original form and then
/// re-instrument them. In case a revert was requested for a method, we need to determine if that method have other
/// probes associated with them, in which case we should re-instrument them.
/// </summary>
/// <param name="revertRequests">Methods to revert.</param>
/// <param name="reInstrumentRequests">[OUT] Gets populated with methods that needs to go through
/// re-instrumentation.</param>
void DebuggerProbesInstrumentationRequester::DetermineReInstrumentProbes(
    std::set<MethodIdentifier>& revertRequests, std::set<MethodIdentifier>& reInstrumentRequests) const
{
    if (revertRequests.empty()) return;

    for (const auto& request : revertRequests)
    {
        const auto moduleHandler = m_debugger_rejit_preprocessor->GetOrAddModule(request.moduleId);
        if (moduleHandler == nullptr)
        {
            Logger::Warn("Module handler is returned as null while tried to RemoveProbes, this only happens if "
                         "the RejitHandler has been shutdown. Exiting early from RemoveProbes.");
            return; // Exit from RemoveProbes
        }

        if (moduleHandler->GetModuleMetadata() == nullptr)
        {
            Logger::Error("Could not find the module metadata of method mdToken ", request.methodToken,
                          " while trying to remove a probe");
            continue;
        }

        RejitHandlerModuleMethod* methodHandler = nullptr;
        if (!moduleHandler->TryGetMethod(request.methodToken, &methodHandler))
        {
            Logger::Error("Could not find the correct method mdToken ", request.methodToken,
                          " while trying to remove a probe");
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

// Assumes `m_probes_mutex` is held
bool DebuggerProbesInstrumentationRequester::ProbeIdExists(const WCHAR* probeId)
{
    auto it = std::find_if(m_probes.begin(), m_probes.end(),
                           [&](ProbeDefinition_S const& probeDef) { return probeDef->probeId == probeId; });
    return it != m_probes.end();
}

void DebuggerProbesInstrumentationRequester::InstrumentProbes(
    debugger::DebuggerMethodProbeDefinition* methodProbes, int methodProbesLength,
    debugger::DebuggerLineProbeDefinition* lineProbes, int lineProbesLength,
    debugger::DebuggerMethodSpanProbeDefinition* spanProbes, int spanProbesLength,
    debugger::DebuggerRemoveProbesDefinition* removeProbes, int removeProbesLength)
{
    std::lock_guard lock(m_probes_mutex);

    std::set<MethodIdentifier> revertRequests{};
    RemoveProbes(removeProbes, removeProbesLength, revertRequests);

    std::set<MethodIdentifier> rejitRequests{};
    AddMethodProbes(methodProbes, methodProbesLength, spanProbes, spanProbesLength, rejitRequests);
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
        auto promise = std::make_shared<std::promise<void>>();
        std::future<void> future = promise->get_future();
        m_debugger_rejit_preprocessor->EnqueueRequestRejit(requests, promise, true);
        // wait and get the value from the future<void>
        future.get();
    }

    if (!rejitRequests.empty())
    {
        Logger::Info("About to RequestRejit for ", rejitRequests.size(), " methods.");

        // RequestRejit
        auto promise = std::make_shared<std::promise<void>>();
        std::future<void> future = promise->get_future();
        std::vector<MethodIdentifier> requests(rejitRequests.size());
        std::copy(rejitRequests.begin(), rejitRequests.end(), requests.begin());
        m_debugger_rejit_preprocessor->EnqueueRequestRejit(requests, promise);
        // wait and get the value from the future<void>
        future.get();
    }
}

int DebuggerProbesInstrumentationRequester::GetProbesStatuses(WCHAR** probeIds, int probeIdsLength,
                                                              debugger::DebuggerProbeStatus* probeStatuses)
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
        if (probeIds[probeIndex] == nullptr)
        {
            Logger::Warn("Received null probeId at index ", probeIndex);
            continue;
        }

        const auto& probeId = shared::WSTRING(probeIds[probeIndex]);
        std::shared_ptr<ProbeMetadata> probeMetadata;
        if (ProbesMetadataTracker::Instance()->TryGetMetadata(probeId, probeMetadata))
        {
            if (probeMetadata->status == ProbeStatus::_ERROR)
            {
                probeStatuses[probeStatusesCount] = {probeMetadata->probeId.c_str(),
                                                     probeMetadata->errorMessage.c_str(), probeMetadata->status};
            }
            else
            {
                probeStatuses[probeStatusesCount] = {probeMetadata->probeId.c_str(), /* errorMessage */ nullptr,
                                                     probeMetadata->status};
            }
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

void DebuggerProbesInstrumentationRequester::RequestRejitForLoadedModule(const ModuleID moduleId)
{
    std::vector<std::shared_ptr<MethodProbeDefinition>> methodProbes;

    std::lock_guard lock(m_probes_mutex);

    for (const auto& probe : m_probes)
    {
        const auto methodProbe = std::dynamic_pointer_cast<MethodProbeDefinition>(probe);
        if (methodProbe != nullptr)
        {
            methodProbes.emplace_back(methodProbe);
        }
    }

    if (methodProbes.empty())
    {
        Logger::Debug("[Debugger] There are no Method Probes");
        return;
    }

    const auto numReJITs =
        m_debugger_rejit_preprocessor->RequestRejitForLoadedModules(std::vector<ModuleID>{moduleId}, methodProbes);
    // TODO do it also for line probes (scenario: module loaded (line probe request arrived) & unloaded & loaded)

    Logger::Debug("[Debugger] Total number of ReJIT Requested: ", numReJITs);
}

void DebuggerProbesInstrumentationRequester::ModuleLoadFinished_AddMetadataToModule(const ModuleID moduleId)
{
    auto corProfilerInfo = m_rejit_handler->GetCorProfilerInfo();

    if (corProfilerInfo == nullptr)
    {
        Logger::Error(
            "DebuggerProbesInstrumentationRequester::ModuleLoadFinished_AddMetadataToModule CorProfilerInfo is null. ");
        return;
    }

    const auto& moduleInfo = GetModuleInfo(corProfilerInfo, moduleId);

    if (moduleInfo.IsNGEN() || moduleInfo.IsDynamic() || IsCoreLibOr3rdParty(moduleInfo.assembly.name))
    {
        return;
    }

    Logger::Debug("Requesting Rejit for Module: ", moduleInfo.assembly.name);

    ComPtr<IUnknown> metadataInterfaces;

    Logger::Debug("  Loading Assembly Metadata...");
    auto hr = corProfilerInfo->GetModuleMetaData(moduleInfo.id, ofRead | ofWrite, IID_IMetaDataImport2,
                                                 metadataInterfaces.GetAddressOf());
    if (hr != S_OK)
    {
        Logger::Warn(
            "DebuggerProbesInstrumentationRequester::sAddMetadataToModule failed to get metadata interface for ",
            moduleInfo.id, " ", moduleInfo.assembly.name);
        return;
    }

    ComPtr<IMetaDataImport2> metadataImport = metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
    ComPtr<IMetaDataEmit2> metadataEmit = metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
    ComPtr<IMetaDataAssemblyImport> assemblyImport =
        metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
    ComPtr<IMetaDataAssemblyEmit> assemblyEmit =
        metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);
    std::unique_ptr<AssemblyMetadata> assemblyMetadata =
        std::make_unique<AssemblyMetadata>(GetAssemblyImportMetadata(assemblyImport));
    Logger::Debug("  Assembly Metadata loaded for: ", assemblyMetadata->name, "(", assemblyMetadata->version.str(),
                  ").");

    m_fault_tolerant_method_duplicator->DuplicateAll(moduleId, moduleInfo, metadataImport, metadataEmit);

    // Enumerate the types of the module
    auto typeDefEnum = EnumTypeDefs(metadataImport);
    auto typeDefIterator = typeDefEnum.begin();
    for (; typeDefIterator != typeDefEnum.end(); typeDefIterator = ++typeDefIterator)
    {
        auto typeDef = *typeDefIterator;

        // check if it is a nested type and the parent is our type
        mdTypeDef parentType;
        if (metadataImport->GetNestedClassProps(typeDef, &parentType) != S_OK)
        {
            // not a nested type
            continue;
        }

        bool isImplementStateMAchineInterface = false;
        // check if the nested type implement the IAsyncStateMachine interface
        hr = DebuggerMethodRewriter::IsTypeImplementIAsyncStateMachine(metadataImport, typeDef,
                                                                       isImplementStateMAchineInterface);
        if (FAILED(hr))
        {
            Logger::Warn(
                "DebuggerProbesInstrumentationRequester::ModuleLoadFinished_AddMetadataToModule: failed in call to "
                "DebuggerMethodRewriter::IsTypeImplementIAsyncStateMachine");
            continue;
        }

        if (!isImplementStateMAchineInterface)
        {
            continue;
        }

        const auto typeInfo = GetTypeInfo(metadataImport, typeDef);

        if (typeInfo.isGeneric)
        {
            Logger::Info(
                "Skipping IsFirstEntry field addition as we don't support generic async methods yet. [ModuleId=",
                moduleInfo.id, ", Assembly=", moduleInfo.assembly.name, ", Type=", typeInfo.name,
                ", IsValueType=", typeInfo.valueType, "]");
            continue;
        }

        if (typeInfo.IsStaticClass())
        {
            Logger::Info("skipping IsFirstEntry field addition as we encountered a static class. [ModuleId=",
                         moduleInfo.id, ", Assembly=", moduleInfo.assembly.name, ", Type=", typeInfo.name,
                         ", IsValueType=", typeInfo.valueType, "]");
            continue;
        }

        // The type implements IAsyncStateMachine, we assume it's a state machine generated by the compiler for async
        // method transformation.

        // define a new boolean field in the state machine object to indicate whether we have already entered the
        // MoveNext method (if we have, it means we are re-entering the method as a continuation in a subsequent
        // `await` operation, and should not capture the method parameter values as we do the first time around).

        mdAssemblyRef managed_profiler_assemblyRef = mdAssemblyRefNil;
        hr = assemblyEmit->DefineAssemblyRef(
            managed_profiler_assembly_property.ppbPublicKey, managed_profiler_assembly_property.pcbPublicKey,
            managed_profiler_assembly_property.szName.data(), &managed_profiler_assembly_property.pMetaData,
            &managed_profiler_assembly_property.pulHashAlgId, sizeof(managed_profiler_assembly_property.pulHashAlgId),
            managed_profiler_assembly_property.assemblyFlags, &managed_profiler_assemblyRef);

        if (FAILED(hr) || managed_profiler_assemblyRef == mdAssemblyRefNil)
        {
            Logger::Warn("Failed to resolve assembly ref of the tracer assembly. [ModuleId=", moduleInfo.id,
                         ", Assembly=", moduleInfo.assembly.name, ", Type=", typeInfo.name,
                         ", IsValueType=", typeInfo.valueType, "]");
            return;
        }

        mdTypeRef asyncMethodDebuggerStateTypeRef = mdTypeRefNil;
        hr = metadataEmit->DefineTypeRefByName(managed_profiler_assemblyRef,
                                               managed_profiler_debugger_async_method_state_type.data(),
                                               &asyncMethodDebuggerStateTypeRef);
        if (FAILED(hr) || asyncMethodDebuggerStateTypeRef == mdTypeRefNil)
        {
            Logger::Warn("Failed to define type ref of the AsyncMethodDebuggerState. [ModuleId=", moduleInfo.id,
                         ", Assembly=", moduleInfo.assembly.name, ", Type=", typeInfo.name,
                         ", IsValueType=", typeInfo.valueType, "]");
            return;
        }

        unsigned callTargetStateBuffer;
        auto callTargetStateSize = CorSigCompressToken(asyncMethodDebuggerStateTypeRef, &callTargetStateBuffer);

        COR_SIGNATURE fieldSignature[500];
        unsigned offset = 0;
        fieldSignature[offset++] = IMAGE_CEE_CS_CALLCONV_FIELD;
        fieldSignature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&fieldSignature[offset], &callTargetStateBuffer, callTargetStateSize);

        mdFieldDef isFirstEntry = mdFieldDefNil;
        hr = metadataEmit->DefineField(typeDef, managed_profiler_debugger_is_first_entry_field_name.c_str(),
                                       fdPrivate | mdHideBySig | fdSpecialName, fieldSignature, sizeof(fieldSignature),
                                       0, nullptr, 0, &isFirstEntry);
        if (FAILED(hr))
        {
            Logger::Error("DebuggerProbesInstrumentationRequester::ModuleLoadFinished_AddMetadataToModule: DefineField "
                          "_isFirstEntry failed");
        }

        Logger::Debug("Added IsFirstEntry field [ModuleId=", moduleInfo.id, ", Assembly=", moduleInfo.assembly.name,
                      ", Type=", typeInfo.name, ", IsValueType=", typeInfo.valueType, "]");
    }
}

HRESULT STDMETHODCALLTYPE DebuggerProbesInstrumentationRequester::ModuleLoadFinished(const ModuleID moduleId)
{
    if (!is_debugger_or_exception_debugging_enabled)
    {
        return S_OK;
    }

    // IMPORTANT: The call to `ModuleLoadFinished_AddMetadataToModule` must be in `ModuleLoadFinished` as mutating the
    // layout of types is only feasible prior the type is loaded.s
    ModuleLoadFinished_AddMetadataToModule(moduleId);
    RequestRejitForLoadedModule(moduleId);
    return S_OK;
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
            ProbesMetadataTracker::Instance()->SetErrorProbeStatus(probeId,
                                                                   invalid_probe_failed_to_instrument_method_probe);
        }
    }

    return S_OK;
}

} // namespace debugger