#include "debugger_method_rewriter.h"
#include "debugger_rejit_handler_module_method.h"
#include "cor_profiler.h"
#include "debugger_constants.h"
#include "debugger_environment_variables_util.h"
#include "il_rewriter_wrapper.h"
#include "logger.h"
#include "stats.h"
#include "environment_variables_util.h"
#include "debugger_probes_tracker.h"
#include "fault_tolerant_envionrment_variables_util.h"
#include "fault_tolerant_tracker.h"
#include "instrumenting_product.h"

namespace debugger
{

// Get function locals
HRESULT DebuggerMethodRewriter::GetFunctionLocalSignature(const ModuleMetadata& module_metadata, ILRewriter& rewriter, FunctionLocalSignature& localSignature)
{
    PCCOR_SIGNATURE local_signature{nullptr};
    ULONG local_signature_len = 0;
    mdToken localVarSig = rewriter.GetTkLocalVarSig();

    if (localVarSig == 0) // No locals.
    {
        localSignature = {};
        return S_OK;
    }

    HRESULT hr = module_metadata.metadata_import->GetSigFromToken(localVarSig, &local_signature,
                                                                  &local_signature_len);
    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::GetFunctionLocalSignature Failed in GetSigFromToken using localVarSig = ",localVarSig);
        return hr;
    }

    std::vector<TypeSignature> locals;
    hr = FunctionLocalSignature::TryParse(local_signature, local_signature_len, locals);

    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::GetFunctionLocalSignature Failed to parse local signature");
        return E_FAIL;
    }

    localSignature = FunctionLocalSignature(local_signature, local_signature_len, std::move(locals));
    return S_OK;
}

HRESULT DebuggerMethodRewriter::LoadArgument(bool isStatic, const ILRewriterWrapper& rewriterWrapper, int argumentIndex, const TypeSignature& argument) const
{
    // Load the argument into the stack
    const auto [elementType, argTypeFlags] = argument.GetElementTypeAndFlags();
    if (argTypeFlags & TypeFlagByRef)
    {
        rewriterWrapper.LoadArgument(argumentIndex + (isStatic ? 0 : 1));
    }
    else
    {
        rewriterWrapper.LoadArgumentRef(argumentIndex + (isStatic ? 0 : 1));
    }

    return S_OK;
}

HRESULT DebuggerMethodRewriter::LoadLocal(const ILRewriterWrapper& rewriterWrapper, int localIndex, const TypeSignature& local) const
{
    // Load the argument into the stack
    const auto [elementType, localTypeFlags] = local.GetElementTypeAndFlags();
    if (localTypeFlags & TypeFlagByRef)
    {
        rewriterWrapper.LoadLocal(localIndex);
    }
    else
    {
        rewriterWrapper.LoadLocalAddress(localIndex);
    }

    return S_OK;
}

HRESULT DebuggerMethodRewriter::WriteCallsToLogArgOrLocal(
    ModuleMetadata& moduleMetadata,
    DebuggerTokens* debuggerTokens, 
    bool isStatic, 
    const std::vector<TypeSignature>& methodArgsOrLocals, 
    int numArgsOrLocals, 
    ILRewriterWrapper& rewriterWrapper, 
    ULONG callTargetStateIndex, 
    ILInstr** beginCallInstruction,
    bool isArgs,
    ProbeType probeType,
    mdFieldDef isReEntryFieldTok) const
{
    for (auto argOrLocalIndex = 0; argOrLocalIndex < numArgsOrLocals; argOrLocalIndex++)
    {
        const auto argOrLocal = methodArgsOrLocals[argOrLocalIndex];

        const auto [elementType, argTypeFlags] = argOrLocal.GetElementTypeAndFlags();
        
        bool isTypeIsByRefLike = false;
        HRESULT hr = IsTypeByRefLike(m_corProfiler->info_, moduleMetadata, argOrLocal, debuggerTokens->GetCorLibAssemblyRef(), isTypeIsByRefLike);

        if (FAILED(hr))
        {
            Logger::Warn("DebuggerRewriter: Failed to determine if ", isArgs ? "argument" : "local", " index = ", argOrLocalIndex,
                         " is By-Ref like.");
        }
        else if (isTypeIsByRefLike)
        {
            Logger::Warn("DebuggerRewriter: Skipped ", isArgs ? "argument" : "local",
                         " index = ", argOrLocalIndex, " because it's By-Ref like.");
            continue;
        }

        if (argTypeFlags & TypeFlagPinnedType)
        {
            Logger::Warn("DebuggerRewriter: Skipped ", isArgs ? "argument" : "local", " index = ", argOrLocalIndex,
                         " because it's a pinned local.");
            continue;
        }

        if (isArgs)
        {
            hr = LoadArgument(isStatic, rewriterWrapper, argOrLocalIndex, argOrLocal);
        }
        else
        {
            hr = LoadLocal(rewriterWrapper, argOrLocalIndex, argOrLocal);
        }

        if (FAILED(hr))
        {
            Logger::Warn("DebuggerRewriter: Failed load ", isArgs ? "argument" : "local", " index = ", argOrLocalIndex,
                         " into the stack.");
            return E_FAIL;
        }

        // Load the index of the argument/local
        rewriterWrapper.LoadInt32(argOrLocalIndex);

        if (isReEntryFieldTok != mdFieldDefNil)
        {
            rewriterWrapper.LoadArgument(0);
            rewriterWrapper.LoadFieldAddress(isReEntryFieldTok);
        }
        else
        {
            rewriterWrapper.LoadLocalAddress(callTargetStateIndex); 
        }

        if (isArgs)
        {
            hr = debuggerTokens->WriteLogArg(&rewriterWrapper, argOrLocal, beginCallInstruction, probeType);
        }
        else
        {
            hr = debuggerTokens->WriteLogLocal(&rewriterWrapper, argOrLocal, beginCallInstruction, probeType);
        }

        if (FAILED(hr))
        {
            Logger::Warn("DebuggerRewriter: Failed in ", isArgs ? "WriteLogArg" : "WriteLogLocal", " with index=", argOrLocalIndex);
            return E_FAIL;
        }
    }
    return S_FALSE;
}

HRESULT
DebuggerMethodRewriter::WriteCallsToLogArg(ModuleMetadata& moduleMetadata,
                                           DebuggerTokens* debuggerTokens, bool isStatic,
                                             const std::vector<TypeSignature>& args,
                                             int numArgs, ILRewriterWrapper& rewriterWrapper, ULONG callTargetStateIndex,
                                           ILInstr** beginCallInstruction, ProbeType probeType,
                                           mdFieldDef isReEntryFieldTok) const
{
    return WriteCallsToLogArgOrLocal(moduleMetadata, debuggerTokens, isStatic, args, numArgs,
                                     rewriterWrapper,
                                     callTargetStateIndex, beginCallInstruction, /* IsArgs */ true, probeType, isReEntryFieldTok);
}

HRESULT
DebuggerMethodRewriter::WriteCallsToLogLocal(ModuleMetadata& moduleMetadata,
                                             DebuggerTokens* debuggerTokens, bool isStatic,
                                             const std::vector<TypeSignature>& locals,
                                             int numLocals, ILRewriterWrapper& rewriterWrapper, ULONG callTargetStateIndex,
                                             ILInstr** beginCallInstruction,
                                             ProbeType probeType,
                                             mdFieldDef isReEntryFieldTok) const
{
    return WriteCallsToLogArgOrLocal(moduleMetadata, debuggerTokens, isStatic, locals, numLocals,
                                     rewriterWrapper,
                                     callTargetStateIndex, beginCallInstruction, /* IsArgs */ false, probeType, isReEntryFieldTok);
}

HRESULT DebuggerMethodRewriter::LoadInstanceIntoStack(FunctionInfo* caller, bool isStatic,
                                                      const ILRewriterWrapper& rewriterWrapper,
                                                      ILInstr** outLoadArgumentInstr,
                                                      CallTargetTokens* callTargetTokens)
{

    // *** Load instance into the stack (if not static)
    if (isStatic)
    {
        if (caller->type.valueType)
        {
            // Static methods in a ValueType can't be instrumented.
            // In the future this can be supported by adding a local for the valuetype and initialize it to the default
            // value. After the signature modification we need to emit the following IL to initialize and load into the
            // stack.
            //    ldloca.s [localIndex]
            //    initobj [valueType]
            //    ldloc.s [localIndex]
            Logger::Warn(
                "*** DebuggerMethodRewriter::Rewrite() Static methods in a ValueType cannot be instrumented. ");
            return E_FAIL;
        }
        *outLoadArgumentInstr = rewriterWrapper.LoadNull();
    }
    else
    {
        bool callerTypeIsValueType = caller->type.valueType;
        mdToken callerTypeToken = callTargetTokens->GetCurrentTypeRef(&caller->type, callerTypeIsValueType);
        if (callerTypeToken == mdTokenNil)
        {
            *outLoadArgumentInstr = rewriterWrapper.LoadNull();
        }
        else
        {

            *outLoadArgumentInstr = rewriterWrapper.LoadArgument(0);
            if (caller->type.valueType)
            {
                if (caller->type.type_spec != mdTypeSpecNil)
                {
                    rewriterWrapper.LoadObj(caller->type.type_spec);
                }
                else if (!caller->type.isGeneric)
                {
                    rewriterWrapper.LoadObj(caller->type.id);
                }
                else
                {
                    // Generic struct instrumentation is currently not supported.
                    // IMetaDataImport::GetMemberProps and IMetaDataImport::GetMemberRefProps return
                    // the parent token as mdTypeDef and not as a mdTypeSpec,
                    // because the method definition is stored in the mdTypeDef.
                    // The problem is that we don't have the exact Spec of that generic instantiation,  and so
                    // we can't emit LoadObj or Box because that would result in producing invalid IL.
                    // This problem doesn't occur on a  reference types because we can always rely on the System.Object
                    // type.
                    return E_FAIL;
                }
            }
        }
    }

    return S_OK;
}

HRESULT DebuggerMethodRewriter::Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler,
                                        ICorProfilerFunctionControl* pFunctionControl,
                                        ICorProfilerInfo* pCorProfilerInfo)
{
    const auto moduleId = moduleHandler->GetModuleId();
    const auto methodId = methodHandler->GetMethodDef();

    const auto debuggerMethodHandler = dynamic_cast<DebuggerRejitHandlerModuleMethod*>(methodHandler);

    if (debuggerMethodHandler->GetProbes().empty())
    {
        Logger::Warn("NotifyReJITCompilationStarted: Probes are missing for "
                     "MethodDef: ",
                     methodHandler->GetMethodDef());

        return S_FALSE;
    }

    auto _ = trace::Stats::Instance()->CallTargetRewriterCallbackMeasure();

    MethodProbeDefinitions methodProbes;
    LineProbeDefinitions lineProbes;
    SpanProbeOnMethodDefinitions spanOnMethodProbes;

    const auto& probes = debuggerMethodHandler->GetProbes();

    if (probes.empty())
    {
        Logger::Info("There are no probes for methodDef: ", methodHandler->GetMethodDef());
        return S_OK;
    }

    Logger::Info("About to apply debugger instrumentation on ", probes.size(),
                 " probes for methodDef: ", methodHandler->GetMethodDef());

    for (const auto& probe : probes)
    {
        const auto spanProbe = std::dynamic_pointer_cast<SpanProbeOnMethodDefinition>(probe);
        if (spanProbe != nullptr)
        {
            spanOnMethodProbes.emplace_back(spanProbe);
            continue;
        }

        const auto methodProbe = std::dynamic_pointer_cast<MethodProbeDefinition>(probe);
        if (methodProbe != nullptr)
        {
            methodProbes.emplace_back(methodProbe);
            continue;
        }

        const auto lineProbe = std::dynamic_pointer_cast<LineProbeDefinition>(probe);
        if (lineProbe != nullptr)
        {
            lineProbes.emplace_back(lineProbe);
            continue;
        }
    }

    if (methodProbes.empty() && lineProbes.empty() && spanOnMethodProbes.empty())
    {
        // No lines probes & method probes. Should not happen unless the user requested to undo the instrumentation
        // while the method got executed.
        Logger::Info("There are no method probes, lines probes and span probes for methodDef",
                     methodHandler->GetMethodDef());
        return S_OK;
    }
    else
    {
        Logger::Info("Applying ", methodProbes.size(), " method probes, ", lineProbes.size(), " line probes and ",
                     spanOnMethodProbes.size(), " span probes on methodDef: ", methodHandler->GetMethodDef());

        auto hr = Rewrite(moduleHandler, methodHandler, pFunctionControl, pCorProfilerInfo, methodProbes, lineProbes,
                          spanOnMethodProbes);

        if (hr == S_OK)
        {
            MarkAllProbesAsInstrumented(methodProbes, lineProbes, spanOnMethodProbes);
        }

        return FAILED(hr) ? S_FALSE : S_OK;
    }
}

InstrumentingProducts DebuggerMethodRewriter::GetInstrumentingProduct(RejitHandlerModule* moduleHandler,
    RejitHandlerModuleMethod* methodHandler)
{
    return InstrumentingProducts::DynamicInstrumentation;
}

