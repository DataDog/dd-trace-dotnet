#include "debugger_rejit_preprocessor.h"

#include "debugger_constants.h"
#include "debugger_method_rewriter.h"
#include "debugger_rejit_handler_module_method.h"
#include "logger.h"
#include "debugger_probes_tracker.h"

namespace debugger
{

// DebuggerRejitPreprocessor

DebuggerRejitPreprocessor::DebuggerRejitPreprocessor(CorProfiler* corProfiler,
                                                     std::shared_ptr<RejitHandler> rejit_handler,
                                                     std::shared_ptr<RejitWorkOffloader> work_offloader) :
    RejitPreprocessor(corProfiler, rejit_handler, work_offloader, RejitterPriority::Critical)
{
}

ULONG DebuggerRejitPreprocessor::PreprocessLineProbes(
    const std::vector<ModuleID>& modules, const std::vector<std::shared_ptr<LineProbeDefinition>>& lineProbes,
    std::vector<MethodIdentifier>& rejitRequests)
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
                Logger::Warn("    * Skipping ", shared::TokenStr(&methodDef), ": the methoddef is not valid!");
                continue;
            }

            // We create a new function info into the heap from the caller functionInfo in the stack, to
            // be used later in the ReJIT process
            auto functionInfo = FunctionInfo(caller);
            auto hr = functionInfo.method_signature.TryParse();
            if (FAILED(hr))
            {
                Logger::Warn("    * Skipping ", functionInfo.method_signature.str(), ": the method signature cannot be parsed.");
                continue;
            }

            auto moduleHandler = GetOrAddModule(moduleInfo.id);
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

                Logger::Debug("ReJIT handler stored metadata for ", moduleInfo.id, " ", moduleInfo.assembly.name,
                             " AppDomain ", moduleInfo.assembly.app_domain_id, " ",
                             moduleInfo.assembly.app_domain_name);

                moduleHandler->SetModuleMetadata(moduleMetadata);
            }

            RejitHandlerModuleMethodCreatorFunc creator =
                [=, functionInfo = functionInfo](const mdMethodDef method, RejitHandlerModule* moduleInScope) {
                    return CreateMethod(method, moduleInScope, functionInfo);
                };

            RejitHandlerModuleMethodUpdaterFunc updater = [=, request = lineProbe](RejitHandlerModuleMethod* method) {
                UpdateMethodInternal(method, request);
            };

            moduleHandler->CreateMethodIfNotExists(methodDef, creator, updater);

            rejitRequests.emplace_back(MethodIdentifier(moduleInfo.id, methodDef));
        }
    }

    const auto rejitCount = (ULONG) rejitRequests.size();

    return rejitCount;
}

void DebuggerRejitPreprocessor::EnqueuePreprocessLineProbes(
    const std::vector<ModuleID>& modulesVector, const std::vector<std::shared_ptr<LineProbeDefinition>>& lineProbes,
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

    if (modulesVector.size() == 0 || lineProbes.size() == 0)
    {
        return;
    }

    Logger::Debug("RejitHandler::EnqueuePreprocessRejitRequests");

    std::function<void()> action = [=, modules = std::move(modulesVector), definitions = std::move(lineProbes),
                                    localRejitRequests = rejitRequests, localPromise = promise]() mutable {
        // Process modules for rejit
        const auto rejitCount = PreprocessLineProbes(modules, definitions, localRejitRequests);

        // Resolve promise
        if (localPromise != nullptr)
        {
            localPromise->set_value(localRejitRequests);
        }
    };

    // Enqueue
    m_work_offloader->Enqueue(std::make_unique<RejitWorkItem>(std::move(action)));
}

