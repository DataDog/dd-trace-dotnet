#include "rejit_preprocessor.h"
#include "debugger_members.h"
#include "fault_tolerant_tracker.h"
#include "function_control_wrapper.h"
#include "integration.h"
#include "logger.h"
#include "stats.h"

namespace trace
{
// Rejitter

Rejitter::Rejitter(std::shared_ptr<RejitHandler> handler, RejitterPriority priority) :
    m_rejitHandler(handler), m_priority(priority)
{
    handler->RegisterRejitter(this);
}

Rejitter::~Rejitter()
{
}

// RejitPreprocessor
template <class RejitRequestDefinition>
RejitPreprocessor<RejitRequestDefinition>::RejitPreprocessor(CorProfiler* corProfiler,
                                                             std::shared_ptr<RejitHandler> rejit_handler,
                                                             std::shared_ptr<RejitWorkOffloader> work_offloader,
                                                             RejitterPriority priority) :
    Rejitter(rejit_handler, priority),
    m_corProfiler(corProfiler),
    m_rejit_handler(std::move(rejit_handler)),
    m_work_offloader(std::move(work_offloader))
{
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::Shutdown()
{
    Logger::Debug("RejitPreprocessor::Shutdown");

    std::lock_guard<std::mutex> moduleGuard(m_modules_lock);
    std::lock_guard<std::mutex> ngenModuleGuard(m_ngenInlinersModules_lock);

    m_modules.clear();
    m_ngenInlinersModules.clear();
}

template <class RejitRequestDefinition>
RejitHandlerModule* RejitPreprocessor<RejitRequestDefinition>::GetOrAddModule(ModuleID moduleId)
{
    if (m_rejit_handler->IsShutdownRequested())
    {
        return nullptr;
    }

    std::lock_guard<std::mutex> guard(m_modules_lock);
    auto find_res = m_modules.find(moduleId);
    if (find_res != m_modules.end())
    {
        return find_res->second.get();
    }

    RejitHandlerModule* moduleHandler = new RejitHandlerModule(moduleId, m_rejit_handler.get());
    m_modules[moduleId] = std::unique_ptr<RejitHandlerModule>(moduleHandler);
    return moduleHandler;
}

template <class RejitRequestDefinition>
bool RejitPreprocessor<RejitRequestDefinition>::HasModuleAndMethod(ModuleID moduleId, mdMethodDef methodDef)
{
    if (m_rejit_handler->IsShutdownRequested())
    {
        return false;
    }

    std::lock_guard<std::mutex> guard(m_modules_lock);
    auto find_res = m_modules.find(moduleId);
    if (find_res != m_modules.end())
    {
        auto moduleHandler = find_res->second.get();
        return moduleHandler->ContainsMethod(methodDef);
    }

    return false;
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::RemoveModule(ModuleID moduleId)
{
    if (m_rejit_handler->IsShutdownRequested())
    {
        return;
    }

    // Removes the RejitHandlerModule instance
    std::lock_guard<std::mutex> modulesGuard(m_modules_lock);
    m_modules.erase(moduleId);

    // Removes the moduleID from the inliners vector
    std::lock_guard<std::mutex> inlinersGuard(m_ngenInlinersModules_lock);
    m_ngenInlinersModules.erase(std::remove(m_ngenInlinersModules.begin(), m_ngenInlinersModules.end(), moduleId),
                                m_ngenInlinersModules.end());
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::AddNGenInlinerModule(ModuleID moduleId)
{
    if (m_rejit_handler->IsShutdownRequested())
    {
        // If the shutdown was requested, we return.
        return;
    }

    // Process the inliner module list ( to catch any incomplete data module )
    // and also check if the module is already in the inliners list
    std::lock_guard<std::mutex> modulesGuard(m_modules_lock);
    std::lock_guard<std::mutex> inlinersGuard(m_ngenInlinersModules_lock);

    bool alreadyAdded = false;
    for (const auto& moduleInliner : m_ngenInlinersModules)
    {
        if (moduleInliner == moduleId)
        {
            alreadyAdded = true;
        }

        for (const auto& mod : m_modules)
        {
            mod.second->RequestRejitForInlinersInModule(moduleInliner);
        }
    }

    // If the module is not in the inliners list we added and request rejit for it.
    if (!alreadyAdded)
    {
        // Add the new module inliner
        m_ngenInlinersModules.push_back(moduleId);

        for (const auto& mod : m_modules)
        {
            mod.second->RequestRejitForInlinersInModule(moduleId);
        }
    }
}

template <class RejitRequestDefinition>
HRESULT RejitPreprocessor<RejitRequestDefinition>::RejitMethod(FunctionControlWrapper& functionControl)
{
    auto moduleId = functionControl.GetModuleId();
    auto methodId = functionControl.GetMethodId();

    auto moduleHandler = GetOrAddModule(moduleId);
    if (moduleHandler == nullptr)
    {
        return S_FALSE;
    }

    RejitHandlerModuleMethod* methodHandler = nullptr;
    if (!moduleHandler->TryGetMethod(methodId, &methodHandler))
    {
        return S_FALSE;
    }

    if (methodHandler->GetMethodDef() == mdMethodDefNil)
    {
        Logger::Warn("RejitMethod: mdMethodDef is missing for "
                     "MethodDef: ",
                     methodId);
        return S_FALSE;
    }

    if (methodHandler->GetFunctionInfo() == nullptr)
    {
        Logger::Warn("RejitMethod: FunctionInfo is missing for "
                     "MethodDef: ",
                     methodId);
        return S_FALSE;
    }

    if (moduleHandler->GetModuleId() == 0)
    {
        Logger::Warn("RejitMethod: ModuleID is missing for "
                     "MethodDef: ",
                     methodId);
        return S_FALSE;
    }

    if (moduleHandler->GetModuleMetadata() == nullptr)
    {
        Logger::Warn("RejitMethod: ModuleMetadata is missing for "
                     "MethodDef: ",
                     methodId);
        return S_FALSE;
    }

    auto rewriter = methodHandler->GetMethodRewriter();

    if (rewriter == nullptr)
    {
        Logger::Error("RejitMethod: The rewriter is missing for "
                      "MethodDef: ",
                      methodId, ", methodHandler type name = ", typeid(methodHandler).name());
        return S_FALSE;
    }

    return rewriter->Rewrite(moduleHandler, methodHandler, (ICorProfilerFunctionControl*) &functionControl,
                             (ICorProfilerInfo*) &functionControl);
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::EnqueueFaultTolerantMethods(
    const RejitRequestDefinition& definition, ComPtr<IMetaDataImport2>& metadataImport,
    ComPtr<IMetaDataEmit2>& metadataEmit, const ModuleInfo& moduleInfo, const mdTypeDef typeDef,
    std::vector<MethodIdentifier>& rejitRequests, unsigned methodDef, const FunctionInfo& functionInfo,
    RejitHandlerModule* moduleHandler)
{
    if (fault_tolerant::FaultTolerantTracker::Instance()->IsKickoffMethod(moduleInfo.id, methodDef))
    {
        const auto originalMethod =
            fault_tolerant::FaultTolerantTracker::Instance()->GetOriginalMethod(moduleInfo.id, methodDef);
        RejitPreprocessor::EnqueueNewMethod(definition, metadataImport, metadataEmit, moduleInfo, typeDef,
                                            rejitRequests, originalMethod, functionInfo, moduleHandler);

        const auto instrumentedMethod =
            fault_tolerant::FaultTolerantTracker::Instance()->GetInstrumentedMethod(moduleInfo.id, methodDef);
        RejitPreprocessor::EnqueueNewMethod(definition, metadataImport, metadataEmit, moduleInfo, typeDef,
                                            rejitRequests, instrumentedMethod, functionInfo, moduleHandler);
    }
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::EnqueueNewMethod(const RejitRequestDefinition& definition,
                                                                 ComPtr<IMetaDataImport2>& metadataImport,
                                                                 ComPtr<IMetaDataEmit2>& metadataEmit,
                                                                 const ModuleInfo& moduleInfo, const mdTypeDef typeDef,
                                                                 std::vector<MethodIdentifier>& rejitRequests,
                                                                 unsigned methodDef, const FunctionInfo& functionInfo,
                                                                 RejitHandlerModule* moduleHandler)
{
    EnqueueFaultTolerantMethods(definition, metadataImport, metadataEmit, moduleInfo, typeDef, rejitRequests, methodDef,
                                functionInfo, moduleHandler);

    RejitHandlerModuleMethodCreatorFunc creator =
        [=, request = definition, fInfo = functionInfo](const mdMethodDef method, RejitHandlerModule* module) {
            return CreateMethod(method, module, fInfo, request);
        };

    RejitHandlerModuleMethodUpdaterFunc updater = [=, request = definition](RejitHandlerModuleMethod* method) {
        return UpdateMethod(method, request);
    };

    moduleHandler->CreateMethodIfNotExists(methodDef, creator, updater);

    // Store module_id and methodDef to request the ReJIT after analyzing all integrations.
    rejitRequests.emplace_back(MethodIdentifier(moduleInfo.id, methodDef));
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::ProcessTypeDefForRejit(
    const RejitRequestDefinition& definition, ComPtr<IMetaDataImport2>& metadataImport,
    ComPtr<IMetaDataEmit2>& metadataEmit, ComPtr<IMetaDataAssemblyImport>& assemblyImport,
    ComPtr<IMetaDataAssemblyEmit>& assemblyEmit, const ModuleInfo& moduleInfo, const mdTypeDef typeDef,
    std::vector<MethodIdentifier>& rejitRequests)
{
    auto target_method = GetTargetMethod(definition);
    auto is_interface = GetIsInterface(definition);
    const bool wildcard_enabled = target_method.method_name == tracemethodintegration_wildcardmethodname;
    const bool iterate_explicit_interface_methods = is_interface && !wildcard_enabled;

    Logger::Debug("  Looking for '", target_method.type.name, ".", target_method.method_name, "(",
                  (target_method.signature_types.size() - 1), " params)' method implementation.");

    // Now we enumerate all methods with the same target method name. (All overloads of the method)
    auto enumMethods = Enumerator<mdMethodDef>(
        [&metadataImport, target_method, typeDef](HCORENUM* ptr, mdMethodDef arr[], ULONG max, ULONG* cnt) -> HRESULT {
            if (target_method.method_name == tracemethodintegration_wildcardmethodname)
            {
                return metadataImport->EnumMethods(ptr, typeDef, arr, max, cnt);
            }
            else
            {
                return metadataImport->EnumMethodsWithName(ptr, typeDef, target_method.method_name.c_str(), arr, max,
                                                           cnt);
            }
        },
        [&metadataImport](HCORENUM ptr) -> void { metadataImport->CloseEnum(ptr); });
    auto enumExplicitInterfaceMethods = Enumerator<mdMethodDef>(
        [&metadataImport, target_method, typeDef](HCORENUM* ptr, mdMethodDef arr[], ULONG max, ULONG* cnt) -> HRESULT {
            auto method_name = target_method.type.name + WStr(".") + target_method.method_name;
            return metadataImport->EnumMethodsWithName(ptr, typeDef, method_name.c_str(), arr, max, cnt);
        },
        [&metadataImport](HCORENUM ptr) -> void { metadataImport->CloseEnum(ptr); });

    auto corProfilerInfo = m_rejit_handler->GetCorProfilerInfo();
    auto pCorAssemblyProperty = m_rejit_handler->GetCorAssemblyProperty();
    auto enable_by_ref_instrumentation = m_rejit_handler->GetEnableByRefInstrumentation();
    auto enable_calltarget_state_by_ref = m_rejit_handler->GetEnableCallTargetStateByRef();

    auto enumIterator = iterate_explicit_interface_methods ? enumExplicitInterfaceMethods.begin() : enumMethods.begin();
    auto iteratorEnd = enumMethods.end();
    auto explicitMode = iterate_explicit_interface_methods;

    for (; enumIterator != iteratorEnd; enumIterator = ++enumIterator)
    {
        // When interface methods are being iterated first we go over the explicit interface method search and then
        // switch to the regular method search, just like the runtime behaves.
        if (iterate_explicit_interface_methods && !(enumIterator != enumExplicitInterfaceMethods.end()))
        {
            enumIterator = enumMethods.begin();
            explicitMode = false;

            // Immediately exit if the second enumerator has 0 entries
            if (!(enumIterator != iteratorEnd))
            {
                break;
            }
        }

        auto methodDef = *enumIterator;

        // Extract the function info from the mdMethodDef
        const auto caller = GetFunctionInfo(metadataImport, methodDef);
        if (!caller.IsValid())
        {
            Logger::Warn("    * Skipping ", shared::TokenStr(&methodDef), ": the methoddef is not valid!");
            continue;
        }

        // We create a new function info into the heap from the caller functionInfo in the stack, to
        // be used later in the ReJIT process
        auto functionInfo = FunctionInfo(caller);
        auto hr = functionInfo.method_signature.TryParse();
        if (FAILED(hr))
        {
            Logger::Warn("    * Skipping ", functionInfo.method_signature.str(),
                         ": the method signature cannot be parsed.");
            continue;
        }

        if (wildcard_enabled)
        {
            if (tracemethodintegration_wildcard_ignored_methods.find(caller.name) !=
                    tracemethodintegration_wildcard_ignored_methods.end() ||
                caller.name.find(tracemethodintegration_setterprefix) == 0 ||
                caller.name.find(tracemethodintegration_getterprefix) == 0)
            {
                Logger::Warn(
                    "    * Skipping enqueue for ReJIT, special method detected during '*' wildcard search [ModuleId=",
                    moduleInfo.id, ", MethodDef=", shared::TokenStr(&methodDef), ", Type=", caller.type.name,
                    ", Method=", caller.name, "(", functionInfo.method_signature.NumberOfArguments(),
                    " params), Signature=", caller.signature.str(), "]");
                continue;
            }
        }

        if (GetIsExactSignatureMatch(definition) && !CheckExactSignatureMatch(metadataImport, functionInfo, target_method))
        {
            continue;
        }

        // As we are in the right method, we gather all information we need and stored it in to the
        // ReJIT handler.
        auto moduleHandler = GetOrAddModule(moduleInfo.id);
        if (moduleHandler == nullptr)
        {
            Logger::Warn("Module handler is null, this only happens if the RejitHandler has been shutdown.");
            break;
        }
        if (moduleHandler->GetModuleMetadata() == nullptr)
        {
            Logger::Debug("Creating ModuleMetadata...");

            const auto moduleMetadata =
                new ModuleMetadata(metadataImport, metadataEmit, assemblyImport, assemblyEmit, moduleInfo.assembly.name,
                                   moduleInfo.assembly.app_domain_id, pCorAssemblyProperty,
                                   enable_by_ref_instrumentation, enable_calltarget_state_by_ref);

            Logger::Debug("ReJIT handler stored metadata for ", moduleInfo.id, " ", moduleInfo.assembly.name,
                          " AppDomain ", moduleInfo.assembly.app_domain_id, " ", moduleInfo.assembly.app_domain_name);

            moduleHandler->SetModuleMetadata(moduleMetadata);
        }

        Logger::Info("Method enqueued for ReJIT for ", target_method.type.name, ".", target_method.method_name, "(",
                     (target_method.signature_types.size() - 1), " params).");
        EnqueueNewMethod(definition, metadataImport, metadataEmit, moduleInfo, typeDef, rejitRequests, methodDef,
                         functionInfo, moduleHandler);

        // If we are in the explicit enumerator and we found the method, we don't look into the normal methods
        // enumerators.
        if (explicitMode)
        {
            Logger::Debug("      Explicit interface implementation found, skipping normal methods search. [",
                          caller.type.name, ".", caller.name, "]");
            break;
        }
    }
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::ProcessTypesForRejit(
    std::vector<MethodIdentifier>& rejitRequests, const ModuleInfo& moduleInfo, ComPtr<IMetaDataImport2> metadataImport,
    ComPtr<IMetaDataEmit2> metadataEmit, ComPtr<IMetaDataAssemblyImport> assemblyImport,
    ComPtr<IMetaDataAssemblyEmit> assemblyEmit, const RejitRequestDefinition& definition,
    const MethodReference& targetMethod)
{
    // We are in the right module, so we try to load the mdTypeDef from the integration target type name.
    mdTypeDef typeDef = mdTypeDefNil;
    auto foundType = FindTypeDefByName(targetMethod.type.name, moduleInfo.assembly.name, metadataImport, typeDef);
    if (!foundType)
    {
        return;
    }

    //
    // Looking for the method to rewrite
    //
    ProcessTypeDefForRejit(definition, metadataImport, metadataEmit, assemblyImport, assemblyEmit, moduleInfo, typeDef,
                           rejitRequests);
}

template <class RejitRequestDefinition>
ULONG RejitPreprocessor<RejitRequestDefinition>::RequestRejitForLoadedModules(
    const std::vector<ModuleID>& modules, const std::vector<RejitRequestDefinition>& definitions,
    bool enqueueInSameThread)
{
    std::vector<MethodIdentifier> rejitRequests{};
    const auto rejitCount = PreprocessRejitRequests(modules, definitions, rejitRequests);
    RequestRejit(rejitRequests, enqueueInSameThread);
    return rejitCount;
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::RequestRejit(std::vector<MethodIdentifier>& rejitRequests,
                                                             bool enqueueInSameThread, bool callRevertExplicitly)
{
    if (!rejitRequests.empty())
    {
        std::vector<ModuleID> vtModules;
        std::vector<mdMethodDef> vtMethodDefs;

        const auto rejitCount = rejitRequests.size();
        vtModules.reserve(rejitCount);
        vtMethodDefs.reserve(rejitCount);

        for (const auto& rejitRequest : rejitRequests)
        {
            vtModules.push_back(rejitRequest.moduleId);
            vtMethodDefs.push_back(rejitRequest.methodToken);
        }

        if (enqueueInSameThread)
        {
            m_rejit_handler->RequestRejit(vtModules, vtMethodDefs, callRevertExplicitly);
        }
        else
        {
            m_rejit_handler->EnqueueForRejit(vtModules, vtMethodDefs, nullptr, callRevertExplicitly);
        }
    }
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::EnqueueRequestRejit(std::vector<MethodIdentifier>& rejitRequests,
                                                                    std::shared_ptr<std::promise<void>> promise,
                                                                    bool callRevertExplicitly)
{
    if (m_rejit_handler->IsShutdownRequested())
    {
        if (promise != nullptr)
        {
            promise->set_value();
        }

        return;
    }

    if (rejitRequests.size() == 0)
    {
        return;
    }

    Logger::Debug("RejitHandler::EnqueueRequestRejit");

    std::function<void()> action = [=, requests = std::move(rejitRequests), localPromise = promise,
                                    callRevertExplicitly = callRevertExplicitly]() mutable {
        // Process modules for rejit
        RequestRejit(requests, true, callRevertExplicitly);

        // Resolve promise
        if (localPromise != nullptr)
        {
            localPromise->set_value();
        }
    };

    // Enqueue
    m_work_offloader->Enqueue(std::make_unique<RejitWorkItem>(std::move(action)));
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::EnqueueRequestRejitForLoadedModules(
    const std::vector<ModuleID>& modulesVector, const std::vector<RejitRequestDefinition>& definitions,
    std::shared_ptr<std::promise<ULONG>> promise)
{
    if (m_rejit_handler->IsShutdownRequested())
    {
        if (promise != nullptr)
        {
            promise->set_value(0);
        }

        return;
    }

    if (modulesVector.size() == 0 || definitions.size() == 0)
    {
        return;
    }

    Logger::Debug("RejitHandler::EnqueueRequestRejitForLoadedModules");

    std::function<void()> action = [=, modules = std::move(modulesVector), definitions = std::move(definitions),
                                    localPromise = promise]() mutable {
        // Process modules for rejit
        const auto rejitCount = RequestRejitForLoadedModules(modules, definitions, true);

        // Resolve promise
        if (localPromise != nullptr)
        {
            localPromise->set_value(rejitCount);
        }
    };

    // Enqueue
    m_work_offloader->Enqueue(std::make_unique<RejitWorkItem>(std::move(action)));
}

template <class RejitRequestDefinition>
ULONG RejitPreprocessor<RejitRequestDefinition>::PreprocessRejitRequests(
    const std::vector<ModuleID>& modules, const std::vector<RejitRequestDefinition>& definitions,
    std::vector<MethodIdentifier>& rejitRequests)
{
    if (m_rejit_handler->IsShutdownRequested())
    {
        return 0;
    }

    auto corProfilerInfo = m_rejit_handler->GetCorProfilerInfo();

    for (const auto& module : modules)
    {
        auto _ = trace::Stats::Instance()->CallTargetRequestRejitMeasure();
        const ModuleInfo& moduleInfo = GetModuleInfo(corProfilerInfo, module);
        Logger::Debug("Requesting Rejit for Module: ", moduleInfo.assembly.name);

        ComPtr<IUnknown> metadataInterfaces;
        ComPtr<IMetaDataImport2> metadataImport;
        ComPtr<IMetaDataEmit2> metadataEmit;
        ComPtr<IMetaDataAssemblyImport> assemblyImport;
        ComPtr<IMetaDataAssemblyEmit> assemblyEmit;
        std::unique_ptr<AssemblyMetadata> assemblyMetadata = nullptr;

        for (const RejitRequestDefinition& definition : definitions)
        {
            const auto target_method = GetTargetMethod(definition);
            const auto is_derived = GetIsDerived(definition);
            const auto is_interface = GetIsInterface(definition);
            const auto is_enabled = GetIsEnabled(definition);

            if (is_derived || is_interface)
            {
                // Abstract methods handling.
                if (assemblyMetadata == nullptr)
                {
                    Logger::Debug("  Loading Assembly Metadata...");
                    auto hr = corProfilerInfo->GetModuleMetaData(moduleInfo.id, ofRead | ofWrite, IID_IMetaDataImport2,
                                                                 metadataInterfaces.GetAddressOf());
                    if (FAILED(hr))
                    {
                        Logger::Warn("CallTarget_RequestRejitForModule failed to get metadata interface for ",
                                     moduleInfo.id, " ", moduleInfo.assembly.name);
                        break;
                    }

                    metadataImport = metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
                    metadataEmit = metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
                    assemblyImport = metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
                    assemblyEmit = metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);
                    assemblyMetadata = std::make_unique<AssemblyMetadata>(GetAssemblyImportMetadata(assemblyImport));
                    Logger::Debug("  Assembly Metadata loaded for: ", assemblyMetadata->name, "(",
                                  assemblyMetadata->version.str(), ").");
                }

                // If the integration is in a different assembly than the target method
                if (assemblyMetadata->name != target_method.type.assembly.name)
                {
                    // Check if the current module contains a reference to the assembly of the integration
                    auto assemblyRefEnum = EnumAssemblyRefs(assemblyImport);
                    auto assemblyRefIterator = assemblyRefEnum.begin();
                    bool assemblyRefFound = false;
                    for (; assemblyRefIterator != assemblyRefEnum.end(); assemblyRefIterator = ++assemblyRefIterator)
                    {
                        auto assemblyRef = *assemblyRefIterator;
                        const auto& assemblyRefMetadata = GetReferencedAssemblyMetadata(assemblyImport, assemblyRef);

                        if (assemblyRefMetadata.name == target_method.type.assembly.name &&
                            target_method.type.min_version <= assemblyRefMetadata.version &&
                            target_method.type.max_version >= assemblyRefMetadata.version)
                        {
                            assemblyRefFound = true;
                            break;
                        }
                    }

                    // If the assembly reference was not found we skip the integration
                    if (!assemblyRefFound)
                    {
                        continue;
                    }
                }

                // Enumerate the types of the module in search of types implementing the integration
                auto typeDefEnum = EnumTypeDefs(metadataImport);
                auto typeDefIterator = typeDefEnum.begin();
                for (; typeDefIterator != typeDefEnum.end(); typeDefIterator = ++typeDefIterator)
                {
                    auto typeDef = *typeDefIterator;
                    const auto typeInfo = GetTypeInfo(metadataImport, typeDef);
                    bool rewriteType = false;

                    // Iterate through interfaces that this type directly implements and
                    // mark the type for instrumentation if the interface type matches and
                    // the assembly version constraints are met
                    //
                    // Note: If this type implements an interface indirectly through a base class,
                    // then the instrumentation would occur when that base class is processed.
                    //
                    // Note: This leaves a gap in instrumentation when the following scenario occurs:
                    // - ClassA implements InterfaceA (with method Method1)
                    // - ClassB derives from ClassA
                    // - ClassB overrides Method1
                    if (is_interface)
                    {
                        auto interfaceImplEnum = EnumInterfaceImpls(metadataImport, typeDef);
                        auto interfaceImplIterator = interfaceImplEnum.begin();
                        for (; interfaceImplIterator != interfaceImplEnum.end();
                             interfaceImplIterator = ++interfaceImplIterator)
                        {
                            // Get the interface token
                            auto interfaceImpl = *interfaceImplIterator;
                            mdToken classToken, interfaceToken;
                            if (metadataImport->GetInterfaceImplProps(interfaceImpl, &classToken, &interfaceToken) ==
                                S_OK)
                            {
                                if (classToken == typeDef)
                                {
                                    // Get the interface type props
                                    WCHAR type_name[kNameMaxSize]{};
                                    DWORD type_name_len = 0;

                                    const auto interfaceTokenType = TypeFromToken(interfaceToken);
                                    if (interfaceTokenType == mdtTypeRef)
                                    {
                                        mdAssembly assemblyToken;
                                        if (metadataImport->GetTypeRefProps(interfaceToken, &assemblyToken, type_name,
                                                                            kNameMaxSize, &type_name_len) == S_OK &&
                                            type_name == target_method.type.name)
                                        {
                                            // Now we have to validate the assembly version
                                            const auto tokenType = TypeFromToken(assemblyToken);
                                            if (tokenType == mdtAssemblyRef)
                                            {
                                                const auto& ancestorAssemblyMetadata =
                                                    GetReferencedAssemblyMetadata(assemblyImport, assemblyToken);

                                                // We check the assembly name and version
                                                if (ancestorAssemblyMetadata.name == target_method.type.assembly.name &&
                                                    target_method.type.min_version <=
                                                        ancestorAssemblyMetadata.version &&
                                                    target_method.type.max_version >= ancestorAssemblyMetadata.version)
                                                {
                                                    rewriteType = true;
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                Logger::Warn("Unknown token type (Not supported)");
                                            }
                                        }
                                    }
                                    else if (interfaceTokenType == mdtTypeDef)
                                    {
                                        DWORD type_flags;
                                        mdToken type_extends = mdTokenNil;
                                        if (metadataImport->GetTypeDefProps(interfaceToken, type_name, kNameMaxSize,
                                                                            &type_name_len, &type_flags,
                                                                            &type_extends) == S_OK &&
                                            type_name == target_method.type.name)
                                        {
                                            // We check the assembly name and version
                                            if (assemblyMetadata->name == target_method.type.assembly.name &&
                                                target_method.type.min_version <= assemblyMetadata->version &&
                                                target_method.type.max_version >= assemblyMetadata->version)
                                            {
                                                rewriteType = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (is_derived)
                    {
                        // Check if the type has ancestors
                        auto ancestorTypeInfo = typeInfo.extend_from.get();
                        int maxDepth = 1;
                        while (!rewriteType && ancestorTypeInfo != nullptr && maxDepth > 0)
                        {
                            // Validate the type name we already have
                            if (ancestorTypeInfo->name == target_method.type.name)
                            {
                                // Validate assembly data (scopeToken has the assemblyRef of the ancestor type)
                                if (ancestorTypeInfo->scopeToken != mdTokenNil)
                                {
                                    const auto tokenType = TypeFromToken(ancestorTypeInfo->scopeToken);

                                    if (tokenType == mdtAssemblyRef)
                                    {
                                        const auto& ancestorAssemblyMetadata =
                                            GetReferencedAssemblyMetadata(assemblyImport, ancestorTypeInfo->scopeToken);

                                        // We check the assembly name and version
                                        if (ancestorAssemblyMetadata.name == target_method.type.assembly.name &&
                                            target_method.type.min_version <= ancestorAssemblyMetadata.version &&
                                            target_method.type.max_version >= ancestorAssemblyMetadata.version)
                                        {
                                            rewriteType = true;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        Logger::Warn("Unknown token type (Not supported)");
                                    }
                                }
                                else
                                {
                                    // Check module name and version
                                    if (moduleInfo.assembly.name == target_method.type.assembly.name &&
                                        target_method.type.min_version <= assemblyMetadata->version &&
                                        target_method.type.max_version >= assemblyMetadata->version)
                                    {
                                        rewriteType = true;
                                        break;
                                    }
                                }
                            }

                            // Go up
                            ancestorTypeInfo = ancestorTypeInfo->extend_from.get();
                            if (ancestorTypeInfo != nullptr)
                            {
                                if (ancestorTypeInfo->name == WStr("System.ValueType") ||
                                    ancestorTypeInfo->name == WStr("System.Object") ||
                                    ancestorTypeInfo->name == WStr("System.Enum"))
                                {
                                    ancestorTypeInfo = nullptr;
                                }
                            }

                            maxDepth--;
                        }
                    }

                    if (rewriteType)
                    {
                        //
                        // Looking for the method to rewrite
                        //
                        ProcessTypeDefForRejit(definition, metadataImport, metadataEmit, assemblyImport, assemblyEmit,
                                               moduleInfo, typeDef, rejitRequests);
                    }
                }
            }
            else
            {
                if (ShouldSkipModule(moduleInfo, definition))
                {
                    continue;
                }

                if (assemblyMetadata == nullptr)
                {
                    Logger::Debug("  Loading Assembly Metadata...");
                    auto hr = corProfilerInfo->GetModuleMetaData(moduleInfo.id, ofRead | ofWrite, IID_IMetaDataImport2,
                                                                 metadataInterfaces.GetAddressOf());
                    if (FAILED(hr))
                    {
                        Logger::Warn("CallTarget_RequestRejitForModule failed to get metadata interface for ",
                                     moduleInfo.id, " ", moduleInfo.assembly.name);
                        break;
                    }

                    metadataImport = metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
                    metadataEmit = metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
                    assemblyImport = metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
                    assemblyEmit = metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);
                    assemblyMetadata = std::make_unique<AssemblyMetadata>(GetAssemblyImportMetadata(assemblyImport));
                    Logger::Debug("  Assembly Metadata loaded for: ", assemblyMetadata->name, "(",
                                  assemblyMetadata->version.str(), ").");
                }

                // Check min version
                if (target_method.type.min_version > assemblyMetadata->version)
                {
                    continue;
                }

                // Check max version
                if (target_method.type.max_version < assemblyMetadata->version)
                {
                    continue;
                }

                ProcessTypesForRejit(rejitRequests, moduleInfo, metadataImport, metadataEmit, assemblyImport,
                                     assemblyEmit, definition, target_method);
            }
        }
    }

    const auto rejitCount = (ULONG) rejitRequests.size();

    return rejitCount;
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::EnqueuePreprocessRejitRequests(
    const std::vector<ModuleID>& modulesVector, const std::vector<RejitRequestDefinition>& definitions,
    std::shared_ptr<std::promise<std::vector<MethodIdentifier>>> promise)
{
    std::vector<MethodIdentifier> rejitRequests;

    if (m_rejit_handler->IsShutdownRequested())
    {
        if (promise != nullptr)
        {
            promise->set_value(rejitRequests);
        }

        return;
    }

    if (modulesVector.size() == 0 || definitions.size() == 0)
    {
        return;
    }

    Logger::Debug("RejitHandler::EnqueuePreprocessRejitRequests");

    std::function<void()> action = [=, modules = std::move(modulesVector), definitions = std::move(definitions),
                                    localRejitRequests = rejitRequests, localPromise = promise]() mutable {
        // Process modules for rejit
        const auto rejitCount = PreprocessRejitRequests(modules, definitions, localRejitRequests);

        // Resolve promise
        if (localPromise != nullptr)
        {
            localPromise->set_value(localRejitRequests);
        }
    };

    // Enqueue
    m_work_offloader->Enqueue(std::make_unique<RejitWorkItem>(std::move(action)));
}

template <class RejitRequestDefinition>
bool RejitPreprocessor<RejitRequestDefinition>::CheckExactSignatureMatch(ComPtr<IMetaDataImport2>& metadataImport, const FunctionInfo& functionInfo, const MethodReference& targetMethod)
{
    const auto numOfArgs = functionInfo.method_signature.NumberOfArguments();

    // Compare if the current mdMethodDef contains the same number of arguments as the
    // instrumentation target
    if (numOfArgs != targetMethod.signature_types.size() - 1)
    {
        Logger::Info("    * Skipping ", functionInfo.type.name, ".", functionInfo.name,
                     ": the methoddef doesn't have the right number of arguments (", numOfArgs, " arguments).");
        return false;
    }

    // Compare each mdMethodDef argument type to the instrumentation target
    bool argumentsMismatch = false;
    const auto& methodArguments = functionInfo.method_signature.GetMethodArguments();

    Logger::Debug("    * Comparing signature for method: ", functionInfo.type.name, ".", functionInfo.name);
    for (unsigned int i = 0; i < numOfArgs; i++)
    {
        const auto argumentTypeName = methodArguments[i].GetTypeTokName(metadataImport);
        const auto integrationArgumentTypeName = targetMethod.signature_types[i + 1];
        Logger::Debug("        -> ", argumentTypeName, " = ", integrationArgumentTypeName);
        if (argumentTypeName != integrationArgumentTypeName && integrationArgumentTypeName != WStr("_"))
        {
            argumentsMismatch = true;
            break;
        }
    }
    if (argumentsMismatch)
    {
        Logger::Info("    * Skipping ", targetMethod.method_name,
                     ": the methoddef doesn't have the right type of arguments.");
        return false;
    }

    return true;
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::UpdateMethod(RejitHandlerModuleMethod* method,
                                                             const RejitRequestDefinition& definition)
{
}

template class RejitPreprocessor<std::shared_ptr<debugger::MethodProbeDefinition>>;
template class RejitPreprocessor<IntegrationDefinition>;

} // namespace trace