WSTRING DebuggerMethodRewriter::GetInstrumentationId(RejitHandlerModule* moduleHandler,
                                                          RejitHandlerModuleMethod* methodHandler)
{
    const auto debuggerMethodHandler = dynamic_cast<DebuggerRejitHandlerModuleMethod*>(methodHandler);

    if (debuggerMethodHandler == nullptr)
    {
        return EmptyWStr;
    }

    const auto& probes = debuggerMethodHandler->GetProbes();

    if (probes.empty())
    {
        return EmptyWStr;
    }

    MethodProbeDefinitions methodProbes;
    LineProbeDefinitions lineProbes;
    SpanProbeOnMethodDefinitions spanOnMethodProbes;

    for (const auto& probe : probes)
    {
        const auto spanProbe = std::dynamic_pointer_cast<SpanProbeOnMethodDefinition>(probe);
        if (spanProbe != nullptr)
        {
            spanOnMethodProbes.emplace_back(spanProbe);
            continue;
        }

        const auto methodProbe = std::dynamic_pointer_cast<MethodProbeDefinition>(probe);
        if (methodProbe != nullptr)
        {
            methodProbes.emplace_back(methodProbe);
            continue;
        }

        const auto lineProbe = std::dynamic_pointer_cast<LineProbeDefinition>(probe);
        if (lineProbe != nullptr)
        {
            lineProbes.emplace_back(lineProbe);
            continue;
        }
    }

    if (methodProbes.empty() && lineProbes.empty() && spanOnMethodProbes.empty())
    {
        return EmptyWStr;
    }

#ifdef MACOS
    std::stringstream instrumentationIdStream;
    instrumentationIdStream << "M" << methodProbes.size() << "L" << lineProbes.size() << "S"
                            << spanOnMethodProbes.size();
#else
    WSTRINGSTREAM instrumentationIdStream;
    instrumentationIdStream << WStr("M") << methodProbes.size() << WStr("L") << lineProbes.size() << WStr("S")
                            << spanOnMethodProbes.size();
#endif

#ifdef MACOS
    return shared::ToWSTRING(instrumentationIdStream.str());
#else
    return instrumentationIdStream.str();
#endif
}

/// <summary>
/// Performs the following instrumentation on the requested bytecode offset:
///try
///{
///  - Invoke LineDebuggerInvoker.BeginLine with object instance (or null if static method) 
///  - Calls to LineDebuggerInvoker.LogArg with original method arguments
///  - Calls to LineDebuggerInvoker.LogLocal with method locals
///  - LineDebuggerInvoker.EndLine
///}
///catch (Exception)
///{
///  - Store exception into Exception local
///}
/// - Executing the selected sequence point (from the selected bytecode offset)
/// </summary>
HRESULT DebuggerMethodRewriter::CallLineProbe(
    const int instrumentedMethodIndex, 
    ModuleID module_id, 
    ModuleMetadata& module_metadata, 
    FunctionInfo* caller, 
    DebuggerTokens* debuggerTokens, 
    mdToken function_token, 
    bool isStatic, 
    std::vector<TypeSignature>& methodArguments, 
    int numArgs, 
    ILRewriter& rewriter, 
    std::vector<TypeSignature>& methodLocals, 
    int numLocals, 
    ILRewriterWrapper& rewriterWrapper, 
    ULONG lineProbeCallTargetStateIndex, 
    std::vector<EHClause>& lineProbesEHClauses, 
    const std::vector<ILInstr*>& branchTargets, 
    const std::shared_ptr<LineProbeDefinition>& lineProbe,
    bool isAsyncMethod) const
{
    const auto& lineProbeId = lineProbe->probeId;
    const auto& bytecodeOffset = lineProbe->bytecodeOffset;
    const auto& probeLineNumber = lineProbe->lineNumber;
    const auto& probeFilePath = lineProbe->probeFilePath;
    const auto& probeType = isAsyncMethod ? AsyncLineProbe : NonAsyncLineProbe;

    ILInstr* lineProbeFirstInstruction;
    auto hr = rewriter.GetInstrFromOffset(bytecodeOffset, &lineProbeFirstInstruction);

    if (FAILED(hr))
    {
        // Note we are not sabotaging the whole rewriting upon failure to lookup for a specific bytecode offset.
        ProbesMetadataTracker::Instance()->SetErrorProbeStatus(lineProbe->probeId, line_probe_il_offset_lookup_failure);
        return E_NOTIMPL;
    }

    if (lineProbeFirstInstruction->m_opcode == CEE_NOP || lineProbeFirstInstruction->m_opcode == CEE_BR_S ||
        lineProbeFirstInstruction->m_opcode == CEE_BR)
    {
        if (lineProbeFirstInstruction->m_pNext == rewriterWrapper.GetILRewriter()->GetILList())
        {
            // Note we are not sabotaging the whole rewriting upon failure to lookup for a specific bytecode offset.
            ProbesMetadataTracker::Instance()->SetErrorProbeStatus(lineProbe->probeId, line_probe_il_offset_lookup_failure_2);
            return E_NOTIMPL;
        }

        lineProbeFirstInstruction = lineProbeFirstInstruction->m_pNext;
    }

    const auto prevInstruction = lineProbeFirstInstruction->m_pPrev;

    rewriterWrapper.SetILPosition(lineProbeFirstInstruction);

    // ***
    // BEGIN LINE PART
    // ***

    // Define ProbeId as string
    mdString lineProbeIdToken;
    hr = module_metadata.metadata_emit->DefineUserString(
        lineProbeId.data(), static_cast<ULONG>(lineProbeId.length()), &lineProbeIdToken);

    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() DefineUserStringFailed. MethodProbeId = ", lineProbeId,
                     " module_id= ", module_id, ", functon_token=", function_token);
        return E_FAIL;
    }

    // Define ProbeLocation as string
    mdString lineProbeFilePathToken;
    hr = module_metadata.metadata_emit->DefineUserString(
        probeFilePath.data(), static_cast<ULONG>(probeFilePath.length()), &lineProbeFilePathToken);

    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() DefineUserStringFailed. lineProbeId = ", lineProbeId,
                     " module_id= ", module_id, ", functon_token=", function_token);
        return hr;
    }

    rewriterWrapper.LoadStr(lineProbeIdToken);

    int probeIndex;
    if (!ProbesMetadataTracker::Instance()->TryGetNextInstrumentedProbeIndex(lineProbeId, module_id, function_token, probeIndex))
    {
        Logger::Warn("*** DebuggerMethodRewriter::CallLineProbe() TryGetNextInstrumentedProbeIndex failed with. lineProbeId = ", lineProbeId,
                     " module_id= ", module_id, ", functon_token=", function_token);
        return E_FAIL;
    }

    rewriterWrapper.LoadInt32(probeIndex);

    ILInstr* loadInstanceInstr;
    hr = LoadInstanceIntoStack(caller, isStatic, rewriterWrapper, &loadInstanceInstr, debuggerTokens);

    IfFailRet(hr);

    rewriterWrapper.LoadToken(function_token);
    rewriterWrapper.LoadToken(caller->type.id);
    rewriterWrapper.LoadInt32(instrumentedMethodIndex);
    rewriterWrapper.LoadInt32(probeLineNumber);
    rewriterWrapper.LoadStr(lineProbeFilePathToken);

    // *** Emit BeginLine call

    ILInstr* beginLineCallInstruction;
    hr = debuggerTokens->WriteBeginLine(&rewriterWrapper, &caller->type, &beginLineCallInstruction, probeType);

    IfFailRet(hr);

    rewriterWrapper.StLocal(lineProbeCallTargetStateIndex);

    // *** Emit LogLocal call(s)
    hr = WriteCallsToLogLocal(module_metadata, debuggerTokens, isStatic, methodLocals, numLocals,
                              rewriterWrapper,
                              lineProbeCallTargetStateIndex, &beginLineCallInstruction, probeType);

    IfFailRet(hr);

    if (probeType == NonAsyncLineProbe) // The signature of MoveNext is without arguments.
    {
        // *** Emit LogArg call(s)
        hr = WriteCallsToLogArg(module_metadata, debuggerTokens, isStatic, methodArguments,
                                numArgs, rewriterWrapper,
                                lineProbeCallTargetStateIndex, &beginLineCallInstruction, probeType);   
    }

    IfFailRet(hr);

    // Load the DebuggerState
    rewriterWrapper.LoadLocalAddress(lineProbeCallTargetStateIndex);
    hr = debuggerTokens->WriteEndLine(&rewriterWrapper, &beginLineCallInstruction, probeType);

    IfFailRet(hr);

    AdjustBranchTargets(lineProbeFirstInstruction, prevInstruction->m_pNext, branchTargets);
    AdjustExceptionHandlingClauses(lineProbeFirstInstruction, prevInstruction->m_pNext, &rewriter);

    return S_OK;
}

HRESULT DebuggerMethodRewriter::ApplyLineProbes(
    const int instrumentedMethodIndex,
    LineProbeDefinitions& lineProbes, 
    ModuleID module_id, 
    ModuleMetadata& module_metadata, 
    FunctionInfo* caller, 
    DebuggerTokens* debuggerTokens, 
    mdToken function_token, 
    bool isStatic, 
    std::vector<TypeSignature>& methodArguments, 
    int numArgs, 
    ILRewriter& rewriter, 
    std::vector<TypeSignature>& methodLocals, 
    int numLocals, 
    ILRewriterWrapper& rewriterWrapper, 
    ULONG lineProbeCallTargetStateIndex, 
    std::vector<EHClause>& newClauses,
    bool isAsyncMethod) const
{
    if (isAsyncMethod && caller->type.isGeneric && caller->type.valueType)
    {
        Logger::Warn("Async generic methods in optimized code are not supported at the moment. Skipping on placing ",
                     lineProbes.size(), " line probe(s).");
        MarkAllLineProbesAsError(lineProbes, line_probe_in_async_generic_method_in_optimized_code);
        return E_NOTIMPL;
    }

    Logger::Info("Applying ", lineProbes.size(), " line probe(s) instrumentation.");

    const auto& branchTargets = GetBranchTargets(&rewriter);

    for (const auto& lineProbe : lineProbes)
    {
        HRESULT hr = CallLineProbe(instrumentedMethodIndex, module_id, module_metadata, caller, debuggerTokens,
                           function_token, isStatic, methodArguments, numArgs, rewriter, methodLocals, numLocals, rewriterWrapper, lineProbeCallTargetStateIndex, newClauses,
                                   branchTargets, lineProbe, isAsyncMethod);
        if (hr == E_NOTIMPL)
        {
            // Appropriate error message and Probe Status is already logged in CallLineProbe.
            return E_NOTIMPL;
        }

        if (FAILED(hr))
        {
            Logger::Warn("Failed to apply line probe instrumentation.");
            return hr;
        }
    }

    return S_OK;
}

