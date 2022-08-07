#include "rejit_preprocessor.h"
#include "stats.h"
#include "integration.h"
#include "logger.h"
#include "debugger_members.h"

namespace trace
{

// RejitPreprocessor
template <class RejitRequestDefinition>
RejitPreprocessor<RejitRequestDefinition>::RejitPreprocessor(std::shared_ptr<RejitHandler> rejit_handler,
                                                             std::shared_ptr<RejitWorkOffloader> work_offloader) :
    m_rejit_handler(std::move(rejit_handler)), m_work_offloader(std::move(work_offloader))
{
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::ProcessTypeDefForRejit(const RejitRequestDefinition& definition,
                                          ComPtr<IMetaDataImport2>& metadataImport,
                                          ComPtr<IMetaDataEmit2>& metadataEmit,
                                          ComPtr<IMetaDataAssemblyImport>& assemblyImport,
                                          ComPtr<IMetaDataAssemblyEmit>& assemblyEmit, const ModuleInfo& moduleInfo,
                                          const mdTypeDef typeDef, std::vector<MethodIdentifier>& rejitRequests)
{
    auto target_method = GetTargetMethod(definition);
    const bool wildcard_enabled = target_method.method_name == tracemethodintegration_wildcardmethodname;

    Logger::Debug("  Looking for '", target_method.type.name, ".", target_method.method_name,
                  "(", (target_method.signature_types.size() - 1), " params)' method implementation.");

    // Now we enumerate all methods with the same target method name. (All overloads of the method)
    auto enumMethods = Enumerator<mdMethodDef>(
        [&metadataImport, target_method, typeDef](HCORENUM* ptr, mdMethodDef arr[], ULONG max, ULONG* cnt) -> HRESULT {
            if (target_method.method_name == tracemethodintegration_wildcardmethodname)
            {
                return metadataImport->EnumMethods(ptr, typeDef, arr, max, cnt);
            }
            else
            {
                return metadataImport->EnumMethodsWithName(ptr, typeDef, target_method.method_name.c_str(), arr,
                                                           max, cnt);
            }
        },
        [&metadataImport](HCORENUM ptr) -> void { metadataImport->CloseEnum(ptr); });

    auto corProfilerInfo = m_rejit_handler->GetCorProfilerInfo();
    auto pCorAssemblyProperty = m_rejit_handler->GetCorAssemblyProperty();
    auto enable_by_ref_instrumentation = m_rejit_handler->GetEnableByRefInstrumentation();
    auto enable_calltarget_state_by_ref = m_rejit_handler->GetEnableCallTargetStateByRef();

    auto enumIterator = enumMethods.begin();
    for (; enumIterator != enumMethods.end(); enumIterator = ++enumIterator)
    {
        auto methodDef = *enumIterator;

        // Extract the function info from the mdMethodDef
        const auto caller = GetFunctionInfo(metadataImport, methodDef);
        if (!caller.IsValid())
        {
            Logger::Warn("    * The caller for the methoddef: ", shared::TokenStr(&methodDef), " is not valid!");
            continue;
        }

        // We create a new function info into the heap from the caller functionInfo in the stack, to
        // be used later in the ReJIT process
        auto functionInfo = FunctionInfo(caller);
        auto hr = functionInfo.method_signature.TryParse();
        if (FAILED(hr))
        {
            Logger::Warn("    * The method signature: ", functionInfo.method_signature.str(), " cannot be parsed.");
            continue;
        }

        const auto numOfArgs = functionInfo.method_signature.NumberOfArguments();
        if (wildcard_enabled)
        {
            if (tracemethodintegration_wildcard_ignored_methods.find(caller.name) != tracemethodintegration_wildcard_ignored_methods.end() ||
                caller.name.find(tracemethodintegration_setterprefix) == 0 ||
                caller.name.find(tracemethodintegration_getterprefix) == 0)
            {
                Logger::Warn(
                    "    * Skipping enqueue for ReJIT, special method detected during '*' wildcard search [ModuleId=", moduleInfo.id, ", MethodDef=", shared::TokenStr(&methodDef),
                    ", Type=", caller.type.name, ", Method=", caller.name, "(", numOfArgs, " params), Signature=", caller.signature.str(), "]");
                continue;
            }
        }

        auto is_exact_signature_match = GetIsExactSignatureMatch(definition);
        if (is_exact_signature_match)
        {
            // Compare if the current mdMethodDef contains the same number of arguments as the
            // instrumentation target
            if (numOfArgs != target_method.signature_types.size() - 1)
            {
                Logger::Debug("    * The caller for the methoddef: ", caller.name,
                              " doesn't have the right number of arguments (", numOfArgs, " arguments).");
                continue;
            }

            // Compare each mdMethodDef argument type to the instrumentation target
            bool argumentsMismatch = false;
            const auto& methodArguments = functionInfo.method_signature.GetMethodArguments();

            Logger::Debug("    * Comparing signature for method: ", caller.type.name, ".", caller.name);
            for (unsigned int i = 0; i < numOfArgs; i++)
            {
                const auto argumentTypeName = methodArguments[i].GetTypeTokName(metadataImport);
                const auto integrationArgumentTypeName = target_method.signature_types[i + 1];
                Logger::Debug("        -> ", argumentTypeName, " = ", integrationArgumentTypeName);
                if (argumentTypeName != integrationArgumentTypeName && integrationArgumentTypeName != WStr("_"))
                {
                    argumentsMismatch = true;
                    break;
                }
            }
            if (argumentsMismatch)
            {
                Logger::Debug("    * The caller for the methoddef: ", target_method.method_name,
                              " doesn't have the right type of arguments.");
                continue;
            }
        }

        // As we are in the right method, we gather all information we need and stored it in to the
        // ReJIT handler.
        auto moduleHandler = m_rejit_handler->GetOrAddModule(moduleInfo.id);
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

            Logger::Info("ReJIT handler stored metadata for ", moduleInfo.id, " ", moduleInfo.assembly.name,
                         " AppDomain ", moduleInfo.assembly.app_domain_id, " ", moduleInfo.assembly.app_domain_name);

            moduleHandler->SetModuleMetadata(moduleMetadata);
        }

        RejitHandlerModuleMethodCreatorFunc creator = [=, request = definition, functionInfo = functionInfo](
                                                          const mdMethodDef method, RejitHandlerModule* module) {
            return CreateMethod(method, module, functionInfo, request);
        };

        RejitHandlerModuleMethodUpdaterFunc updater = [=, request = definition](RejitHandlerModuleMethod* method) {
            return UpdateMethod(method, request);
        };

        moduleHandler->CreateMethodIfNotExists(methodDef, creator, updater);

        // Store module_id and methodDef to request the ReJIT after analyzing all integrations.
        rejitRequests.emplace_back(MethodIdentifier(moduleInfo.id, methodDef));

        Logger::Debug("    * Enqueue for ReJIT [ModuleId=", moduleInfo.id, ", MethodDef=", shared::TokenStr(&methodDef),
                      ", AppDomainId=", moduleHandler->GetModuleMetadata()->app_domain_id,
                      ", Assembly=", moduleHandler->GetModuleMetadata()->assemblyName, ", Type=", caller.type.name,
                      ", Method=", caller.name, "(", numOfArgs, " params), Signature=", caller.signature.str(), "]");
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
                                                        const std::vector<ModuleID>& modules,
                                                        const std::vector<RejitRequestDefinition>& definitions,
                                                        bool enqueueInSameThread)
{
    std::vector<MethodIdentifier> rejitRequests {};
    const auto rejitCount = PreprocessRejitRequests(modules, definitions, rejitRequests);
    RequestRejit(rejitRequests, enqueueInSameThread);
    return rejitCount;
}

template <class RejitRequestDefinition>
ULONG RejitPreprocessor<RejitRequestDefinition>::RequestRevertForLoadedModules(
    const std::vector<ModuleID>& modules, const std::vector<RejitRequestDefinition>& definitions,
    bool enqueueInSameThread)
{
    std::vector<MethodIdentifier> rejitRequests{};
    const auto rejitCount = PreprocessRejitRequests(modules, definitions, rejitRequests);
    RequestRevert(rejitRequests, enqueueInSameThread);
    return rejitCount;
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::RequestRejit(std::vector<MethodIdentifier>& rejitRequests, bool enqueueInSameThread)
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
            m_rejit_handler->RequestRejit(vtModules, vtMethodDefs);
        }
        else
        {
            m_rejit_handler->EnqueueForRejit(vtModules, vtMethodDefs);
        }
    }
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::RequestRevert(std::vector<MethodIdentifier>& revertRequests, bool enqueueInSameThread)
{
    if (!revertRequests.empty())
    {
        std::vector<ModuleID> vtModules;
        std::vector<mdMethodDef> vtMethodDefs;

        const auto rejitCount = revertRequests.size();
        vtModules.reserve(rejitCount);
        vtMethodDefs.reserve(rejitCount);

        for (const auto& rejitRequest : revertRequests)
        {
            vtModules.push_back(rejitRequest.moduleId);
            vtMethodDefs.push_back(rejitRequest.methodToken);
        }

        if (enqueueInSameThread)
        {
            m_rejit_handler->RequestRevert(vtModules, vtMethodDefs);
        }
        else
        {
            m_rejit_handler->EnqueueForRevert(vtModules, vtMethodDefs);
        }
    }
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::EnqueueRequestRejit(std::vector<MethodIdentifier>& rejitRequests,
    std::promise<void>* promise)
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

    std::function<void()> action = [=, requests = std::move(rejitRequests), promise = promise]() mutable {
        // Process modules for rejit
        RequestRejit(requests, true);

        // Resolve promise
        if (promise != nullptr)
        {
            promise->set_value();
        }
    };

    // Enqueue
    m_work_offloader->Enqueue(std::make_unique<RejitWorkItem>(std::move(action)));
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::EnqueueRequestRevert(std::vector<MethodIdentifier>& revertRequests,
                                                                    std::promise<void>* promise)
{
    if (m_rejit_handler->IsShutdownRequested())
    {
        if (promise != nullptr)
        {
            promise->set_value();
        }

        return;
    }

    if (revertRequests.size() == 0)
    {
        return;
    }

    Logger::Debug("RejitHandler::EnqueueRequestRevert");

    std::function<void()> action = [=, requests = std::move(revertRequests), promise = promise]() mutable {
        // Process modules for rejit
        RequestRevert(requests, true);

        // Resolve promise
        if (promise != nullptr)
        {
            promise->set_value();
        }
    };

    // Enqueue
    m_work_offloader->Enqueue(std::make_unique<RejitWorkItem>(std::move(action)));
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::EnqueueRequestRejitForLoadedModules(
    const std::vector<ModuleID>& modulesVector, const std::vector<RejitRequestDefinition>& definitions,
    std::promise<ULONG>* promise)
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
                                    promise = promise]() mutable {
        // Process modules for rejit
        const auto rejitCount = RequestRejitForLoadedModules(modules, definitions, true);

        // Resolve promise
        if (promise != nullptr)
        {
            promise->set_value(rejitCount);
        }
    };

    // Enqueue
    m_work_offloader->Enqueue(std::make_unique<RejitWorkItem>(std::move(action)));
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::EnqueueRequestRevertForLoadedModules(
    const std::vector<ModuleID>& modulesVector, const std::vector<RejitRequestDefinition>& definitions,
    std::promise<ULONG>* promise)
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

    Logger::Debug("RejitHandler::EnqueueRequestRevertForLoadedModules");

    std::function<void()> action = [=, modules = std::move(modulesVector), definitions = std::move(definitions),
                                    promise = promise]() mutable {
        // Process modules for rejit
        const auto rejitCount = RequestRevertForLoadedModules(modules, definitions, true);

        // Resolve promise
        if (promise != nullptr)
        {
            promise->set_value(rejitCount);
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

            if (is_derived)
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
                    auto ancestorTypeInfo = typeInfo.extend_from.get();

                    // Check if the type has ancestors
                    int maxDepth = 1;
                    while (ancestorTypeInfo != nullptr && maxDepth > 0)
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

                ProcessTypesForRejit(rejitRequests, moduleInfo, metadataImport, metadataEmit, assemblyImport, assemblyEmit, definition, target_method);
            }
        }
    }