void DebuggerRejitPreprocessor::ProcessTypesForRejit(
    std::vector<MethodIdentifier>& rejitRequests, const ModuleInfo& moduleInfo, ComPtr<IMetaDataImport2> metadataImport,
    ComPtr<IMetaDataEmit2> metadataEmit, ComPtr<IMetaDataAssemblyImport> assemblyImport,
    ComPtr<IMetaDataAssemblyEmit> assemblyEmit, const std::shared_ptr<MethodProbeDefinition>& definition,
    const MethodReference& targetMethod)
{
    const auto instrumentationTargetTypeName = definition->target_method.type.name;

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

const MethodReference&
DebuggerRejitPreprocessor::GetTargetMethod(const std::shared_ptr<MethodProbeDefinition>& methodProbe)
{
    return methodProbe->target_method;
}

const bool DebuggerRejitPreprocessor::GetIsDerived(const std::shared_ptr<MethodProbeDefinition>& methodProbe)
{
    return false; // TODO
}

const bool DebuggerRejitPreprocessor::GetIsInterface(const std::shared_ptr<MethodProbeDefinition>& methodProbe)
{
    return false; // TODO
}

const bool
DebuggerRejitPreprocessor::GetIsExactSignatureMatch(const std::shared_ptr<MethodProbeDefinition>& methodProbe)
{
    return methodProbe->is_exact_signature_match;
}

const bool DebuggerRejitPreprocessor::GetIsEnabled(const std::shared_ptr<MethodProbeDefinition>& definition)
{
    return true;
}
const bool DebuggerRejitPreprocessor::SupportsSelectiveEnablement()
{
    return false;
}

bool DebuggerRejitPreprocessor::CheckExactSignatureMatch(ComPtr<IMetaDataImport2>& metadataImport,
    const FunctionInfo& functionInfo, const MethodReference& targetMethod)
{
    const auto numOfArgs = functionInfo.method_signature.NumberOfArguments();
    const auto isStatic = !(functionInfo.method_signature.CallingConvention() & IMAGE_CEE_CS_CALLCONV_HASTHIS);
    const auto thisArg = isStatic ? 0 : 1;
    const auto numOfArgsTargetMethod = targetMethod.signature_types.size();

    // Compare if the current mdMethodDef contains the same number of arguments as the
    // instrumentation target
    if (numOfArgs != numOfArgsTargetMethod - thisArg)
    {
        Logger::Info("    * Skipping ", functionInfo.type.name, ".", functionInfo.name,
                     ": the methoddef doesn't have the right number of arguments (", numOfArgs, " arguments).");
        return false;
    }

    // Compare each mdMethodDef argument type to the instrumentation target
    bool argumentsMismatch = false;
    const auto& methodArguments = functionInfo.method_signature.GetMethodArguments();

    Logger::Debug("    * Comparing signature for method: ", functionInfo.type.name, ".", functionInfo.name);
    for (unsigned int i = 0; i < numOfArgs && (i + thisArg) < numOfArgsTargetMethod; i++)
    {
        const auto argumentTypeName = methodArguments[i].GetTypeTokName(metadataImport);
        const auto integrationArgumentTypeName = targetMethod.signature_types[i + thisArg];
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


const std::unique_ptr<RejitHandlerModuleMethod>
DebuggerRejitPreprocessor::CreateMethod(const mdMethodDef methodDef, RejitHandlerModule* module,
                                        const FunctionInfo& functionInfo,
                                        const std::shared_ptr<MethodProbeDefinition>& methodProbe)
{
    auto faultTolerantRewriter = std::make_unique<fault_tolerant::FaultTolerantRewriter>(m_corProfiler, std::make_unique<DebuggerMethodRewriter>(m_corProfiler), m_rejit_handler);
    return std::make_unique<DebuggerRejitHandlerModuleMethod>(methodDef, module, functionInfo, std::move(faultTolerantRewriter));
}

const std::unique_ptr<RejitHandlerModuleMethod>
DebuggerRejitPreprocessor::CreateMethod(const mdMethodDef methodDef, RejitHandlerModule* module,
                                        const FunctionInfo& functionInfo) const
{
    auto faultTolerantRewriter = std::make_unique<fault_tolerant::FaultTolerantRewriter>(m_corProfiler, std::make_unique<DebuggerMethodRewriter>(m_corProfiler), m_rejit_handler);
    return std::make_unique<DebuggerRejitHandlerModuleMethod>(methodDef, module, functionInfo, std::move(faultTolerantRewriter));
}

void DebuggerRejitPreprocessor::UpdateMethod(RejitHandlerModuleMethod* methodHandler, const std::shared_ptr<MethodProbeDefinition>& methodProbe)
{
    UpdateMethodInternal(methodHandler, methodProbe);
}

void DebuggerRejitPreprocessor::UpdateMethodInternal(RejitHandlerModuleMethod* methodHandler, const std::shared_ptr<ProbeDefinition>& probe)
{
    const auto debuggerMethodHandler = dynamic_cast<DebuggerRejitHandlerModuleMethod*>(methodHandler);

    if (debuggerMethodHandler == nullptr)
    {
        Logger::Warn("Tried to place Debugger Probe on a method that have been instrumented already be the Tracer/Ci "
                     "instrumentation.",
                     "ProbeId: ", probe->probeId);
        ProbesMetadataTracker::Instance()->SetErrorProbeStatus(probe->probeId,
                                                               invalid_probe_method_already_instrumented);
        return;
    }

    debuggerMethodHandler->AddProbe(probe);
    ProbesMetadataTracker::Instance()->AddMethodToProbe(
        probe->probeId, debuggerMethodHandler->GetModule()->GetModuleId(), debuggerMethodHandler->GetMethodDef());
}

void DebuggerRejitPreprocessor::EnqueueNewMethod(const std::shared_ptr<MethodProbeDefinition>& definition,
                                                 ComPtr<IMetaDataImport2>& metadataImport,
                                                 ComPtr<IMetaDataEmit2>& metadataEmit, const ModuleInfo& moduleInfo,
                                                 mdTypeDef typeDef, std::vector<MethodIdentifier>& rejitRequests,
                                                 unsigned methodDef, const FunctionInfo& functionInfo,
                                                 RejitHandlerModule* moduleHandler)
{
    auto [hr, newMethodDef, newFunctionInfo] = 
        PickMethodToRejit(metadataImport, metadataEmit, typeDef, methodDef, functionInfo);
    if (hr != S_OK)
    {
        return;
    }

    RejitPreprocessor::EnqueueNewMethod(definition, metadataImport, metadataEmit, moduleInfo, typeDef, rejitRequests, newMethodDef, newFunctionInfo, moduleHandler);

    Logger::Debug("    * Enqueue for ReJIT [ModuleId=", moduleInfo.id, ", MethodDef=", shared::TokenStr(&methodDef),
                     ", AppDomainId=", moduleHandler->GetModuleMetadata()->app_domain_id,
                     ", Assembly=", moduleHandler->GetModuleMetadata()->assemblyName, ", Type=", newFunctionInfo.type.name,
                     ", Method=", newFunctionInfo.name, "(", newFunctionInfo.method_signature.NumberOfArguments(), " params), Signature=", newFunctionInfo.signature.str(), "]");
}

HRESULT DebuggerRejitPreprocessor::GetMoveNextMethodFromKickOffMethod(const ComPtr<IMetaDataImport2>& metadataImport, mdTypeDef typeDef, mdMethodDef methodDef, 
                                                                      const FunctionInfo& function, mdMethodDef& moveNextMethod, mdTypeDef& nestedAsyncClassOrStruct) 
{
    // TODO: We might consider rewriting this code using CustomAttributeParser [AsyncStateMachine(typeof(<X>d__1))]

    moveNextMethod = mdMethodDefNil;
    nestedAsyncClassOrStruct = mdTypeDefNil;
    bool hasAsyncAttribute;
    auto hr = HasAsyncStateMachineAttribute(metadataImport, methodDef, hasAsyncAttribute);
    IfFailRet(hr);

    if (!hasAsyncAttribute)
    {
        return hr;
    }

    const auto generatedTypeName = ToWSTRING("<") + function.name + ToWSTRING(">");
    const auto typeDefEnum = EnumTypeDefs(metadataImport);
    auto typeDefIterator = typeDefEnum.begin();
    for (; typeDefIterator != typeDefEnum.end(); typeDefIterator = ++typeDefIterator)
    {
        nestedAsyncClassOrStruct = *typeDefIterator;
        const auto typeInfo = GetTypeInfo(metadataImport, nestedAsyncClassOrStruct);

        // search for a state machine compiler generated type
        if (!StartsWith(shared::ToString(typeInfo.name), ToString(generatedTypeName)))
        {
            // not a compiler generated type
            continue;
        }

        // check if it is a nested type and the parent is our type
        mdTypeDef parentType;
        if (metadataImport->GetNestedClassProps(nestedAsyncClassOrStruct, &parentType) != S_OK || parentType != typeDef)
        {
            // not a nested type or it is nested but the parent type is different from what we are looking for
            continue;
        }

        bool isImplementStateMAchineInterface;
        // check if the nested type implement the IAsyncStateMachine interface
        hr = DebuggerMethodRewriter::IsTypeImplementIAsyncStateMachine(metadataImport, nestedAsyncClassOrStruct, isImplementStateMAchineInterface);
        if (FAILED(hr))
        {
            Logger::Error("DebuggerRejitPreprocessor::GetMoveNextMethodFromKickOffMethod: failed in call to DebuggerMethodRewriter::IsTypeImplementIAsyncStateMachine");
            return  hr;
        }

        if (isImplementStateMAchineInterface)
        {
            // only one method called "MoveNext" exists in the state machine so no need to include the signature
            const COR_SIGNATURE* moveNextSig{};
            hr = metadataImport->FindMethod(nestedAsyncClassOrStruct, WStr("MoveNext"), moveNextSig,
                                            0, &moveNextMethod);
            if (FAILED(hr))
            {
                Logger::Error("DebuggerRejitPreprocessor::GetMoveNextMethodFromKickOffMethod: failed to call metadataImport->FindMethod for MoveNext method");
                return  hr;
            }
        }

        if (moveNextMethod != mdMethodDefNil)
        {
            // we found the correct nested type and MoveNext method
            break;
        }
    }
    return S_OK;
}

std::tuple<HRESULT, mdMethodDef, FunctionInfo> DebuggerRejitPreprocessor::TransformKickOffToMoveNext(
                                                    const ComPtr<IMetaDataImport2>& metadataImport, const ComPtr<IMetaDataEmit2>& metadataEmit,
                                                    mdMethodDef moveNextMethod, mdTypeDef nestedAsyncClassOrStruct)
{
    // save the MoveNext method and create a function info for it
    auto caller = GetFunctionInfo(metadataImport, moveNextMethod);
    if (!caller.IsValid())
    {
        Logger::Error("DebuggerRejitPreprocessor::TransformKickOffToMoveNext: The methoddef: ",
                      shared::TokenStr(&moveNextMethod), " is not valid!");
        return {E_FAIL, mdMethodDefNil, FunctionInfo()};
    }

    const auto hr = caller.method_signature.TryParse();
    if (FAILED(hr))
    {
        Logger::Error("DebuggerRejitPreprocessor::TransformKickOffToMoveNext: The method signature: ",
                      caller.method_signature.str(), " cannot be parsed.");
        return {hr, mdMethodDefNil, FunctionInfo()};
    }

    return {S_OK, moveNextMethod, FunctionInfo(caller)};
}

/**
 * \brief In certain cases, the method that we have in our hand (methodDef, typeDef and functionInfo) is not what we need to instrument,
 * e.g. in async method we have the kickoff method but we need to instrument the MoveNext method,
 * In the future, this may be applicable to other cases (e.g. if the user is trying to instrument an abstract method,
 * we should actually instrument the methods that override and implement it, etc etc).
 */
std::tuple<HRESULT, mdMethodDef, FunctionInfo> DebuggerRejitPreprocessor::PickMethodToRejit(const ComPtr<IMetaDataImport2>& metadataImport,
                                                                                            const ComPtr<IMetaDataEmit2>& metadataEmit,
                                                                                            mdTypeDef typeDef, mdMethodDef methodDef,
                                                                                            const FunctionInfo& functionInfo) const
{
    mdMethodDef moveNextMethod;
    mdTypeDef nestedAsyncClassOrStruct;
    HRESULT hr = GetMoveNextMethodFromKickOffMethod(metadataImport, typeDef, methodDef, functionInfo, moveNextMethod, nestedAsyncClassOrStruct);
    if (FAILED(hr))
    {
        Logger::Error("DebuggerRejitPreprocessor::TransformMethodToRejit: GetMoveNextMethodFromKickOffMethod method failed");
        return {hr, mdMethodDefNil, FunctionInfo()};
    }

    if (moveNextMethod == mdMethodDefNil)
    {
        Logger::Info("DebuggerRejitPreprocessor::TransformMethodToRejit: MoveNextMethod didn't found. Assuming it's a non-async method");
        return {S_OK, methodDef, functionInfo};
    }

    return TransformKickOffToMoveNext(metadataImport, metadataEmit, moveNextMethod, nestedAsyncClassOrStruct);
}

bool DebuggerRejitPreprocessor::ShouldSkipModule(const ModuleInfo& moduleInfo, const std::shared_ptr<MethodProbeDefinition>& methodProbe)
{
    return false;
}

} // namespace debugger