HRESULT DebuggerMethodRewriter::ApplyMethodProbe(
    const MethodProbeDefinitions& methodProbes,
    ModuleID module_id, 
    ModuleMetadata& module_metadata, 
    FunctionInfo* caller, 
    DebuggerTokens* debuggerTokens, 
    mdToken function_token, 
    TypeSignature retFuncArg, 
    bool isVoid, 
    bool isStatic, 
    const std::vector<TypeSignature>& methodArguments, 
    int numArgs, 
    ILRewriter& rewriter, 
    const std::vector<TypeSignature>& methodLocals, 
    int numLocals, 
    ILRewriterWrapper& rewriterWrapper, 
    ULONG callTargetStateIndex, 
    ULONG exceptionIndex, 
    ULONG callTargetReturnIndex, 
    ULONG returnValueIndex,
    ULONG multiProbeStatesIndex,
    mdToken callTargetReturnToken,
    ILInstr* firstInstruction, 
    const int instrumentedMethodIndex, 
    ILInstr* const& beforeLineProbe,
    std::vector<EHClause>& newClauses) const
{
    LogDebugCallerInfo(caller, instrumentedMethodIndex);

    const auto isMultiProbe = methodProbes.size() > 1;
    const auto probeType = isMultiProbe ? NonAsyncMethodMultiProbe : NonAsyncMethodSingleProbe;
    const auto stateLocalIndex = isMultiProbe ? multiProbeStatesIndex : callTargetStateIndex;

    const auto& methodProbeId = methodProbes[0]->probeId;

    rewriterWrapper.SetILPosition(beforeLineProbe);

    const auto& branchTargets = GetBranchTargets(rewriterWrapper.GetILRewriter());

    for (const auto& branchInstr : branchTargets)
    {
        if (branchInstr->m_pTarget == beforeLineProbe)
        {
            branchInstr->m_pTarget = nullptr;
        }
    }

    const auto tryInstruction = beforeLineProbe->m_pPrev;

    const auto instrumentationVersion = ProbesMetadataTracker::Instance()->GetNextInstrumentationVersion();

    // ***
    // BEGIN METHOD PART
    // ***

    ILInstr* beginCallInstruction;

    rewriterWrapper.LoadInt32(instrumentedMethodIndex);
    rewriterWrapper.LoadInt32(instrumentationVersion);

    auto hr = debuggerTokens->WriteShouldUpdateProbeInfo(&rewriterWrapper, &beginCallInstruction, probeType);
    IfFailRet(hr);

    ILInstr* brFalse = rewriterWrapper.CreateInstr(CEE_BRFALSE_S);

    // Probe Ids

    COR_SIGNATURE stringData{ELEMENT_TYPE_STRING};
    TypeSignature stringType = {0, 1, &stringData};

    rewriterWrapper.LoadInt32(static_cast<INT32>(methodProbes.size()));
    hr = debuggerTokens->WriteRentArray(&rewriterWrapper, stringType, &beginCallInstruction);
    IfFailRet(hr);

    ILInstr* loadStrInstr;
    for (auto methodIndex = 0; methodIndex < static_cast<int>(methodProbes.size()); methodIndex++)
    {
        auto probeId = methodProbes[methodIndex]->probeId;

        rewriterWrapper.BeginLoadValueIntoArray(methodIndex);
        hr = LoadProbeIdIntoStack(module_id, module_metadata, function_token, probeId, rewriterWrapper, &loadStrInstr);
        IfFailRet(hr);
        rewriterWrapper.EndLoadValueIntoArray();
    }

    // probeMetadataIndices

    COR_SIGNATURE intData{ELEMENT_TYPE_I4};
    TypeSignature intType = {0, 1, &intData};

    rewriterWrapper.LoadInt32(static_cast<INT32>(methodProbes.size()));
    hr = debuggerTokens->WriteRentArray(&rewriterWrapper, intType, &beginCallInstruction);
    IfFailRet(hr);

    for (auto methodIndex = 0; methodIndex < static_cast<int>(methodProbes.size()); methodIndex++)
    {
        auto probeId = methodProbes[methodIndex]->probeId;

        int probeIndex;
        if (!ProbesMetadataTracker::Instance()->TryGetNextInstrumentedProbeIndex(probeId, module_id, function_token,
                                                                                 probeIndex))
        {
            Logger::Warn("*** DebuggerMethodRewriter::ApplyMethodProbe() TryGetNextInstrumentedProbeIndex failed with. "
                         "methodProbeId = ",
                         methodProbeId, " module_id= ", module_id, ", functon_token=", function_token);
            return E_FAIL;
        }

        rewriterWrapper.BeginLoadValueIntoArray(methodIndex);
        rewriterWrapper.LoadInt32(probeIndex);
        rewriterWrapper.CreateInstr(CEE_STELEM_I4);
    }

    // UpdateProbeInfo
    rewriterWrapper.LoadInt32(instrumentedMethodIndex);
    rewriterWrapper.LoadInt32(instrumentationVersion);
    rewriterWrapper.LoadToken(function_token);
    rewriterWrapper.LoadToken(caller->type.id);

    hr = debuggerTokens->WriteUpdateProbeInfo(&rewriterWrapper, &caller->type, & beginCallInstruction, probeType);
    IfFailRet(hr);

    ILInstr* loadInstanceInstr;
    hr = LoadInstanceIntoStack(caller, isStatic, rewriterWrapper, &loadInstanceInstr, debuggerTokens);
    IfFailRet(hr);

    brFalse->m_pTarget = loadInstanceInstr;
    
    rewriterWrapper.LoadInt32(instrumentedMethodIndex);

    if (isMultiProbe)
    {
        // Multiple method probes
        rewriterWrapper.LoadInt32(instrumentationVersion);
    }
    else
    {
        // Single method probe
        int probeIndex;
        if (!ProbesMetadataTracker::Instance()->TryGetNextInstrumentedProbeIndex(methodProbeId, module_id,
                                                                                 function_token, probeIndex))
        {
            Logger::Warn("*** DebuggerMethodRewriter::ApplyMethodProbe() TryGetNextInstrumentedProbeIndex failed with. "
                         "methodProbeId = ",
                         methodProbeId, " module_id= ", module_id, ", functon_token=", function_token);
            return E_FAIL;
        }
        rewriterWrapper.LoadInt32(probeIndex);
        ILInstr* loadStrInstr;
        hr = LoadProbeIdIntoStack(module_id, module_metadata, function_token, methodProbeId, rewriterWrapper, &loadStrInstr);
        IfFailRet(hr);
    }

    // *** Emit BeginMethod call
    hr = debuggerTokens->WriteBeginMethod_StartMarker(&rewriterWrapper, &caller->type, &beginCallInstruction, probeType);

    IfFailRet(hr);

    rewriterWrapper.StLocal(stateLocalIndex);

    // *** Emit LogArg call(s)
    hr = WriteCallsToLogArg(module_metadata, debuggerTokens, isStatic, methodArguments,
                            numArgs, rewriterWrapper,
                            stateLocalIndex, &beginCallInstruction, probeType);

    IfFailRet(hr);

    // Load the DebuggerState
    rewriterWrapper.LoadLocalAddress(stateLocalIndex);
    hr = debuggerTokens->WriteBeginMethod_EndMarker(&rewriterWrapper, &beginCallInstruction, probeType);

    IfFailRet(hr);

    for (const auto& branchInstr : branchTargets)
    {
        if (branchInstr->m_pTarget == nullptr)
        {
            branchInstr->m_pTarget = tryInstruction->m_pNext;
        }
    }

    ILInstr* pStateLeaveToBeginOriginalMethodInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    // *** BeginMethod call catch
    ILInstr* beginMethodCatchFirstInstr = rewriterWrapper.LoadLocalAddress(stateLocalIndex);
    debuggerTokens->WriteLogException(&rewriterWrapper, probeType);
    ILInstr* beginMethodCatchLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    EHClause beginMethodExClause = {};
    beginMethodExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    beginMethodExClause.m_pTryBegin = tryInstruction->m_pNext;
    beginMethodExClause.m_pTryEnd = beginMethodCatchFirstInstr;
    beginMethodExClause.m_pHandlerBegin = beginMethodCatchFirstInstr;
    beginMethodExClause.m_pHandlerEnd = beginMethodCatchLeaveInstr;
    beginMethodExClause.m_ClassToken = debuggerTokens->GetExceptionTypeRef();

    // ***
    // METHOD EXECUTION
    // ***
    ILInstr* beginOriginalMethodInstr = rewriterWrapper.GetCurrentILInstr();
    pStateLeaveToBeginOriginalMethodInstr->m_pTarget = beginOriginalMethodInstr;
    beginMethodCatchLeaveInstr->m_pTarget = beginOriginalMethodInstr;

    // ***
    // ENDING OF THE METHOD EXECUTION
    // ***

    // *** Create return instruction and insert it at the end
    ILInstr* methodReturnInstr = rewriter.NewILInstr();
    methodReturnInstr->m_opcode = CEE_RET;
    rewriter.InsertAfter(rewriter.GetILList()->m_pPrev, methodReturnInstr);
    rewriterWrapper.SetILPosition(methodReturnInstr);

    // ***
    // EXCEPTION CATCH
    // ***
    ILInstr* startExceptionCatch = rewriterWrapper.StLocal(exceptionIndex);
    rewriterWrapper.SetILPosition(methodReturnInstr);
    ILInstr* rethrowInstr = rewriterWrapper.Rethrow();

    // ***
    // EXCEPTION FINALLY / END METHOD PART
    // ***
    ILInstr* endMethodTryStartInstr;
    hr = LoadInstanceIntoStack(caller, isStatic, rewriterWrapper, &endMethodTryStartInstr, debuggerTokens);

    IfFailRet(hr);

    // *** Load the return value is is not void
    if (!isVoid)
    {
        rewriterWrapper.LoadLocal(returnValueIndex);
    }

    rewriterWrapper.LoadLocal(exceptionIndex);
    // Load the DebuggerState
    rewriterWrapper.LoadLocalAddress(stateLocalIndex);

    ILInstr* endMethodCallInstr;
    if (isVoid)
    {
        hr = debuggerTokens->WriteEndVoidReturnMemberRef(&rewriterWrapper, &caller->type, &endMethodCallInstr, probeType);
    }
    else
    {
        hr = debuggerTokens->WriteEndReturnMemberRef(&rewriterWrapper, &caller->type, &retFuncArg, &endMethodCallInstr,
                                                probeType);
    }

    IfFailRet(hr);

    rewriterWrapper.StLocal(callTargetReturnIndex);

    // *** Emit LogLocal call(s)
    hr = WriteCallsToLogLocal(module_metadata, debuggerTokens, isStatic, methodLocals,
                              numLocals, rewriterWrapper,
                              stateLocalIndex, &endMethodCallInstr, probeType);

    IfFailRet(hr);
    
    // *** Emit LogArg call(s)
    hr = WriteCallsToLogArg(module_metadata, debuggerTokens, isStatic, methodArguments,
                            numArgs, rewriterWrapper,
                            stateLocalIndex, &endMethodCallInstr, probeType);

    IfFailRet(hr);

    // Load the DebuggerState
    rewriterWrapper.LoadLocalAddress(stateLocalIndex);
    hr = debuggerTokens->WriteEndMethod_EndMarker(&rewriterWrapper, &endMethodCallInstr, probeType);

    IfFailRet(hr);

    if (!isVoid)
    {
        ILInstr* callTargetReturnGetReturnInstr;
        rewriterWrapper.LoadLocalAddress(callTargetReturnIndex);
        debuggerTokens->WriteCallTargetReturnGetReturnValue(&rewriterWrapper, callTargetReturnToken,
                                                            &callTargetReturnGetReturnInstr);
        rewriterWrapper.StLocal(returnValueIndex);
    }

    rewriterWrapper.LoadInt32(instrumentedMethodIndex);
    rewriterWrapper.LoadInt32(instrumentationVersion);
    rewriterWrapper.LoadLocalAddress(stateLocalIndex);

    hr = debuggerTokens->WriteDispose(&rewriterWrapper, &endMethodCallInstr, probeType);
    IfFailRet(hr);

    ILInstr* endMethodTryLeave = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    // *** EndMethod call catch

    // Load the DebuggerState
    ILInstr* endMethodCatchFirstInstr = rewriterWrapper.LoadLocalAddress(stateLocalIndex);
    debuggerTokens->WriteLogException(&rewriterWrapper, probeType);
    ILInstr* endMethodCatchLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    EHClause endMethodExClause = {};
    endMethodExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    endMethodExClause.m_pTryBegin = endMethodTryStartInstr;
    endMethodExClause.m_pTryEnd = endMethodCatchFirstInstr;
    endMethodExClause.m_pHandlerBegin = endMethodCatchFirstInstr;
    endMethodExClause.m_pHandlerEnd = endMethodCatchLeaveInstr;
    endMethodExClause.m_ClassToken = debuggerTokens->GetExceptionTypeRef();

    // *** EndMethod leave to finally
    ILInstr* endFinallyInstr = rewriterWrapper.EndFinally();
    endMethodTryLeave->m_pTarget = endFinallyInstr;
    endMethodCatchLeaveInstr->m_pTarget = endFinallyInstr;

    // ***
    // METHOD RETURN
    // ***

    // Load the current return value from the local var
    if (!isVoid)
    {
        rewriterWrapper.LoadLocal(returnValueIndex);
    }

    // Changes all returns to a LEAVE.S
    for (ILInstr* pInstr = rewriter.GetILList()->m_pNext; pInstr != rewriter.GetILList(); pInstr = pInstr->m_pNext)
    {
        if (pInstr->m_opcode == CEE_RET && pInstr != methodReturnInstr)
        {
            if (isVoid)
            {
                pInstr->m_opcode = CEE_LEAVE_S;
                pInstr->m_pTarget = endFinallyInstr->m_pNext;
            }
            else
            {
                pInstr->m_opcode = CEE_STLOC;
                pInstr->m_Arg16 = static_cast<INT16>(returnValueIndex);
                if (pInstr->m_Arg16 < 0)
                {
                    // We check if the conversion returned negative numbers.
                    Logger::Error("The local variable index for the return value ('returnValueIndex') cannot be lower "
                                  "than zero.");
                    return S_FALSE;
                }

                ILInstr* leaveInstr = rewriter.NewILInstr();
                leaveInstr->m_opcode = CEE_LEAVE_S;
                leaveInstr->m_pTarget = endFinallyInstr->m_pNext;
                rewriter.InsertAfter(pInstr, leaveInstr);
            }
        }
    }

    EHClause exClause = {};
    exClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    exClause.m_pTryBegin = tryInstruction->m_pNext;
    exClause.m_pTryEnd = startExceptionCatch;
    exClause.m_pHandlerBegin = startExceptionCatch;
    exClause.m_pHandlerEnd = rethrowInstr;
    exClause.m_ClassToken = debuggerTokens->GetExceptionTypeRef();

    EHClause finallyClause = {};
    finallyClause.m_Flags = COR_ILEXCEPTION_CLAUSE_FINALLY;
    finallyClause.m_pTryBegin = tryInstruction->m_pNext;
    finallyClause.m_pTryEnd = rethrowInstr->m_pNext;
    finallyClause.m_pHandlerBegin = rethrowInstr->m_pNext;
    finallyClause.m_pHandlerEnd = endFinallyInstr;

    newClauses.push_back(beginMethodExClause);
    newClauses.push_back(endMethodExClause);
    newClauses.push_back(exClause);
    newClauses.push_back(finallyClause);

    return S_OK;
}