    const auto rejitCount = (ULONG) rejitRequests.size();

    return rejitCount;
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::EnqueuePreprocessRejitRequests(const std::vector<ModuleID>& modulesVector, const std::vector<RejitRequestDefinition>& definitions,
    std::promise<std::vector<MethodIdentifier>>* promise)
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
                                    rejitRequests = rejitRequests,
                                    promise = promise]() mutable {

        // Process modules for rejit
        const auto rejitCount = PreprocessRejitRequests(modules, definitions, rejitRequests);

        // Resolve promise
        if (promise != nullptr)
        {
            promise->set_value(rejitRequests);
        }
    };

    // Enqueue
    m_work_offloader->Enqueue(std::make_unique<RejitWorkItem>(std::move(action)));
}

template <class RejitRequestDefinition>
void RejitPreprocessor<RejitRequestDefinition>::UpdateMethod(RejitHandlerModuleMethod* method,
                                                             const RejitRequestDefinition& definition)
{
}

// TraceIntegrationRejitPreprocessor

const MethodReference& TracerRejitPreprocessor::GetTargetMethod(const IntegrationDefinition& integrationDefinition)
{
    return integrationDefinition.target_method;
}

const bool TracerRejitPreprocessor::GetIsDerived(const IntegrationDefinition& integrationDefinition)
{
    return integrationDefinition.is_derived;
}

const bool TracerRejitPreprocessor::GetIsExactSignatureMatch(const IntegrationDefinition& integrationDefinition)
{
    return integrationDefinition.is_exact_signature_match;
}

const std::unique_ptr<RejitHandlerModuleMethod> TracerRejitPreprocessor::CreateMethod(const mdMethodDef methodDef, RejitHandlerModule* module,
                                                const FunctionInfo& functionInfo,
                                                const IntegrationDefinition& integrationDefinition)
{
    return std::make_unique<TracerRejitHandlerModuleMethod>(methodDef,
                                                                       module,
                                                                       functionInfo,
                                                                       integrationDefinition);
}

bool TracerRejitPreprocessor::ShouldSkipModule(const ModuleInfo& moduleInfo, const IntegrationDefinition& integrationDefinition)
{
    // If the integration is not for the current assembly we skip.

    const auto target_method = GetTargetMethod(integrationDefinition);

    return target_method.type.assembly.name != tracemethodintegration_assemblyname &&
           target_method.type.assembly.name != moduleInfo.assembly.name;
}

template class RejitPreprocessor<debugger::MethodProbeDefinition>;
template class RejitPreprocessor<IntegrationDefinition>;

} // namespace trace