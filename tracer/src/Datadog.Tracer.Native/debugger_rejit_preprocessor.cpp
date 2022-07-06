#include "debugger_rejit_preprocessor.h"

#include "debugger_constants.h"
#include "debugger_rejit_handler_module_method.h"
#include "logger.h"
#include "probes_tracker.h"

namespace debugger
{

// DebuggerRejitPreprocessor

ULONG DebuggerRejitPreprocessor::PreprocessLineProbes(const std::vector<ModuleID>& modules,
    const LineProbeDefinitions& lineProbes, std::vector<MethodIdentifier>& rejitRequests) const
{
    if (m_rejit_handler->IsShutdownRequested())
    {
        return 0;
    }

    auto corProfilerInfo = m_rejit_handler->GetCorProfilerInfo();
    auto pCorAssemblyProperty = m_rejit_handler->GetCorAssemblyProperty();
    auto enable_by_ref_instrumentation = m_rejit_handler->GetEnableByRefInstrumentation();
    auto enable_calltarget_state_by_ref = m_rejit_handler->GetEnableCallTargetStateByRef();

    for (const auto& module : modules)
    {
        const ModuleInfo& moduleInfo = GetModuleInfo(corProfilerInfo, module);
        Logger::Debug("Requesting Rejit for Module: ", moduleInfo.assembly.name);

        ComPtr<IUnknown> metadataInterfaces;
        ComPtr<IMetaDataImport2> metadataImport;
        ComPtr<IMetaDataEmit2> metadataEmit;
        ComPtr<IMetaDataAssemblyImport> assemblyImport;
        ComPtr<IMetaDataAssemblyEmit> assemblyEmit;
        std::unique_ptr<AssemblyMetadata> assemblyMetadata = nullptr;

        Logger::Debug("  Loading Assembly Metadata...");
        auto hr = corProfilerInfo->GetModuleMetaData(moduleInfo.id, ofRead | ofWrite, IID_IMetaDataImport2,
                                                     metadataInterfaces.GetAddressOf());
        if (FAILED(hr))
        {
            Logger::Warn("CallTarget_RequestRejitForModule failed to get metadata interface for ", moduleInfo.id, " ",
                         moduleInfo.assembly.name);
            break;
        }

        metadataImport = metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
        metadataEmit = metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
        assemblyImport = metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
        assemblyEmit = metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);
        assemblyMetadata = std::make_unique<AssemblyMetadata>(GetAssemblyImportMetadata(assemblyImport));
        Logger::Debug("  Assembly Metadata loaded for: ", assemblyMetadata->name, "(", assemblyMetadata->version.str(),
                      ").");

        WCHAR moduleName[MAX_PACKAGE_NAME];
        ULONG nSize;
        GUID analyzingModuleMvid;
        hr = metadataImport->GetScopeProps(moduleName, MAX_PACKAGE_NAME, &nSize, &analyzingModuleMvid);

        if (FAILED(hr))
        {
            // TODO Log
            continue;
        }

        for (const auto& lineProbe : lineProbes)
        {
            if (lineProbe->mvid != analyzingModuleMvid)
            {
                continue;
            }

            const auto methodDef = lineProbe->methodId;

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

            auto moduleHandler = m_rejit_handler->GetOrAddModule(moduleInfo.id);
            if (moduleHandler == nullptr)
            {
                Logger::Warn("Module handler is null, this only happens if the RejitHandler has been shutdown.");
                break;
            }
            if (moduleHandler->GetModuleMetadata() == nullptr)
            {
                Logger::Debug("Creating ModuleMetadata...");

                const auto moduleMetadata = new ModuleMetadata(
                    metadataImport, metadataEmit, assemblyImport, assemblyEmit, moduleInfo.assembly.name,
                    moduleInfo.assembly.app_domain_id, pCorAssemblyProperty, enable_by_ref_instrumentation,
                    enable_calltarget_state_by_ref);

                Logger::Info("ReJIT handler stored metadata for ", moduleInfo.id, " ", moduleInfo.assembly.name,
                             " AppDomain ", moduleInfo.assembly.app_domain_id, " ",
                             moduleInfo.assembly.app_domain_name);

                moduleHandler->SetModuleMetadata(moduleMetadata);
            }

            RejitHandlerModuleMethodCreatorFunc creator =
                [=, functionInfo = functionInfo](const mdMethodDef method, RejitHandlerModule* moduleInScope) {
                    return CreateMethod(method, moduleInScope, functionInfo);
                };

            RejitHandlerModuleMethodUpdaterFunc updater = [=, request = lineProbe](RejitHandlerModuleMethod* method) {
                return UpdateMethod(method, request);
            };

            moduleHandler->CreateMethodIfNotExists(methodDef, creator, updater);

            rejitRequests.emplace_back(MethodIdentifier(moduleInfo.id, methodDef));
        }
    }