HRESULT DebuggerMethodRewriter::ApplyMethodSpanProbe(
    ModuleID module_id, ModuleMetadata& module_metadata, FunctionInfo* caller, DebuggerTokens* debuggerTokens,
    mdToken function_token, TypeSignature retFuncArg, bool isVoid, bool isStatic,
    const std::vector<TypeSignature>& methodArguments, int numArgs,
    const std::shared_ptr<SpanProbeOnMethodDefinition>& spanProbe,
    ILRewriter& rewriter, const std::vector<TypeSignature>& methodLocals, int numLocals,
    ILRewriterWrapper& rewriterWrapper, ULONG spanMethodStateIndex, ULONG exceptionIndex, ULONG callTargetReturnIndex,
    ULONG returnValueIndex, mdToken callTargetReturnToken, const int instrumentedMethodIndex,
    ILInstr*& beforeLineProbe, std::vector<EHClause>& newClauses) const
{
    const auto& spanProbeId = spanProbe->probeId;

    LogDebugCallerInfo(caller, instrumentedMethodIndex);

    rewriterWrapper.SetILPosition(beforeLineProbe);

    const auto tryInstruction = beforeLineProbe->m_pPrev;

    // ***
    // BEGIN SPAN PART
    // ***

    // Define ResourceName as string
    WSTRING resourceName =
        spanProbe->target_method.type.name.substr(spanProbe->target_method.type.name.find_last_of(WStr('.')) + 1) +
        WStr(".") + spanProbe->target_method.method_name;

    mdString resourceNameIdToken;
    auto hr = module_metadata.metadata_emit->DefineUserString(resourceName.data(), static_cast<ULONG>(resourceName.size()),
                                                        &resourceNameIdToken);

    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() DefineUserString of ResourceName is Failed. Aborting "
                     "an async instrumentation. module id:",
                     module_id, " method: ", caller->type.name, ".", caller->name);
        return E_FAIL;
    }

    // Define OperationName as string
    mdString operationNameIdToken;
    hr = module_metadata.metadata_emit->DefineUserString(dynamic_span_operation_name.data(),
                                                         static_cast<ULONG>(dynamic_span_operation_name.size()),
                                                        &operationNameIdToken);

    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() DefineUserString of OperationName is Failed. Aborting "
                     "an async instrumentation. module id:",
                     module_id, " method: ", caller->type.name, ".", caller->name);
        return E_FAIL;
    }

    ILInstr* loadStrInstr;
    hr = LoadProbeIdIntoStack(module_id, module_metadata, function_token, spanProbeId, rewriterWrapper, &loadStrInstr);
    IfFailRet(hr);

    beforeLineProbe = rewriterWrapper.GetCurrentILInstr()->m_pPrev;

    rewriterWrapper.LoadStr(resourceNameIdToken);
    rewriterWrapper.LoadStr(operationNameIdToken);

    ILInstr* beginCallInstruction;
    hr = debuggerTokens->WriteBeginSpan(&rewriterWrapper, &caller->type, &beginCallInstruction, /* isAsyncMethod */ false);

    IfFailRet(hr);

    rewriterWrapper.StLocal(spanMethodStateIndex);

    ILInstr* pStateLeaveToBeginOriginalMethodInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    // *** BeginMethod call catch
    ILInstr* beginMethodCatchFirstInstr = rewriterWrapper.LoadLocalAddress(spanMethodStateIndex);
    debuggerTokens->WriteLogException(&rewriterWrapper, NonAsyncMethodSpanProbe);
    ILInstr* beginMethodCatchLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    EHClause beginMethodExClause = {};
    beginMethodExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    beginMethodExClause.m_pTryBegin = tryInstruction->m_pNext;
    beginMethodExClause.m_pTryEnd = beginMethodCatchFirstInstr;
    beginMethodExClause.m_pHandlerBegin = beginMethodCatchFirstInstr;
    beginMethodExClause.m_pHandlerEnd = beginMethodCatchLeaveInstr;
    beginMethodExClause.m_ClassToken = debuggerTokens->GetExceptionTypeRef();

    // ***
    // METHOD EXECUTION
    // ***
    ILInstr* beginOriginalMethodInstr = rewriterWrapper.GetCurrentILInstr();
    pStateLeaveToBeginOriginalMethodInstr->m_pTarget = beginOriginalMethodInstr;
    beginMethodCatchLeaveInstr->m_pTarget = beginOriginalMethodInstr;

    // ***
    // ENDING OF THE METHOD EXECUTION
    // ***

    // *** Create return instruction and insert it at the end
    ILInstr* methodReturnInstr = rewriter.NewILInstr();
    methodReturnInstr->m_opcode = CEE_RET;
    rewriter.InsertAfter(rewriter.GetILList()->m_pPrev, methodReturnInstr);
    rewriterWrapper.SetILPosition(methodReturnInstr);

    // ***
    // EXCEPTION CATCH
    // ***
    ILInstr* startExceptionCatch = rewriterWrapper.StLocal(exceptionIndex);
    rewriterWrapper.SetILPosition(methodReturnInstr);
    ILInstr* rethrowInstr = rewriterWrapper.Rethrow();

    // ***
    // EXCEPTION FINALLY / END METHOD PART
    // ***
    
    IfFailRet(hr);

    ILInstr* endMethodCallInstr;
    auto endMethodTryStartInstr = rewriterWrapper.LoadLocal(exceptionIndex);
    rewriterWrapper.LoadLocalAddress(spanMethodStateIndex);
    hr = debuggerTokens->WriteEndSpan(&rewriterWrapper, &endMethodCallInstr, /* isAsyncMethod */ false);

    IfFailRet(hr);

    ILInstr* endMethodTryLeave = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    // *** EndMethod call catch

    // Load the DebuggerState
    ILInstr* endMethodCatchFirstInstr = rewriterWrapper.LoadLocalAddress(spanMethodStateIndex);
    debuggerTokens->WriteLogException(&rewriterWrapper, NonAsyncMethodSpanProbe);
    ILInstr* endMethodCatchLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    EHClause endMethodExClause = {};
    endMethodExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    endMethodExClause.m_pTryBegin = endMethodTryStartInstr;
    endMethodExClause.m_pTryEnd = endMethodCatchFirstInstr;
    endMethodExClause.m_pHandlerBegin = endMethodCatchFirstInstr;
    endMethodExClause.m_pHandlerEnd = endMethodCatchLeaveInstr;
    endMethodExClause.m_ClassToken = debuggerTokens->GetExceptionTypeRef();

    // *** EndMethod leave to finally
    ILInstr* endFinallyInstr = rewriterWrapper.EndFinally();
    endMethodTryLeave->m_pTarget = endFinallyInstr;
    endMethodCatchLeaveInstr->m_pTarget = endFinallyInstr;

    // ***
    // METHOD RETURN
    // ***

    // Load the current return value from the local var
    if (!isVoid)
    {
        rewriterWrapper.LoadLocal(returnValueIndex);
    }

    // Changes all returns to a LEAVE.S
    for (ILInstr* pInstr = rewriter.GetILList()->m_pNext; pInstr != rewriter.GetILList(); pInstr = pInstr->m_pNext)
    {
        if (pInstr->m_opcode == CEE_RET && pInstr != methodReturnInstr)
        {
            if (isVoid)
            {
                pInstr->m_opcode = CEE_LEAVE_S;
                pInstr->m_pTarget = endFinallyInstr->m_pNext;
            }
            else
            {
                pInstr->m_opcode = CEE_STLOC;
                pInstr->m_Arg16 = static_cast<INT16>(returnValueIndex);
                if (pInstr->m_Arg16 < 0)
                {
                    // We check if the conversion returned negative numbers.
                    Logger::Error("The local variable index for the return value ('returnValueIndex') cannot be lower "
                                  "than zero.");
                    return S_FALSE;
                }

                ILInstr* leaveInstr = rewriter.NewILInstr();
                leaveInstr->m_opcode = CEE_LEAVE_S;
                leaveInstr->m_pTarget = endFinallyInstr->m_pNext;
                rewriter.InsertAfter(pInstr, leaveInstr);
            }
        }
    }

    EHClause exClause = {};
    exClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    exClause.m_pTryBegin = tryInstruction->m_pNext;
    exClause.m_pTryEnd = startExceptionCatch;
    exClause.m_pHandlerBegin = startExceptionCatch;
    exClause.m_pHandlerEnd = rethrowInstr;
    exClause.m_ClassToken = debuggerTokens->GetExceptionTypeRef();

    EHClause finallyClause = {};
    finallyClause.m_Flags = COR_ILEXCEPTION_CLAUSE_FINALLY;
    finallyClause.m_pTryBegin = tryInstruction->m_pNext;
    finallyClause.m_pTryEnd = rethrowInstr->m_pNext;
    finallyClause.m_pHandlerBegin = rethrowInstr->m_pNext;
    finallyClause.m_pHandlerEnd = endFinallyInstr;

    newClauses.push_back(beginMethodExClause);
    newClauses.push_back(endMethodExClause);
    newClauses.push_back(exClause);
    newClauses.push_back(finallyClause);

    return S_OK;
}

HRESULT DebuggerMethodRewriter::EndAsyncMethodProbe(ILRewriterWrapper& rewriterWrapper,
                                                    ModuleMetadata& module_metadata,
                                                    DebuggerTokens* debuggerTokens, FunctionInfo* caller, bool isStatic,
                                                    TypeSignature* methodReturnType,
                                                    const std::vector<TypeSignature>& methodLocals, int numLocals,
                                                    ULONG callTargetReturnIndex,
                                                    mdFieldDef isReEntryFieldTok, 
                                                    std::vector<EHClause>& newClauses,
                                                    const ProbeType& probeType) const
{
    ILInstr* setResultEndMethodTryStartInstr = nullptr;
    ILInstr* endMethodOriginalCodeFirstInstr = nullptr;

    int numberOfCallsFounded = 0;
    auto lastEh = &rewriterWrapper.GetILRewriter()->GetEHPointer()[rewriterWrapper.GetILRewriter()->GetEHCount() - 1];
    ILInstr* setExceptionReturnInstruction = nullptr; // Used by SetException to determine what is the index of the return value
    // search call to SetResult and SetException
    for (ILInstr* pInstr = rewriterWrapper.GetILRewriter()->GetILList()->m_pPrev;
         numberOfCallsFounded < 2 && pInstr != rewriterWrapper.GetILRewriter()->GetILList(); pInstr = pInstr->m_pPrev)
    {
        // It is a call to a known struct method so CALL instruction but pay attention to change it if the runtime changes
        if (pInstr->m_opcode != CEE_CALL)
        {
            continue;
        }

        auto functionInfo = GetFunctionInfo(module_metadata.metadata_import, pInstr->m_Arg32);
        if (functionInfo.name != WStr("SetResult") && functionInfo.name != WStr("SetException"))
        {
            continue;
        }

        HRESULT hr;
        ILInstr* endMethodTryStartInstr = nullptr;
        ILInstr* endMethodCallInstr;
        auto [elementType, returnTypeFlags] = methodReturnType->GetElementTypeAndFlags();
        if (functionInfo.name == WStr("SetResult"))
        {
            rewriterWrapper.SetILPosition(lastEh->m_pHandlerEnd->m_pNext);

            if (elementType == ELEMENT_TYPE_VOID)
            {
                hr = LoadInstanceIntoStack(caller, isStatic, rewriterWrapper, &endMethodTryStartInstr, debuggerTokens);
                rewriterWrapper.LoadNull(); // exception
                rewriterWrapper.LoadArgument(0);
                rewriterWrapper.LoadFieldAddress(isReEntryFieldTok);
                /*debuggerTokens->WriteEndReturnMemberRef(&rewriterWrapper, &caller->type,methodReturnType,
                    &endMethodCallInstr, AsyncMethod);*/
                debuggerTokens->WriteEndVoidReturnMemberRef(&rewriterWrapper, &caller->type, &endMethodCallInstr, probeType);
            }
            else
            {
                LoadInstanceIntoStack(caller, isStatic, rewriterWrapper, &endMethodTryStartInstr, debuggerTokens);
                // create the instruction that load the return value
                ILInstr* returnInstruction = rewriterWrapper.GetILRewriter()->NewILInstr();
                memcpy(returnInstruction, pInstr->m_pPrev, sizeof(*returnInstruction));

                // Used by SetException to determine what is the index that should be loaded in EndMethod_StartMarker callback
                setExceptionReturnInstruction = rewriterWrapper.GetILRewriter()->NewILInstr();
                memcpy(setExceptionReturnInstruction, pInstr->m_pPrev, sizeof(*setExceptionReturnInstruction));

                rewriterWrapper.GetILRewriter()->InsertBefore(rewriterWrapper.GetCurrentILInstr(), returnInstruction);
                rewriterWrapper.LoadNull(); // exception
                rewriterWrapper.LoadArgument(0);
                rewriterWrapper.LoadFieldAddress(isReEntryFieldTok);
                debuggerTokens->WriteEndReturnMemberRef(&rewriterWrapper, &caller->type, methodReturnType, &endMethodCallInstr, probeType);
            }

            setResultEndMethodTryStartInstr = endMethodTryStartInstr;
            endMethodOriginalCodeFirstInstr = rewriterWrapper.GetCurrentILInstr();
        }
        else if (functionInfo.name == WStr("SetException"))
        {
            rewriterWrapper.SetILPosition(lastEh->m_pHandlerBegin->m_pNext);
            LoadInstanceIntoStack(caller, isStatic, rewriterWrapper, &endMethodTryStartInstr, debuggerTokens);
            if (elementType != ELEMENT_TYPE_VOID)
            {
                if (setExceptionReturnInstruction == nullptr)
                {
                    Logger::Error("The return instruction is not initialized by the SetResult logic. elementType: " , elementType,
                                  " Method is: ", caller->type.name, ".", caller->name);
                    return E_FAIL;
                }

                rewriterWrapper.GetILRewriter()->InsertBefore(rewriterWrapper.GetCurrentILInstr(), setExceptionReturnInstruction);
            }

            // create the instruction that load the exception value
            ILInstr* exceptionInstruction = rewriterWrapper.GetILRewriter()->NewILInstr();
            memcpy(exceptionInstruction, pInstr->m_pPrev, sizeof(*exceptionInstruction));
            rewriterWrapper.GetILRewriter()->InsertBefore(rewriterWrapper.GetCurrentILInstr(), exceptionInstruction);
            rewriterWrapper.LoadArgument(0);
            rewriterWrapper.LoadFieldAddress(isReEntryFieldTok);
            if (elementType != ELEMENT_TYPE_VOID)
            {
                debuggerTokens->WriteEndReturnMemberRef(&rewriterWrapper, &caller->type, methodReturnType, &endMethodCallInstr, probeType);
            }
            else
            {
                debuggerTokens->WriteEndVoidReturnMemberRef(&rewriterWrapper, &caller->type, &endMethodCallInstr, probeType);
            }
        }

        // store the return value
        rewriterWrapper.StLocal(callTargetReturnIndex);

        // call LogLocal
        hr = WriteCallsToLogLocal(module_metadata, debuggerTokens, isStatic, methodLocals,
                                  numLocals, rewriterWrapper,
                                  /* callTargetStateIndex */ 0, &endMethodCallInstr, probeType, isReEntryFieldTok);
        IfFailRet(hr);

        // load the state and call EndMethod_EndMarker
        rewriterWrapper.LoadArgument(0);
        rewriterWrapper.LoadFieldAddress(isReEntryFieldTok);
        hr = debuggerTokens->WriteEndMethod_EndMarker(&rewriterWrapper, &endMethodCallInstr, probeType);
        IfFailRet(hr);
        ILInstr* endMethodTryLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

        // call LogException
        ILInstr* endMethodCatchFirstInstr = rewriterWrapper.LoadArgument(0);
        rewriterWrapper.LoadFieldAddress(isReEntryFieldTok);
        debuggerTokens->WriteLogException(&rewriterWrapper, probeType);
        ILInstr* endMethodCatchLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

        // target the leave instructions of the try and catch to the first corresponding instruction of the original code
        ILInstr* originalCodeFirstInstr = rewriterWrapper.GetCurrentILInstr();
        endMethodCatchLeaveInstr->m_pTarget = originalCodeFirstInstr;
        endMethodTryLeaveInstr->m_pTarget = originalCodeFirstInstr;
        EHClause endMethodExClause = {};
        endMethodExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
        endMethodExClause.m_pTryBegin = endMethodTryStartInstr;
        endMethodExClause.m_pTryEnd = endMethodCatchFirstInstr;
        endMethodExClause.m_pHandlerBegin = endMethodCatchFirstInstr;
        endMethodExClause.m_pHandlerEnd = endMethodCatchLeaveInstr;
        endMethodExClause.m_ClassToken = debuggerTokens->GetExceptionTypeRef();

        newClauses.push_back(endMethodExClause);
        numberOfCallsFounded++;
    }

    if (numberOfCallsFounded < 1)
    {
        // MoveNext can contains only SetException so `numberOfCallsFounded` will be 1
        return E_FAIL;
    }

    if (setResultEndMethodTryStartInstr == nullptr || endMethodOriginalCodeFirstInstr == nullptr)
    {
        return S_OK;
    }

    // Changes all LEAVE's to the original end method to the try end method
    for (ILInstr* pInstr = rewriterWrapper.GetILRewriter()->GetILList()->m_pNext;
         pInstr != setResultEndMethodTryStartInstr;
         pInstr = pInstr->m_pNext)
    {
        switch (pInstr->m_opcode)
        {
            case CEE_LEAVE:
            case CEE_LEAVE_S:
            {
                if (pInstr->m_pTarget == endMethodOriginalCodeFirstInstr)
                {
                    pInstr->m_pTarget = setResultEndMethodTryStartInstr;
                }
                break;
            }
            default:
                break;
        }
    }

    return S_OK;
}

HRESULT DebuggerMethodRewriter::EndAsyncMethodSpanProbe(ILRewriterWrapper& rewriterWrapper, ModuleMetadata& module_metadata,
                                                    DebuggerTokens* debuggerTokens, FunctionInfo* caller, bool isStatic,
                                                    TypeSignature* methodReturnType,
                                                    const std::vector<TypeSignature>& methodLocals, int numLocals,
                                                    ULONG callTargetReturnIndex, mdFieldDef isReEntryFieldTok,
                                                    std::vector<EHClause>& newClauses) const
{
    ILInstr* setResultEndMethodTryStartInstr = nullptr;
    ILInstr* endMethodOriginalCodeFirstInstr = nullptr;

    int numberOfCallsFounded = 0;
    auto lastEh = &rewriterWrapper.GetILRewriter()->GetEHPointer()[rewriterWrapper.GetILRewriter()->GetEHCount() - 1];
    ILInstr* setExceptionReturnInstruction =
        nullptr; // Used by SetException to determine what is the index of the return value
    // search call to SetResult and SetException
    for (ILInstr* pInstr = rewriterWrapper.GetILRewriter()->GetILList()->m_pPrev;
         numberOfCallsFounded < 2 && pInstr != rewriterWrapper.GetILRewriter()->GetILList(); pInstr = pInstr->m_pPrev)
    {
        // It is a call to a known struct method so CALL instruction but pay attention to change it if the runtime
        // changes
        if (pInstr->m_opcode != CEE_CALL)
        {
            continue;
        }

        auto functionInfo = GetFunctionInfo(module_metadata.metadata_import, pInstr->m_Arg32);
        if (functionInfo.name != WStr("SetResult") && functionInfo.name != WStr("SetException"))
        {
            continue;
        }

        ILInstr* endMethodTryStartInstr = nullptr;
        ILInstr* endMethodCallInstr;
        auto [elementType, returnTypeFlags] = methodReturnType->GetElementTypeAndFlags();
        if (functionInfo.name == WStr("SetResult"))
        {
            rewriterWrapper.SetILPosition(lastEh->m_pHandlerEnd->m_pNext);
            endMethodTryStartInstr = rewriterWrapper.LoadNull();
            rewriterWrapper.LoadArgument(0);
            rewriterWrapper.LoadFieldAddress(isReEntryFieldTok);
            debuggerTokens->WriteEndSpan(&rewriterWrapper, &endMethodCallInstr, /* isAsyncMethod */ true);
            setResultEndMethodTryStartInstr = endMethodTryStartInstr;
            endMethodOriginalCodeFirstInstr = rewriterWrapper.GetCurrentILInstr();
        }
        else if (functionInfo.name == WStr("SetException"))
        {
            rewriterWrapper.SetILPosition(lastEh->m_pHandlerBegin->m_pNext);
            // create the instruction that load the exception value
            ILInstr* exceptionInstruction = rewriterWrapper.GetILRewriter()->NewILInstr();
            memcpy(exceptionInstruction, pInstr->m_pPrev, sizeof(*exceptionInstruction));
            rewriterWrapper.GetILRewriter()->InsertBefore(rewriterWrapper.GetCurrentILInstr(), exceptionInstruction);
            rewriterWrapper.LoadArgument(0);
            rewriterWrapper.LoadFieldAddress(isReEntryFieldTok);
            debuggerTokens->WriteEndSpan(&rewriterWrapper, &endMethodCallInstr, /* isAsyncMethod */ true);
            endMethodTryStartInstr = exceptionInstruction;
        }

        ILInstr* endMethodTryLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

        // call LogException
        ILInstr* endMethodCatchFirstInstr = rewriterWrapper.LoadArgument(0);
        rewriterWrapper.LoadFieldAddress(isReEntryFieldTok);
        debuggerTokens->WriteLogException(&rewriterWrapper, AsyncMethodSpanProbe);
        ILInstr* endMethodCatchLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

        // target the leave instructions of the try and catch to the first corresponding instruction of the original
        // code
        ILInstr* originalCodeFirstInstr = rewriterWrapper.GetCurrentILInstr();
        endMethodCatchLeaveInstr->m_pTarget = originalCodeFirstInstr;
        endMethodTryLeaveInstr->m_pTarget = originalCodeFirstInstr;
        EHClause endMethodExClause = {};
        endMethodExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
        endMethodExClause.m_pTryBegin = endMethodTryStartInstr;
        endMethodExClause.m_pTryEnd = endMethodCatchFirstInstr;
        endMethodExClause.m_pHandlerBegin = endMethodCatchFirstInstr;
        endMethodExClause.m_pHandlerEnd = endMethodCatchLeaveInstr;
        endMethodExClause.m_ClassToken = debuggerTokens->GetExceptionTypeRef();

        newClauses.push_back(endMethodExClause);
        numberOfCallsFounded++;
    }

    if (numberOfCallsFounded < 1)
    {
        // MoveNext can contains only SetException so `numberOfCallsFounded` will be 1
        return E_FAIL;
    }

    if (setResultEndMethodTryStartInstr == nullptr || endMethodOriginalCodeFirstInstr == nullptr)
    {
        return S_OK;
    }

    // Changes all LEAVE's to the original end method to the try end method
    for (ILInstr* pInstr = rewriterWrapper.GetILRewriter()->GetILList()->m_pNext;
         pInstr != setResultEndMethodTryStartInstr; pInstr = pInstr->m_pNext)
    {
        switch (pInstr->m_opcode)
        {
            case CEE_LEAVE:
            case CEE_LEAVE_S:
            {
                if (pInstr->m_pTarget == endMethodOriginalCodeFirstInstr)
                {
                    pInstr->m_pTarget = setResultEndMethodTryStartInstr;
                }
                break;
            }
            default:
                break;
        }
    }

    return S_OK;
}

HRESULT DebuggerMethodRewriter::LoadProbeIdIntoStack(const ModuleID moduleId, const ModuleMetadata& moduleMetadata,
                                                     const mdToken functionToken, const shared::WSTRING& methodProbeId,
                                                     const ILRewriterWrapper& rewriterWrapper,
                                                     ILInstr** outLoadStrInstr)
{
    // Define ProbeId as string
    mdString methodProbeIdToken;
    const auto hr = moduleMetadata.metadata_emit->DefineUserString(
        methodProbeId.data(), static_cast<ULONG>(methodProbeId.length()), &methodProbeIdToken);

    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::ApplyAsyncMethodProbe() DefineUserStringFailed. MethodProbeId = ",
                     methodProbeId, " module_id= ", moduleId, ", function_token=", functionToken);
        return hr;
    }

    *outLoadStrInstr = rewriterWrapper.LoadStr(methodProbeIdToken);
    return hr;
}

void DebuggerMethodRewriter::LogDebugCallerInfo(const FunctionInfo* caller, const int instrumentedMethodIndex) 
{
    if (!IsDebugEnabled()) return;

    Logger::Debug("Caller InstrumentedMethodInfo: ", instrumentedMethodIndex);
    Logger::Debug("Caller Type.Id: ", HexStr(&caller->type.id, sizeof(mdToken)));
    Logger::Debug("Caller Type.IsGeneric: ", caller->type.isGeneric);
    Logger::Debug("Caller Type.IsValid: ", caller->type.IsValid());
    Logger::Debug("Caller Type.Name: ", caller->type.name);
    Logger::Debug("Caller Type.TokenType: ", caller->type.token_type);
    Logger::Debug("Caller Type.Spec: ", HexStr(&caller->type.type_spec, sizeof(mdTypeSpec)));
    Logger::Debug("Caller Type.ValueType: ", caller->type.valueType);

    if (caller->type.extend_from != nullptr)
    {
        Logger::Debug("Caller Type Extend From.Id: ", HexStr(&caller->type.extend_from->id, sizeof(mdToken)));
        Logger::Debug("Caller Type Extend From.IsGeneric: ", caller->type.extend_from->isGeneric);
        Logger::Debug("Caller Type Extend From.IsValid: ", caller->type.extend_from->IsValid());
        Logger::Debug("Caller Type Extend From.Name: ", caller->type.extend_from->name);
        Logger::Debug("Caller Type Extend From.TokenType: ", caller->type.extend_from->token_type);
        Logger::Debug("Caller Type Extend From.Spec: ",
                      HexStr(&caller->type.extend_from->type_spec, sizeof(mdTypeSpec)));
        Logger::Debug("Caller Type Extend From.ValueType: ", caller->type.extend_from->valueType);
    }

    if (caller->type.parent_type != nullptr)
    {
        Logger::Debug("Caller ParentType.Id: ", HexStr(&caller->type.parent_type->id, sizeof(mdToken)));
        Logger::Debug("Caller ParentType.IsGeneric: ", caller->type.parent_type->isGeneric);
        Logger::Debug("Caller ParentType.IsValid: ", caller->type.parent_type->IsValid());
        Logger::Debug("Caller ParentType.Name: ", caller->type.parent_type->name);
        Logger::Debug("Caller ParentType.TokenType: ", caller->type.parent_type->token_type);
        Logger::Debug("Caller ParentType.Spec: ", HexStr(&caller->type.parent_type->type_spec, sizeof(mdTypeSpec)));
        Logger::Debug("Caller ParentType.ValueType: ", caller->type.parent_type->valueType);
    }
}