    const auto rejitCount = (ULONG) rejitRequests.size();

    return rejitCount;
}

void DebuggerRejitPreprocessor::EnqueuePreprocessLineProbes(const std::vector<ModuleID>& modulesVector,
    const LineProbeDefinitions& lineProbes,
    std::promise<std::vector<MethodIdentifier>>* promise) const
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

    if (modulesVector.size() == 0 || lineProbes.size() == 0)
    {
        return;
    }

    Logger::Debug("RejitHandler::EnqueuePreprocessRejitRequests");

    std::function<void()> action = [=, modules = std::move(modulesVector), definitions = std::move(lineProbes),
                                    rejitRequests = rejitRequests, promise = promise]() mutable {
        // Process modules for rejit
        const auto rejitCount = PreprocessLineProbes(modules, definitions, rejitRequests);

        // Resolve promise
        if (promise != nullptr)
        {
            promise->set_value(rejitRequests);
        }
    };

    // Enqueue
    m_work_offloader->Enqueue(std::make_unique<RejitWorkItem>(std::move(action)));
}

mdTypeRef DebuggerRejitPreprocessor::GetIAsyncStateMachineToken(ModuleMetadata* moduleMetadata)
{
    mdTypeRef iasyncStateMachineTypeRef = mdTypeRefNil;
    moduleMetadata->GetDebuggerTokens()->EnsureCorLibTokens();
    auto corlibAssemblyRefToken = moduleMetadata->GetDebuggerTokens()->GetCorLibAssemblyRef();
    if (corlibAssemblyRefToken == mdTokenNil)
    {
        Logger::Warn("Could not get CorLib assembly ref token.");
        return iasyncStateMachineTypeRef;
    }

    auto hr = moduleMetadata->metadata_emit->DefineTypeRefByName(
        corlibAssemblyRefToken, L"System.Runtime.CompilerServices.IAsyncStateMachine", &iasyncStateMachineTypeRef);

    if (FAILED(hr))
    {
        Logger::Warn("Failed to get IAsyncStateMachine token.");
        return mdTypeRefNil;
    }
    
    return iasyncStateMachineTypeRef;
}

void DebuggerRejitPreprocessor::ProcessTypesForRejit(
    std::vector<MethodIdentifier>& rejitRequests, const ModuleInfo& moduleInfo, ComPtr<IMetaDataImport2> metadataImport,
    ComPtr<IMetaDataEmit2> metadataEmit, ComPtr<IMetaDataAssemblyImport> assemblyImport,
    ComPtr<IMetaDataAssemblyEmit> assemblyEmit, const MethodProbeDefinition& definition,
    const MethodReference& targetMethod)
{
    const auto instrumentationTargetTypeName = definition.target_method.type.name;

    // Is full name requested?
    auto nameParts = shared::Split(instrumentationTargetTypeName, '.');

    if (nameParts.size() > 1)
    {
        RejitPreprocessor::ProcessTypesForRejit(rejitRequests, moduleInfo, metadataImport, metadataEmit, assemblyImport,
                                                assemblyEmit, definition, targetMethod);
        return;
    }

    // We received a single identifier as the class name. Find every class that has this name, while searching through
    // all namespaces, and also taking into account the possibility that it's a nested class.
    const auto asAnyNamespace = shared::ToString(WStr(".") + instrumentationTargetTypeName);
    const auto asNestedType = shared::ToString(WStr("+") + instrumentationTargetTypeName);

    // Enumerate the types of the module
    auto typeDefEnum = EnumTypeDefs(metadataImport);
    auto typeDefIterator = typeDefEnum.begin();
    for (; typeDefIterator != typeDefEnum.end(); typeDefIterator = ++typeDefIterator)
    {
        auto typeDef = *typeDefIterator;
        const auto typeInfo = GetTypeInfo(metadataImport, typeDef);

        if (typeInfo.name == instrumentationTargetTypeName ||
            EndsWith(shared::ToString(typeInfo.name), asAnyNamespace) ||
            EndsWith(shared::ToString(typeInfo.name), asNestedType))
        {
            // Now that we found the type, look for the methods within that type we want to instrument
            RejitPreprocessor::ProcessTypeDefForRejit(definition, metadataImport, metadataEmit, assemblyImport,
                                                      assemblyEmit, moduleInfo, typeDef, rejitRequests);
        }
    }
}