HRESULT DebuggerMethodRewriter::ApplyAsyncMethodProbe(
    MethodProbeDefinitions& methodProbes, ModuleID module_id,
    ModuleMetadata& module_metadata, FunctionInfo* caller,
    DebuggerTokens* debugger_tokens, mdToken function_token, bool isStatic, TypeSignature* methodReturnType,
    const std::vector<TypeSignature>& methodLocals, int numLocals, ILRewriterWrapper& rewriterWrapper,
    ULONG callTargetReturnIndex, ULONG returnValueIndex,
    mdToken callTargetReturnToken, ILInstr* firstInstruction, const int instrumentedMethodIndex,
    ILInstr* const& beforeLineProbe, std::vector<EHClause>& newClauses) const
{
    /*
     * void MoveNext()
     * {
     *      try
     *      {
     *          AsyncMethodDebuggerInvoker.BeginMethod<StateMachineType>(probeId, instance, methodHandle, typeHandle, methodMetadataIndex, ref isReEntryToMoveNext)
     *      }
     *      catch (Exception e)
     *      {
     *          AsyncMethodDebuggerInvoker.LogException(e, ref _asyncState);
     *      }
     *      try
     *      {
     *          Method execution
     *      }
     *      catch (Exception ex)
     *      {
     *          try
     *          {
     *              DebuggerReturn return = AsyncMethodDebuggerInvoker.EndMethod_StartMarker<StateMachineType>(instance, ex, ref asyncState);
     *              AsyncMethodDebuggerInvoker.LogLocal() * N
     *              AsyncMethodDebuggerInvoker.EndMethod_EndMarker(ref _asyncState);
     *          }
     *          catch(Exception e)
     *          {
     *              AsyncMethodDebuggerInvoker.LogException(e, ref _asyncState);
     *          }
     *          ...
     *          taskBuilder.SetException(ex);
     *      }
     *      try
     *      {
     *          // depends on the actual task return type this can be:
     *          DebuggerReturn<ReturnType> return = AsyncMethodDebuggerInvoker.EndMethod_StartMarker<StateMachineType, ReturnType>(instance, result, null, ref asyncState);
     *          // or:
     *          DebuggerReturn return = AsyncMethodDebuggerInvoker.EndMethod_StartMarker<StateMachineType>(instance, ex, ref asyncState);
     *          AsyncMethodDebuggerInvoker.LogLocal() * N
     *          AsyncMethodDebuggerInvoker.EndMethod_EndMarker(ref _asyncState);
     *      }
     *      catch (Exception e)
     *      {
     *          AsyncMethodDebuggerInvoker.LogException(e, ref _asyncState);
     *      }
     *      ...
     *      taskBuilder.SetResult(result);
     *  }
     */

    const auto& methodProbeId = methodProbes[0]->probeId;

    mdFieldDef isReEntryFieldTok;
    HRESULT hr = debugger_tokens->GetIsFirstEntryToMoveNextFieldToken(caller->type.id, isReEntryFieldTok);
    IfFailRet(hr);

    if (isReEntryFieldTok == mdFieldDefNil)
    {
        Logger::Info("isReEntryField token is nil. Aborting an async instrumentation. module id:", module_id,
                     " method: ", caller->type.name, ".", caller->name);
        return E_FAIL;
    }

    if (hr != S_OK)
    {
        Logger::Info("Failed to apply Method Probe on Async Method due to failure in the lookup of the isReEntry field in the state machine. module id:", module_id, " method: ", caller->type.name, ".", caller->name);
        return S_OK; // We do not fail the whole instrumentation as there could be Line Probes that we want to emit. They do not suffer from the absence of the IsReEntry field.
    }

    LogDebugCallerInfo(caller, instrumentedMethodIndex);
    Logger::Info("Applying async method probe. module id:", module_id, " method: ", caller->type.name, ".", caller->name);

    rewriterWrapper.SetILPosition(beforeLineProbe);

    const auto& branchTargets = GetBranchTargets(rewriterWrapper.GetILRewriter());

    for (const auto& branchInstr : branchTargets)
    {
        if (branchInstr->m_pTarget == beforeLineProbe)
        {
            branchInstr->m_pTarget = nullptr;
        }
    }

    const auto tryInstruction = beforeLineProbe->m_pPrev;

    const auto instrumentationVersion = ProbesMetadataTracker::Instance()->GetNextInstrumentationVersion();

    const auto probeType = AsyncMethodProbe;
    // ***
    // BEGIN METHOD PART
    // ***

    ILInstr* beginCallInstruction;

    rewriterWrapper.LoadInt32(instrumentedMethodIndex);
    rewriterWrapper.LoadInt32(instrumentationVersion);
    rewriterWrapper.LoadArgument(0);
    rewriterWrapper.LoadFieldAddress(isReEntryFieldTok);
    
    hr = debugger_tokens->WriteShouldUpdateProbeInfo(&rewriterWrapper, &beginCallInstruction, probeType);
    IfFailRet(hr);

    ILInstr* brFalse = rewriterWrapper.CreateInstr(CEE_BRFALSE_S);

    // Probe Ids

    COR_SIGNATURE stringData{ELEMENT_TYPE_STRING};
    TypeSignature stringType = {0, 1, &stringData};

    rewriterWrapper.LoadInt32(static_cast<INT32>(methodProbes.size()));
    hr = debugger_tokens->WriteRentArray(&rewriterWrapper, stringType, &beginCallInstruction);
    IfFailRet(hr);

    for (auto methodIndex = 0; methodIndex < static_cast<int>(methodProbes.size()); methodIndex++)
    {
        auto probeId = methodProbes[methodIndex]->probeId;

        rewriterWrapper.BeginLoadValueIntoArray(methodIndex);
        ILInstr* loadStrInstr;
        hr = LoadProbeIdIntoStack(module_id, module_metadata, function_token, probeId, rewriterWrapper, &loadStrInstr);
        IfFailRet(hr);
        rewriterWrapper.EndLoadValueIntoArray();
    }

    // probeMetadataIndices

    COR_SIGNATURE intData{ELEMENT_TYPE_I4};
    TypeSignature intType = {0, 1, &intData};

    rewriterWrapper.LoadInt32(static_cast<INT32>(methodProbes.size()));
    hr = debugger_tokens->WriteRentArray(&rewriterWrapper, intType, &beginCallInstruction);
    IfFailRet(hr);

    for (auto methodIndex = 0; methodIndex < static_cast<int>(methodProbes.size()); methodIndex++)
    {
        auto probeId = methodProbes[methodIndex]->probeId;

        int probeIndex;
        if (!ProbesMetadataTracker::Instance()->TryGetNextInstrumentedProbeIndex(probeId, module_id, function_token,
                                                                                 probeIndex))
        {
            Logger::Warn("*** DebuggerMethodRewriter::ApplyMethodProbe() TryGetNextInstrumentedProbeIndex failed with. "
                         "methodProbeId = ",
                         methodProbeId, " module_id= ", module_id, ", functon_token=", function_token);
            return E_FAIL;
        }

        rewriterWrapper.BeginLoadValueIntoArray(methodIndex);
        rewriterWrapper.LoadInt32(probeIndex);
        rewriterWrapper.CreateInstr(CEE_STELEM_I4);
    }

    // UpdateProbeInfo
    ILInstr* loadInstanceInstr;
    hr = LoadInstanceIntoStack(caller, isStatic, rewriterWrapper, &loadInstanceInstr, debugger_tokens);
    IfFailRet(hr);

    if (loadInstanceInstr->m_opcode == CEE_LDNULL)
    {
        Logger::Warn("*** DebuggerMethodRewriter::ApplyMethodProbe() Failed to load this for async method. "
                     "methodProbeId = ",
                     methodProbeId, " module_id= ", module_id, ", functon_token=", function_token);
        MarkAllMethodProbesAsError(methodProbes, async_method_could_not_load_this);
        return E_FAIL;
    }

    rewriterWrapper.LoadInt32(instrumentedMethodIndex);
    rewriterWrapper.LoadInt32(instrumentationVersion);
    rewriterWrapper.LoadToken(function_token);
    rewriterWrapper.LoadToken(caller->type.id);

    hr = debugger_tokens->WriteUpdateProbeInfo(&rewriterWrapper, &caller->type, &beginCallInstruction, probeType);
    IfFailRet(hr);

    /* BeginMethod */

    hr = LoadInstanceIntoStack(caller, isStatic, rewriterWrapper, &loadInstanceInstr, debugger_tokens);
    IfFailRet(hr);

    brFalse->m_pTarget = loadInstanceInstr;

    rewriterWrapper.LoadInt32(instrumentedMethodIndex);
    rewriterWrapper.LoadInt32(instrumentationVersion);
    
    loadInstanceInstr = rewriterWrapper.LoadArgument(0);
    rewriterWrapper.LoadFieldAddress(isReEntryFieldTok);
    hr = debugger_tokens->WriteBeginMethod_StartMarker(&rewriterWrapper, &caller->type, &beginCallInstruction, probeType);
    IfFailRet(hr);

    const auto& beginMethodTryLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);


    // *** BeginMethod call catch
    ILInstr* beginMethodCatchFirstInstr = rewriterWrapper.LoadArgument(0);
    rewriterWrapper.LoadFieldAddress(isReEntryFieldTok);
    debugger_tokens->WriteLogException(&rewriterWrapper, probeType);
    ILInstr* beginMethodCatchLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    EHClause beginMethodExClause{};
    beginMethodExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    beginMethodExClause.m_pTryBegin = tryInstruction->m_pNext;
    beginMethodExClause.m_pTryEnd = beginMethodCatchFirstInstr;
    beginMethodExClause.m_pHandlerBegin = beginMethodCatchFirstInstr;
    beginMethodExClause.m_pHandlerEnd = beginMethodCatchLeaveInstr;
    beginMethodExClause.m_ClassToken = debugger_tokens->GetExceptionTypeRef();
    newClauses.push_back(beginMethodExClause);

    ILInstr* beginOriginalMethodInstr = rewriterWrapper.GetCurrentILInstr();
    beginMethodTryLeaveInstr->m_pTarget = beginOriginalMethodInstr;
    beginMethodCatchLeaveInstr->m_pTarget = beginOriginalMethodInstr;

    for (const auto& branchInstr : branchTargets)
    {
        if (branchInstr->m_pTarget == nullptr)
        {
            branchInstr->m_pTarget = tryInstruction->m_pNext;
        }
    }

    // ***
    // ENDING OF THE METHOD EXECUTION
    // ***

    hr = EndAsyncMethodProbe(rewriterWrapper, module_metadata, debugger_tokens, caller, isStatic, methodReturnType,
                                methodLocals, numLocals, callTargetReturnIndex, isReEntryFieldTok, newClauses, probeType);

    if (FAILED(hr))
    {
        Logger::Error("DebuggerMethodRewriter::ApplyAsyncMethodProbe: Fail in EndAsyncMethodProbe");
        return hr;
    }

    return S_OK;
}

HRESULT DebuggerMethodRewriter::ApplyAsyncMethodSpanProbe(
    const std::shared_ptr<SpanProbeOnMethodDefinition>& spanProbe, ModuleID moduleId,
    ModuleMetadata& moduleMetadata, FunctionInfo* caller, DebuggerTokens* debuggerTokens, mdToken functionToken,
    bool isStatic, TypeSignature* methodReturnType, const std::vector<TypeSignature>& methodLocals, int numLocals,
    ILRewriterWrapper& rewriterWrapper, ULONG callTargetReturnIndex, ULONG returnValueIndex,
    mdToken callTargetReturnToken, ILInstr* firstInstruction, const int instrumentedMethodIndex,
    ILInstr* const& beforeLineProbe, std::vector<EHClause>& newClauses) const
{
    const auto& spanProbeId = spanProbe->probeId;

    mdFieldDef isReEntryFieldTok;
    HRESULT hr = debuggerTokens->GetIsFirstEntryToMoveNextFieldToken(caller->type.id, isReEntryFieldTok);
    IfFailRet(hr);

    if (isReEntryFieldTok == mdFieldDefNil)
    {
        Logger::Info("isReEntryField token is nil. Aborting an async instrumentation. module id:", moduleId,
                     " method: ", caller->type.name, ".", caller->name);
        return E_FAIL;
    }

    if (hr != S_OK)
    {
        Logger::Info("Failed to apply Method Probe on Async Method due to failure in the lookup of the isReEntry field "
                     "in the state machine. module id:",
                     moduleId, " method: ", caller->type.name, ".", caller->name);
        return S_OK; // We do not fail the whole instrumentation as there could be Line Probes that we want to emit.
                     // They do not suffer from the absence of the IsReEntry field.
    }

    LogDebugCallerInfo(caller, instrumentedMethodIndex);
    Logger::Info("Applying async method probe. module id:", moduleId, " method: ", caller->type.name, ".",
                 caller->name);

    rewriterWrapper.SetILPosition(beforeLineProbe);

    const auto& branchTargets = GetBranchTargets(rewriterWrapper.GetILRewriter());

    for (const auto& branchInstr : branchTargets)
    {
        if (branchInstr->m_pTarget == beforeLineProbe)
        {
            branchInstr->m_pTarget = nullptr;
        }
    }

    const auto tryInstruction = beforeLineProbe->m_pPrev;

    // ***
    // BEGIN SPAN PART
    // ***

    // Define ResourceName as string
    WSTRING resourceName =
        spanProbe->target_method.type.name.substr(spanProbe->target_method.type.name.find_last_of(L'.') + 1) +
        WStr(".") +
        spanProbe->target_method.method_name;

    mdString resourceNameIdToken;
    hr = moduleMetadata.metadata_emit->DefineUserString(
        resourceName.data(), static_cast<ULONG>(resourceName.size()), &resourceNameIdToken);

    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() DefineUserString of ResourceName is Failed. Aborting "
                        "an async instrumentation. module id:",
                        moduleId, " method: ", caller->type.name, ".", caller->name);
        return E_FAIL;
    }

    // Define OperationName as string
    mdString operationNameIdToken;
    hr = moduleMetadata.metadata_emit->DefineUserString(dynamic_span_operation_name.data(),
                                                        static_cast<ULONG>(dynamic_span_operation_name.size()),
                                                        &operationNameIdToken);

    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() DefineUserString of OperationName is Failed. Aborting "
                        "an async instrumentation. module id:",
                        moduleId, " method: ", caller->type.name, ".", caller->name);
        return E_FAIL;
    }

    ILInstr* loadStrInstr;
    hr = LoadProbeIdIntoStack(moduleId, moduleMetadata, functionToken, spanProbeId, rewriterWrapper, &loadStrInstr);
    IfFailRet(hr);
    rewriterWrapper.LoadStr(resourceNameIdToken);
    rewriterWrapper.LoadStr(operationNameIdToken);
    rewriterWrapper.LoadArgument(0);
    rewriterWrapper.LoadFieldAddress(isReEntryFieldTok);

    ILInstr* beginCallInstruction;
    hr = debuggerTokens->WriteBeginSpan(&rewriterWrapper, &caller->type, &beginCallInstruction,
                                        /* isAsyncMethod */ true);
    IfFailRet(hr);

    const auto& beginMethodTryLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    // *** BeginMethod call catch
    ILInstr* beginMethodCatchFirstInstr = rewriterWrapper.LoadArgument(0);
    rewriterWrapper.LoadFieldAddress(isReEntryFieldTok);
    debuggerTokens->WriteLogException(&rewriterWrapper, AsyncMethodSpanProbe);
    ILInstr* beginMethodCatchLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    EHClause beginMethodExClause{};
    beginMethodExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    beginMethodExClause.m_pTryBegin = tryInstruction->m_pNext;
    beginMethodExClause.m_pTryEnd = beginMethodCatchFirstInstr;
    beginMethodExClause.m_pHandlerBegin = beginMethodCatchFirstInstr;
    beginMethodExClause.m_pHandlerEnd = beginMethodCatchLeaveInstr;
    beginMethodExClause.m_ClassToken = debuggerTokens->GetExceptionTypeRef();
    newClauses.push_back(beginMethodExClause);

    ILInstr* beginOriginalMethodInstr = rewriterWrapper.GetCurrentILInstr();
    beginMethodTryLeaveInstr->m_pTarget = beginOriginalMethodInstr;
    beginMethodCatchLeaveInstr->m_pTarget = beginOriginalMethodInstr;

    for (const auto& branchInstr : branchTargets)
    {
        if (branchInstr->m_pTarget == nullptr)
        {
            branchInstr->m_pTarget = tryInstruction->m_pNext;
        }
    }

    // ***
    // ENDING OF THE METHOD EXECUTION
    // ***

    hr = EndAsyncMethodSpanProbe(rewriterWrapper, moduleMetadata, debuggerTokens, caller, isStatic, methodReturnType,
                                methodLocals, numLocals, callTargetReturnIndex, isReEntryFieldTok, newClauses);

    if (FAILED(hr))
    {
        Logger::Error("DebuggerMethodRewriter::ApplyAsyncMethodProbe: Fail in EndAsyncMethodSpanProbe");
        return hr;
    }

    return S_OK;
}

bool DebuggerMethodRewriter::DoesILContainUnsupportedInstructions(ILRewriter& rewriter)
{
    for (auto pInstr = rewriter.GetILList()->m_pNext; pInstr != rewriter.GetILList(); pInstr = pInstr->m_pNext)
    {
        if (pInstr->m_opcode == CEE_JMP || pInstr->m_opcode == CEE_TAILCALL /* F# */ )
        {
            return true;
        }
    }
    return false;
}

HRESULT DebuggerMethodRewriter::IsTypeImplementIAsyncStateMachine(const ComPtr<IMetaDataImport2>& metadataImport,
                                                                  const ULONG32 typeToken, bool& isTypeImplementIAsyncStateMachine)
{
    HCORENUM interfaceImplsEnum = nullptr;
    ULONG actualImpls;
    mdInterfaceImpl impls;
    // check if the nested type implement the IAsyncStateMachine interface
    const auto hr = metadataImport->EnumInterfaceImpls(&interfaceImplsEnum, typeToken, &impls, 1, &actualImpls);
    metadataImport->CloseEnum(interfaceImplsEnum);
    if (hr != S_OK)
    {
        // FAILED or S_FALSE
        return hr;
    }

    if (actualImpls != 1)
    {
        // our compiler generated nested type should implement exactly one interface
        isTypeImplementIAsyncStateMachine = false;
        return S_OK;
    }

    mdToken classToken, interfaceToken;
    // get the interface token
    if (metadataImport->GetInterfaceImplProps(impls, &classToken, &interfaceToken) != S_OK)
    {
        Logger::Warn("DebuggerMethodRewriter::IsTypeImplementIAsyncStateMachine: failed to get interface props");
        return E_FAIL;
    }

    // get the interface type props
    WCHAR type_name[kNameMaxSize]{};
    DWORD type_name_len = 0;
    mdAssembly assemblyToken;
    if (metadataImport->GetTypeRefProps(interfaceToken, &assemblyToken, type_name, kNameMaxSize, &type_name_len) != S_OK)
    {
        Logger::Warn("DebuggerMethodRewriter::IsTypeImplementIAsyncStateMachine: failed to get type ref props");
        return E_FAIL;
    }


    // if the interface is the IAsyncStateMachine
    if (classToken == typeToken && type_name == IAsyncStateMachineName)
    {
        isTypeImplementIAsyncStateMachine = true;
        return S_OK;
    }

    isTypeImplementIAsyncStateMachine = false;
    return S_OK;
}

HRESULT DebuggerMethodRewriter::IsAsyncMethodProbe(const ComPtr<IMetaDataImport2>& metadataImport,
                                                   const FunctionInfo* caller, bool& isAsyncMethod) const
{
    if (caller->name != WStr("MoveNext") || caller->method_signature.NumberOfArguments() > 0 ||
        std::get<unsigned>(caller->method_signature.GetReturnValue().GetElementTypeAndFlags()) != ELEMENT_TYPE_VOID)
    {
        isAsyncMethod = false;
        return S_OK;
    }

    return IsTypeImplementIAsyncStateMachine(metadataImport, caller->type.id, isAsyncMethod);
}

HRESULT DebuggerMethodRewriter::GetTaskReturnType(const ILInstr* instruction, ModuleMetadata& moduleMetadata, const std::vector<TypeSignature>& methodLocals, TypeSignature* returnType)
{
    for (const ILInstr* pInstr = instruction->m_pPrev;
        pInstr != instruction; 
        pInstr = pInstr->m_pPrev)
    {
        // It is a call to a struct method so CALL instruction but pay attention to change it if the runtime changes
        if (pInstr->m_opcode != CEE_CALL)
        {
            continue;
        }

        auto functionInfo = GetFunctionInfo(moduleMetadata.metadata_import, pInstr->m_Arg32);
        if (functionInfo.name == WStr("SetException"))
        {
            // We go through the instructions in reverse order so if we're already in SetException,
            // it means that there is not SetResult in the current MoveNext method
            return S_FALSE;
        }

        if (functionInfo.name == WStr("SetResult"))
        {
            if (ILRewriter::IsLoadLocalDirectInstruction(pInstr->m_pPrev->m_opcode) /*meaning that the task return T value*/)
            {
                // get the index of the local that represent the return value of the task
                const auto returnValueLocalIndex =ILRewriter::GetLocalIndexFromOpcode(pInstr);
                *returnType = methodLocals[returnValueLocalIndex];
            }
            else
            {
                return S_FALSE;
            }

            return S_OK;
        }
    }

    Logger::Error("DebuggerMethodRewriter::GetTaskReturnType: Failed to get task result");
    return E_FAIL;
}

void DebuggerMethodRewriter::MarkAllProbesAsInstrumented(MethodProbeDefinitions& methodProbes,
    LineProbeDefinitions& lineProbes, SpanProbeOnMethodDefinitions& spanOnMethodProbes)
{
    for (const auto& probe : methodProbes)
    {
        ProbesMetadataTracker::Instance()->SetProbeStatus(probe->probeId, ProbeStatus::INSTRUMENTED);
    }

    for (const auto& probe : lineProbes)
    {
        ProbesMetadataTracker::Instance()->SetProbeStatus(probe->probeId, ProbeStatus::INSTRUMENTED);
    }

    for (const auto& probe : spanOnMethodProbes)
    {
        ProbesMetadataTracker::Instance()->SetProbeStatus(probe->probeId, ProbeStatus::INSTRUMENTED);
    }
}

void DebuggerMethodRewriter::MarkAllProbesAsError(MethodProbeDefinitions& methodProbes,
                                                  LineProbeDefinitions& lineProbes,
                                                  SpanProbeOnMethodDefinitions& spanOnMethodProbes,
                                                  const WSTRING& reasoning)
{
    // Mark all probes as Error
    MarkAllLineProbesAsError(lineProbes, reasoning);
    MarkAllMethodProbesAsError(methodProbes, reasoning);
    MarkAllSpanOnMethodProbesAsError(spanOnMethodProbes, reasoning);
}

void DebuggerMethodRewriter::MarkAllLineProbesAsError(LineProbeDefinitions& lineProbes, const WSTRING& reasoning)
{
    for (const auto& probe : lineProbes)
    {
        ProbesMetadataTracker::Instance()->SetErrorProbeStatus(probe->probeId, reasoning);
    }
}

void DebuggerMethodRewriter::MarkAllMethodProbesAsError(MethodProbeDefinitions& methodProbes, const WSTRING& reasoning)
{
    for (const auto& probe : methodProbes)
    {
        ProbesMetadataTracker::Instance()->SetErrorProbeStatus(probe->probeId, reasoning);
    }
}

void DebuggerMethodRewriter::MarkAllSpanOnMethodProbesAsError(SpanProbeOnMethodDefinitions& spanProbes,
                                                            const WSTRING& reasoning)
{
    for (const auto& probe : spanProbes)
    {
        ProbesMetadataTracker::Instance()->SetErrorProbeStatus(probe->probeId, reasoning);
    }
}