const MethodReference& DebuggerRejitPreprocessor::GetTargetMethod(const MethodProbeDefinition& methodProbe)
{
    return methodProbe.target_method;
}

const bool DebuggerRejitPreprocessor::GetIsDerived(const MethodProbeDefinition& methodProbe)
{
    return false; // TODO
}

const bool DebuggerRejitPreprocessor::GetIsExactSignatureMatch(const MethodProbeDefinition& methodProbe)
{
    return methodProbe.is_exact_signature_match;
}

const std::unique_ptr<RejitHandlerModuleMethod>
DebuggerRejitPreprocessor::CreateMethod(const mdMethodDef methodDef, RejitHandlerModule* module,
                                        const FunctionInfo& functionInfo, const MethodProbeDefinition& methodProbe)
{
    return std::make_unique<DebuggerRejitHandlerModuleMethod>(methodDef, module, functionInfo);
}

const std::unique_ptr<RejitHandlerModuleMethod>
DebuggerRejitPreprocessor::CreateMethod(const mdMethodDef methodDef, RejitHandlerModule* module,
                                        const FunctionInfo& functionInfo) const
{
    return std::make_unique<DebuggerRejitHandlerModuleMethod>(methodDef, module, functionInfo);
}

void DebuggerRejitPreprocessor::UpdateMethod(RejitHandlerModuleMethod* methodHandler,
                                             const MethodProbeDefinition& methodProbe)
{
    UpdateMethod(methodHandler, std::make_shared<MethodProbeDefinition>(methodProbe));
}

void DebuggerRejitPreprocessor::UpdateMethod(RejitHandlerModuleMethod* methodHandler, const ProbeDefinition_S& probe)
{
    const auto debuggerMethodHandler = dynamic_cast<DebuggerRejitHandlerModuleMethod*>(methodHandler);
    debuggerMethodHandler->AddProbe(probe);
    ProbesMetadataTracker::Instance()->AddMethodToProbe(
        probe->probeId, debuggerMethodHandler->GetModule()->GetModuleId(), debuggerMethodHandler->GetMethodDef());
}

void DebuggerRejitPreprocessor::EnqueueNewMethod(const MethodProbeDefinition& definition,
                                                 ComPtr<IMetaDataImport2>& metadataImport,
                                                 ComPtr<IMetaDataEmit2>& metadataEmit, const ModuleInfo& moduleInfo,
                                                 mdTypeDef typeDef, std::vector<MethodIdentifier>& rejitRequests,
                                                 unsigned methodDef, FunctionInfo functionInfo,
                                                 RejitHandlerModule* moduleHandler)
{
    const auto [newMethodDef, newFunctionInfo] = DebuggerRejitPreprocessor::TransformKickOffToMoveNext(
        metadataImport, metadataEmit, moduleHandler, typeDef, methodDef, functionInfo);
    RejitPreprocessor::EnqueueNewMethod(definition, metadataImport, metadataEmit, moduleInfo, typeDef, rejitRequests,
                                        newMethodDef, newFunctionInfo, moduleHandler);
}

std::tuple<mdMethodDef, FunctionInfo> DebuggerRejitPreprocessor::TransformKickOffToMoveNext(
    const ComPtr<IMetaDataImport2>& metadataImport, const ComPtr<IMetaDataEmit2>& metadataEmit,
    RejitHandlerModule* moduleHandler, mdTypeDef typeDef, mdMethodDef methodDef, const FunctionInfo& functionInfo) const
{
    // an alternative way is to use CustomAttributeParser

    const auto iasyncStateMachineTypeRef = DebuggerRejitPreprocessor::GetIAsyncStateMachineToken(moduleHandler->GetModuleMetadata());
    if (iasyncStateMachineTypeRef == mdTypeRefNil)
    {
        Logger::Error("DebuggerRejitPreprocessor::TransformKickOffToMoveNext: IAsyncStateMachine type ref token is mdTypeRefNil");
        return {methodDef, functionInfo};
    }

    auto generatedTypeName = WStr("<") + functionInfo.name + WStr(">");
    mdMethodDef moveNextMethod = mdMethodDefNil;
    mdTypeDef nestedAsyncClassOrStruct = mdTypeDefNil;
    const auto typeDefEnum = EnumTypeDefs(metadataImport);
    auto typeDefIterator = typeDefEnum.begin();
    for (; typeDefIterator != typeDefEnum.end(); typeDefIterator = ++typeDefIterator)
    {
        nestedAsyncClassOrStruct = *typeDefIterator;
        const auto typeInfo = GetTypeInfo(metadataImport, nestedAsyncClassOrStruct);

        if (!StartsWith(shared::ToString(typeInfo.name), ToString(generatedTypeName)))
        {
            // not a compiler generated type
            continue;
        }

        // if it is a nested type and the parent is our type
        mdTypeDef parentType;
        if (metadataImport->GetNestedClassProps(nestedAsyncClassOrStruct, &parentType) != S_OK || parentType != typeDef)
        {
            // not a nested type or it is nested but the parent type is different from what we are looking for
            continue;
        }

        HCORENUM interfaceImplsEnum = nullptr;
        ULONG actualImpls;
        mdInterfaceImpl impls;

        // check if the nested type implement the IAsyncStateMachine interface
        if (SUCCEEDED(metadataImport->EnumInterfaceImpls(&interfaceImplsEnum, nestedAsyncClassOrStruct,
            &impls, 1, &actualImpls)))
        {
            if (actualImpls != 1)
            {
                // our compiler generated nested type should implement exactly one interface
                break;
            }

            mdToken classToken, interfaceToken;
            // get the interface token
            if (FAILED(metadataImport->GetInterfaceImplProps(impls, &classToken, &interfaceToken)))
            {
                Logger::Warn("DebuggerRejitPreprocessor::TransformKickOffToMoveNext: failed to get interface props");
                break;
            }

            WCHAR type_name[kNameMaxSize]{};
            DWORD type_name_len = 0;
            mdAssembly assemblyToken;
            if (FAILED(metadataImport->GetTypeRefProps(interfaceToken, &assemblyToken, type_name, kNameMaxSize, &type_name_len)))
            {
                Logger::Warn("DebuggerRejitPreprocessor::TransformKickOffToMoveNext: failed to get type ref props");
                break;
            }

            // if the interface is the IAsyncStateMachine
            if (classToken == nestedAsyncClassOrStruct && type_name == debugger_iasync_state_machine_name)
            {
                // only one MoveNext exist in the state machine so no need to include signature
                COR_SIGNATURE* moveNextSig{};
                if (FAILED(metadataImport->FindMethod(nestedAsyncClassOrStruct, WStr("MoveNext"), moveNextSig,
                                                     0, &moveNextMethod)))
                {
                    Logger::Error("DebuggerRejitPreprocessor::TransformKickOffToMoveNext: failed to find the MoveNext method");
                }
                break;
            }
        }

        metadataImport->CloseEnum(interfaceImplsEnum);

        if (moveNextMethod != mdMethodDefNil)
        {
            // we found the correct nested type and MoveNext method
            break;
        }
    }

    if (moveNextMethod == mdMethodDefNil)
    {
        return {methodDef, functionInfo};
    }

    // this is an async method and we found the generated nested state machine type,
    // define a new field in the state machine object to indicate if we are in the first entry to moveNext method
    BYTE field_signature[] = {IMAGE_CEE_CS_CALLCONV_FIELD, ELEMENT_TYPE_BOOLEAN};
    mdFieldDef isFirstEntry = mdFieldDefNil;
    auto hr = metadataEmit->DefineField(nestedAsyncClassOrStruct, WStr("<>dd_liveDebugger_isFirstEntryToMoveNext"), fdPrivate, field_signature,
                                        sizeof(field_signature), 0, nullptr, 0, &isFirstEntry);
    if (FAILED(hr))
    {
        Logger::Warn("DebuggerRejitPreprocessor::TransformKickOffToMoveNext: DefineField _isFirstEntry failed");
        return {methodDef, functionInfo};
    }

    // save the MoveNext method and create a function info for it
    auto caller = GetFunctionInfo(metadataImport, moveNextMethod);
    if (!caller.IsValid())
    {
        Logger::Warn("DebuggerRejitPreprocessor::TransformKickOffToMoveNext: The caller for the methoddef: ",
                     shared::TokenStr(&methodDef), " is not valid!");
        return {methodDef, functionInfo};
    }

    hr = caller.method_signature.TryParse();
    if (FAILED(hr))
    {
        Logger::Warn("DebuggerRejitPreprocessor::TransformKickOffToMoveNext: The method signature: ",
                     caller.method_signature.str(), " cannot be parsed.");
        return {methodDef, functionInfo};
    }

    return {moveNextMethod, FunctionInfo(caller)};
}

bool DebuggerRejitPreprocessor::ShouldSkipModule(const ModuleInfo& moduleInfo, const MethodProbeDefinition& methodProbe)
{
    return false;
}

} // namespace debugger