HRESULT DebuggerMethodRewriter::Rewrite(RejitHandlerModule* moduleHandler,
                                        RejitHandlerModuleMethod* methodHandler,
                                        ICorProfilerFunctionControl* pFunctionControl,
                                        ICorProfilerInfo* pCorProfilerInfo,
                                        MethodProbeDefinitions& methodProbes,
                                        LineProbeDefinitions& lineProbes,
                                        SpanProbeOnMethodDefinitions& spanOnMethodProbes) const
{
    ModuleID module_id = moduleHandler->GetModuleId();
    ModuleMetadata& module_metadata = *moduleHandler->GetModuleMetadata();
    FunctionInfo* caller = methodHandler->GetFunctionInfo();
    DebuggerTokens* debuggerTokens = module_metadata.GetDebuggerTokens();
    mdToken function_token = caller->id;
    TypeSignature retFuncArg = caller->method_signature.GetReturnValue();
    const auto [retFuncElementType, retTypeFlags] = retFuncArg.GetElementTypeAndFlags();
    bool isVoid = (retTypeFlags & TypeFlagVoid) > 0;
    bool isStatic = !(caller->method_signature.CallingConvention() & IMAGE_CEE_CS_CALLCONV_HASTHIS);
    std::vector<TypeSignature> methodArguments = caller->method_signature.GetMethodArguments();
    int numArgs = caller->method_signature.NumberOfArguments();

    if (caller->type.name.rfind(L'@') != std::wstring::npos)
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() Encountered a type with '@' in it's name - it's not supported since the realization of generic is non-deterministic (it does not contain the ` in it's name if it's a generic type)."
                     "token=", function_token, " caller_name=", caller->type.name, ".", caller->name, "()");
        MarkAllProbesAsError(methodProbes, lineProbes, spanOnMethodProbes, general_error_message);
        return E_NOTIMPL;
    }
    
    if (retTypeFlags & TypeFlagByRef || caller->name == WStr(".ctor") || caller->name == WStr(".cctor"))
    {
        // Internal Jira ticket: DEBUG-1063, DEBUG-1065.
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() Placing probes on a method with ref return/constructor is not supported for now. token=",
                     function_token, " caller_name=", caller->type.name, ".", caller->name, "()");

        const WSTRING& reasoning = caller->name == WStr(".ctor") || caller->name == WStr(".cctor")
                                       ? invalid_probe_probe_cctor_ctor_not_supported
                                       : invalid_probe_probe_byreflike_return_not_supported;
        MarkAllProbesAsError(methodProbes, lineProbes, spanOnMethodProbes, reasoning);
        return E_NOTIMPL;
    }

    // First we check if the managed profiler has not been loaded yet
    if (!m_corProfiler->ProfilerAssemblyIsLoadedIntoAppDomain(module_metadata.app_domain_id))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() skipping method: The managed profiler has "
                     "not yet been loaded into AppDomain with id=",
                     module_metadata.app_domain_id, " token=", function_token, " caller_name=", caller->type.name, ".",
                     caller->name, "()");
        MarkAllProbesAsError(methodProbes, lineProbes, spanOnMethodProbes, profiler_assemly_is_not_loaded);
        return S_FALSE;
    }

    // *** Create rewriter
    ILRewriter rewriter(pCorProfilerInfo, pFunctionControl, module_id, function_token);
    auto hr = rewriter.Import();
    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() Call to ILRewriter.Import() failed for ", module_id, " ",
                     function_token);
        MarkAllProbesAsError(methodProbes, lineProbes, spanOnMethodProbes, invalid_probe_failed_to_import_method_il);
        return E_FAIL;
    }

    if (caller->type.name.rfind(L'@') != std::wstring::npos)
    {
        auto errorMessage = type_contains_invalid_symbol + WStr("caller_name =") +
                       caller->type.name + WStr(".") + caller->name;
        MarkAllProbesAsError(methodProbes, lineProbes, spanOnMethodProbes, errorMessage);
        return E_NOTIMPL;
    }

    if (DoesILContainUnsupportedInstructions(rewriter))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite(): IL contain unsupported instructions (i.e. jmp, tail)");
        MarkAllProbesAsError(methodProbes, lineProbes, spanOnMethodProbes, non_supported_compiled_bytecode);
        return E_NOTIMPL;
    }

    // *** Store the original il code text if the dump_il option is enabled.
    std::string original_code;
    if (IsDumpILRewriteEnabled())
    {
        original_code = m_corProfiler->GetILCodes("*** DebuggerMethodRewriter::Rewrite() Original Code: ", &rewriter,
                                                *caller, module_metadata.metadata_import);
    }

    // *** Get the locals signature.
    FunctionLocalSignature localSignature;
    hr = GetFunctionLocalSignature(module_metadata, rewriter, localSignature);

    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() failed to parse locals signature for ", module_id, " ",
                     function_token);
        MarkAllProbesAsError(methodProbes, lineProbes, spanOnMethodProbes, invalid_probe_failed_to_parse_locals);
        return E_FAIL;
    }

    std::vector<TypeSignature> methodLocals = localSignature.GetMethodLocals();
    int numLocals = localSignature.NumberOfLocals();

    if (trace::Logger::IsDebugEnabled())
    {
        Logger::Debug("*** DebuggerMethodRewriter::Rewrite() Start: ", caller->type.name, ".", caller->name,
                      "() [IsVoid=", isVoid, ", IsStatic=", isStatic, ", Arguments=", numArgs, "]");
    }

    // *** Create the rewriter wrapper helper
    ILRewriterWrapper rewriterWrapper(&rewriter);
    rewriterWrapper.SetILPosition(rewriter.GetILList()->m_pNext);
    // *** Modify the Local Var Signature of the method and initialize the new local vars
    ULONG callTargetStateIndex = static_cast<ULONG>(ULONG_MAX);
    ULONG exceptionIndex = static_cast<ULONG>(ULONG_MAX);
    ULONG callTargetReturnIndex = static_cast<ULONG>(ULONG_MAX);
    ULONG returnValueIndex = static_cast<ULONG>(ULONG_MAX);
    mdToken callTargetStateToken = mdTokenNil;
    mdToken exceptionToken = mdTokenNil;
    mdToken callTargetReturnToken = mdTokenNil;
    ILInstr* firstInstruction;

    bool isAsyncMethod;
    auto isAsyncMethodProbeHr = IsAsyncMethodProbe(module_metadata.metadata_import, caller, isAsyncMethod);

    if (FAILED(isAsyncMethodProbeHr))
    {
        MarkAllProbesAsError(methodProbes, lineProbes, spanOnMethodProbes, failed_to_determine_if_method_is_async);
        return isAsyncMethodProbeHr;
    }

    TypeSignature methodReturnType{};
    if (isAsyncMethod)
    {
        hr = GetTaskReturnType(rewriterWrapper.GetILRewriter()->GetILList(), module_metadata, methodLocals, &methodReturnType);

        if (FAILED(hr))
        {
            MarkAllProbesAsError(methodProbes, lineProbes, spanOnMethodProbes, failed_to_retrieve_task_return_type);
            return hr;
        }
        
        if (hr == S_FALSE) // return type is System.Void
        {
            methodReturnType = caller->method_signature.GetReturnValue();
        }
    }
    else
    {
        methodReturnType = caller->method_signature.GetReturnValue();
    }
    auto debuggerLocals = std::vector<ULONG>(debuggerTokens->GetAdditionalLocalsCount(methodArguments));
    hr = debuggerTokens->ModifyLocalSigAndInitialize(&rewriterWrapper, &methodReturnType, &methodArguments, &callTargetStateIndex, &exceptionIndex,
                                                     &callTargetReturnIndex, &returnValueIndex, &callTargetStateToken,
                                                     &exceptionToken, &callTargetReturnToken, &firstInstruction, debuggerLocals, isAsyncMethod);

    ULONG lineProbeCallTargetStateIndex = debuggerLocals[0];
    ULONG spanMethodStateIndex = debuggerLocals[1];
    ULONG multiProbeStatesIndex = debuggerLocals[2];

    if (FAILED(hr))
    {
        MarkAllProbesAsError(methodProbes, lineProbes, spanOnMethodProbes, invalid_probe_failed_to_add_di_locals);
        // Error message is already written in ModifyLocalSigAndInitialize
        return S_FALSE; // TODO https://datadoghq.atlassian.net/browse/DEBUG-706
    }

    if (!isAsyncMethod)
    {
        // In async methods, the return value can't be byref-like (it can't be Task<T> where T is byref-like, because
        // byref-like can't exist as a generic param). Therefore, we only need to worry about non-async methods.
        bool isTypeIsByRefLike = false;
        hr = IsTypeByRefLike(m_corProfiler->info_, module_metadata, methodReturnType, debuggerTokens->GetCorLibAssemblyRef(), isTypeIsByRefLike);
        if (FAILED(hr))
        {
            Logger::Warn("DebuggerRewriter: Failed to determine if the return value is By-Ref like.");
        }
        else if (isTypeIsByRefLike)
        {
            MarkAllProbesAsError(methodProbes, lineProbes, spanOnMethodProbes, invalid_probe_probe_byreflike_return_not_supported);
            return E_NOTIMPL;
        }
    }

    bool isTypeIsByRefLike = false;
    hr = IsTypeTokenByRefLike(m_corProfiler->info_, module_metadata, caller->type.id, isTypeIsByRefLike);

    if (FAILED(hr))
    {
        Logger::Warn("DebuggerRewriter: Failed to determine if the type we are instrumenting is By-Ref like.");
    }
    else if (isTypeIsByRefLike)
    {
        MarkAllProbesAsError(methodProbes, lineProbes, spanOnMethodProbes, invalid_probe_type_is_by_ref_like);
        return E_NOTIMPL;
    }

    const auto instrumentedMethodIndex = ProbesMetadataTracker::Instance()->GetInstrumentedMethodIndex(module_id, function_token);
    std::vector<EHClause> newClauses;

    // ***
    // BEGIN LINE PROBES PART
    // ***

    auto beforeLineProbe = rewriterWrapper.GetCurrentILInstr()->m_pPrev;

    // TODO support multiple line probes & multiple line probes on the same bytecode offset (by deduplicating the probe ids)

    bool appliedAtLeastOneLineProbeInstrumentation = false;
    if (!lineProbes.empty())
    {
        if (isAsyncMethod)
        {
            Logger::Info("Applying ", lineProbes.size(), " Async Line Probe(s)");
        }
        else
        {
            Logger::Info("Applying ", lineProbes.size(), " Non-Async Line Probe(s)");
        }

        hr = ApplyLineProbes(instrumentedMethodIndex, lineProbes, module_id, module_metadata, caller,
                                 debuggerTokens, function_token, isStatic, methodArguments, numArgs, rewriter,
                                 methodLocals, numLocals, rewriterWrapper, lineProbeCallTargetStateIndex, newClauses, isAsyncMethod);

        if (hr != E_NOTIMPL && FAILED(hr))
        {
            MarkAllProbesAsError(methodProbes, lineProbes, spanOnMethodProbes, invalid_probe_failed_to_instrument_line_probe);
            // Appropriate error message is already logged in ApplyLineProbes.
            return E_FAIL;            
        }

        if (hr == E_NOTIMPL)
        {
            // Probe status already marked in ApplyLineProbes for the E_NOTIMPL scenario
            Logger::Info("Emplacement of a line probe is not supported.");
        }

        appliedAtLeastOneLineProbeInstrumentation = hr == S_OK;
    }

    beforeLineProbe = beforeLineProbe->m_pNext;

    // ***
    // BEGIN METHOD PROBE PART
    // ***

    bool appliedAtLeastOneSpanProbeInstrumentation = false;
    if (!spanOnMethodProbes.empty())
    {
        // TODO accept multiple probeIds
        const auto& spanProbe = spanOnMethodProbes[0];
        const auto& spanProbeId = spanProbe->probeId;

        if (isAsyncMethod)
        {
            Logger::Info("Applying Async Span Probe instrumentation with probeId.", spanProbeId);
            hr = ApplyAsyncMethodSpanProbe(
                spanProbe, module_id, module_metadata, caller, debuggerTokens, function_token, isStatic,
                &methodReturnType, methodLocals, numLocals, rewriterWrapper, callTargetReturnIndex, returnValueIndex,
                callTargetReturnToken, firstInstruction, instrumentedMethodIndex, beforeLineProbe, newClauses);
        }
        else
        {
            Logger::Info("Applying Non-Async Span Probe instrumentation with probeId.", spanProbeId);
            hr = ApplyMethodSpanProbe(module_id, module_metadata, caller, debuggerTokens, function_token, retFuncArg,
                                      isVoid, isStatic, methodArguments, numArgs, spanProbe, rewriter, methodLocals,
                                      numLocals, rewriterWrapper, spanMethodStateIndex, exceptionIndex,
                                      callTargetReturnIndex, returnValueIndex, callTargetReturnToken,
                                      instrumentedMethodIndex, beforeLineProbe, newClauses);
        }

        if (hr != E_NOTIMPL && FAILED(hr))
        {
            MarkAllProbesAsError(methodProbes, lineProbes, spanOnMethodProbes,
                                 invalid_probe_failed_to_instrument_method_probe);
            // Appropriate error message is already logged in ApplyMethodProbe / ApplyAsyncMethodProbe.
            return E_FAIL;
        }

        if (hr == E_NOTIMPL)
        {
            ProbesMetadataTracker::Instance()->SetErrorProbeStatus(spanProbeId,
                                                                   invalid_method_probe_probe_is_not_supported);
            Logger::Info("Emplacement of a span probe is not supported.");
        }

        appliedAtLeastOneSpanProbeInstrumentation = hr == S_OK;
    }

    bool appliedAtLeastOneMethodProbeInstrumentation = false;
    if (!methodProbes.empty())
    {
        if (isAsyncMethod)
        {
            Logger::Info("Applying Async Method Probe instrumentation with ", methodProbes.size(), " probes.");
            hr = ApplyAsyncMethodProbe(methodProbes, module_id, module_metadata, caller, debuggerTokens,
                                       function_token,
                                       isStatic, &methodReturnType, methodLocals, numLocals,
                                       rewriterWrapper, callTargetReturnIndex, returnValueIndex,
                                       callTargetReturnToken, firstInstruction, instrumentedMethodIndex,
                                       beforeLineProbe, newClauses);
        }
        else
        {
            Logger::Info("Applying Non-Async  Method Probe instrumentation with ", methodProbes.size(), " probes.");

            hr = ApplyMethodProbe(methodProbes, module_id, module_metadata, caller, debuggerTokens, function_token,
                                  retFuncArg, isVoid, isStatic, methodArguments, numArgs, rewriter,
                                  methodLocals, numLocals, rewriterWrapper, callTargetStateIndex, exceptionIndex,
                                  callTargetReturnIndex, returnValueIndex, multiProbeStatesIndex, callTargetReturnToken,
                                  firstInstruction, instrumentedMethodIndex, beforeLineProbe, newClauses);
        }

        if (hr != E_NOTIMPL && FAILED(hr))
        {
            MarkAllProbesAsError(methodProbes, lineProbes, spanOnMethodProbes, invalid_probe_failed_to_instrument_method_probe);
            // Appropriate error message is already logged in ApplyMethodProbe / ApplyAsyncMethodProbe.
            return E_FAIL;
        }

        appliedAtLeastOneMethodProbeInstrumentation = hr == S_OK;
    }

    if (!appliedAtLeastOneMethodProbeInstrumentation && !appliedAtLeastOneLineProbeInstrumentation &&
        !appliedAtLeastOneSpanProbeInstrumentation)
    {
        Logger::Info("There are no Method, Span or Line probes instrumentations. Skipping method instrumentation.");
        return S_FALSE;
    }

    // ***
    // Update and Add exception clauses
    // ***

    auto ehCount = rewriter.GetEHCount();
    auto ehPointer = rewriter.GetEHPointer();
    auto newClausesCount = static_cast<int>(newClauses.size());
    auto newEHClauses = new EHClause[ehCount + newClausesCount];
    for (unsigned i = 0; i < ehCount; i++)
    {
        newEHClauses[i] = ehPointer[i];
    }

    // *** Add the new EH clauses
    ehCount += newClausesCount;
    
    for (auto ehClauseIndex = 0; ehClauseIndex < newClausesCount; ehClauseIndex++)
    {
        newEHClauses[ehCount - newClausesCount + ehClauseIndex] = newClauses[ehClauseIndex];
    }

    rewriter.SetEHClause(newEHClauses, ehCount);

    if (IsDumpILRewriteEnabled())
    {
        Logger::Info(original_code);
        Logger::Info(m_corProfiler->GetILCodes("*** Rewriter(): Modified Code: ", &rewriter, *caller,
                                             module_metadata.metadata_import));
    }

    hr = rewriter.Export();

    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() Call to ILRewriter.Export() failed for "
                     "ModuleID=",
                     module_id, " ", function_token);
        MarkAllProbesAsError(methodProbes, lineProbes, spanOnMethodProbes, failed_to_export_method_il);
        return E_FAIL;
    }

    Logger::Info("*** DebuggerMethodRewriter::Rewrite() Finished: ", caller->type.name, ".", caller->name,
                 "() [IsVoid=", isVoid, ", IsStatic=", isStatic, ", Arguments=", numArgs, "]");

    hr = this->m_corProfiler->info_->ApplyMetaData(module_id);
    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() Finished: Error applying metadata to module_id: ", module_id);
    }

    return S_OK;
}

void DebuggerMethodRewriter::AdjustExceptionHandlingClauses(ILInstr* pFromInstr, ILInstr* pToInstr,
                                                            ILRewriter* pRewriter)
{
    const auto ehClauses = pRewriter->GetEHPointer();
    const auto ehCount = pRewriter->GetEHCount();

    for (unsigned ehIndex = 0; ehIndex < ehCount; ehIndex++)
    {
        if (ehClauses[ehIndex].m_pTryEnd == pFromInstr)
        {
            ehClauses[ehIndex].m_pTryEnd = pToInstr;
        }

        if (ehClauses[ehIndex].m_pTryBegin == pFromInstr)
        {
            ehClauses[ehIndex].m_pTryBegin = pToInstr;
        }

        if (ehClauses[ehIndex].m_pHandlerBegin == pFromInstr)
        {
            ehClauses[ehIndex].m_pHandlerBegin = pToInstr;
        }

        if (ehClauses[ehIndex].m_pFilter == pFromInstr)
        {
            ehClauses[ehIndex].m_pFilter = pToInstr;
        }
    }
}

std::vector<ILInstr*> DebuggerMethodRewriter::GetBranchTargets(ILRewriter* pRewriter)
{
    std::vector<ILInstr*> branchTargets{};

    for (auto pInstr = pRewriter->GetILList()->m_pNext; pInstr != pRewriter->GetILList(); pInstr = pInstr->m_pNext)
    {
        if (ILRewriter::IsBranchTarget(pInstr))
        {
            branchTargets.emplace_back(pInstr);
        }
    }

    return branchTargets;
}

void DebuggerMethodRewriter::AdjustBranchTargets(ILInstr* pFromInstr, ILInstr* pToInstr,
                                                 const std::vector<ILInstr*>& branchTargets)
{
    for (const auto& branchInstr : branchTargets)
    {
        if (branchInstr->m_pTarget == pFromInstr)
        {
            branchInstr->m_pTarget = pToInstr;
        }
    }
}

} // namespace debugger