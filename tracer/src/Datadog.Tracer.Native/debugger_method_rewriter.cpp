#include "debugger_method_rewriter.h"
#include "debugger_rejit_handler_module_method.h"
#include "cor_profiler.h"
#include "il_rewriter_wrapper.h"
#include "logger.h"
#include "stats.h"
#include "version.h"
#include "environment_variables_util.h"
#include "probes_tracker.h"

namespace debugger
{

int DebuggerMethodRewriter::GetNextInstrumentedMethodIndex()
{
    return std::atomic_fetch_add(&_nextInstrumentedMethodIndex, 1);
}

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

HRESULT DebuggerMethodRewriter::LoadArgument(CorProfiler* corProfiler, bool isStatic, const ILRewriterWrapper& rewriterWrapper, int argumentIndex, const TypeSignature& argument)
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

HRESULT DebuggerMethodRewriter::LoadLocal(CorProfiler* corProfiler, const ILRewriterWrapper& rewriterWrapper, int localIndex, const TypeSignature& local)
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
    CorProfiler* corProfiler, 
    DebuggerTokens* debuggerTokens, 
    bool isStatic, 
    const std::vector<TypeSignature>& methodArgsOrLocals, 
    int numArgsOrLocals, 
    ILRewriterWrapper& rewriterWrapper, 
    ULONG callTargetStateIndex, 
    ILInstr** beginCallInstruction,
    bool isArgs, 
    bool isMethodProbe)
{
    for (auto argOrLocalIndex = 0; argOrLocalIndex < numArgsOrLocals; argOrLocalIndex++)
    {
        const auto argOrLocal = methodArgsOrLocals[argOrLocalIndex];
        HRESULT hr;

        if (isArgs)
        {
            hr = LoadArgument(corProfiler, isStatic, rewriterWrapper, argOrLocalIndex, argOrLocal);
        }
        else
        {
            hr = LoadLocal(corProfiler, rewriterWrapper, argOrLocalIndex, argOrLocal);
        }

        if (FAILED(hr))
        {
            Logger::Warn("DebuggerRewriter: Failed load ", isArgs ? "argument" : "local", " index = ", argOrLocalIndex,
                         " into the stack.");
            return E_FAIL;
        }

        // Load the index of the argument/local
        rewriterWrapper.LoadInt32(argOrLocalIndex);

        // Load the DebuggerState
        rewriterWrapper.LoadLocalAddress(callTargetStateIndex);

        if (isArgs)
        {
            hr = debuggerTokens->WriteLogArg(&rewriterWrapper, methodArgsOrLocals[argOrLocalIndex], beginCallInstruction, isMethodProbe);
        }
        else
        {
            hr = debuggerTokens->WriteLogLocal(&rewriterWrapper, methodArgsOrLocals[argOrLocalIndex], beginCallInstruction, isMethodProbe);
        }

        if (FAILED(hr))
        {
            Logger::Warn("DebuggerRewriter: Failed in ", isArgs ? "WriteLogArg" : "WriteLogLocal", " with index=", argOrLocalIndex);
            return E_FAIL;
        }
    }
    return false;
}

HRESULT
DebuggerMethodRewriter::WriteCallsToLogArg(CorProfiler* corProfiler, DebuggerTokens* debuggerTokens, bool isStatic,
                                             const std::vector<TypeSignature>& args,
                                             int numArgs, ILRewriterWrapper& rewriterWrapper, ULONG callTargetStateIndex,
                                           ILInstr** beginCallInstruction, bool isMethodProbe)
{
    return WriteCallsToLogArgOrLocal(corProfiler, debuggerTokens, isStatic, args, numArgs, rewriterWrapper,
                                     callTargetStateIndex, beginCallInstruction, /* IsArgs */ true, isMethodProbe);
}

HRESULT
DebuggerMethodRewriter::WriteCallsToLogLocal(CorProfiler* corProfiler, DebuggerTokens* debuggerTokens, bool isStatic,
                                             const std::vector<TypeSignature>& locals,
                                             int numLocals, ILRewriterWrapper& rewriterWrapper, ULONG callTargetStateIndex,
                                             ILInstr** beginCallInstruction, bool isMethodProbe)
{
    return WriteCallsToLogArgOrLocal(corProfiler, debuggerTokens, isStatic, locals, numLocals, rewriterWrapper,
                                     callTargetStateIndex, beginCallInstruction, /* IsArgs */ false, isMethodProbe);
}

HRESULT DebuggerMethodRewriter::LoadInstanceIntoStack(FunctionInfo* caller, bool isStatic, const ILRewriterWrapper& rewriterWrapper, ILInstr** outLoadArgumentInstr)
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
            Logger::Warn("*** DebuggerMethodRewriter::Rewrite() Static methods in a ValueType cannot be instrumented. ");
            return E_FAIL;
        }
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
                // Generic struct instrumentation is not supported
                // IMetaDataImport::GetMemberProps and IMetaDataImport::GetMemberRefProps returns
                // The parent token as mdTypeDef and not as a mdTypeSpec
                // that's because the method definition is stored in the mdTypeDef
                // This problem doesn't occur on reference types because in that scenario,
                // we can always rely on the object's type.
                // This problem doesn't occur on a class type because we can always relay in the
                // object type.
                return E_FAIL;
            }
        }
    }

    return S_OK;
}

HRESULT DebuggerMethodRewriter::Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler)
{
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

    const auto& probes = debuggerMethodHandler->GetProbes();

    if (probes.empty())
    {
        Logger::Info("There are no probes for methodDef: ", methodHandler->GetMethodDef());
        return S_OK;
    }

    Logger::Info("About to apply debugger instrumentation on ", probes.size(), " probes for methodDef: ", methodHandler->GetMethodDef());

    for (const auto& probe : probes)
    {
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
        }
    }

    if (methodProbes.empty() && lineProbes.empty())
    {
        // No lines probes & method probes. Should not happen unless the user requested to undo the instrumentation while the method got executed.
        Logger::Info("There are no method probes and lines probes for methodDef", methodHandler->GetMethodDef());
        return S_OK;
    }
    else
    {
        Logger::Info("Applying ", methodProbes.size(), " method probes and ", lineProbes.size(),
                     " line probes on methodDef: ", methodHandler->GetMethodDef());

        const auto hr = Rewrite(moduleHandler, methodHandler, methodProbes, lineProbes);

        if (FAILED(hr))
        {
            // Mark all probes as Error
            for (const auto& probe : probes)
            {
                ProbesMetadataTracker::Instance()->SetProbeStatus(probe->probeId, ProbeStatus::_ERROR);
            }
        }
    }

    return S_OK;
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
    CorProfiler* corProfiler, 
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
    const std::shared_ptr<LineProbeDefinition>& lineProbe)
{
    const auto& lineProbeId = lineProbe->probeId;
    const auto& bytecodeOffset = lineProbe->bytecodeOffset;
    const auto& probeLineNumber = lineProbe->lineNumber;
    const auto& probeFilePath = lineProbe->probeFilePath;

    ILInstr* lineProbeFirstInstruction;
    auto hr = rewriter.GetInstrFromOffset(bytecodeOffset, &lineProbeFirstInstruction);

    if (FAILED(hr))
    {
        // Note we are not sabotaging the whole rewriting upon failure to lookup for a specific bytecode offset.
        // TODO Upon implementing the Probe Statuses, we'll need to reflect that.
        return S_OK;
    }

    if (lineProbeFirstInstruction->m_opcode == CEE_NOP || lineProbeFirstInstruction->m_opcode == CEE_BR_S ||
        lineProbeFirstInstruction->m_opcode == CEE_BR)
    {
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
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() DefineUserStringFailed. MethodProbeId = ", lineProbeId,
                     " module_id= ", module_id, ", functon_token=", function_token);
        return hr;
    }

    rewriterWrapper.LoadStr(lineProbeIdToken);

    ILInstr* loadInstanceInstr;
    hr = LoadInstanceIntoStack(caller, isStatic, rewriterWrapper, &loadInstanceInstr);

    IfFailRet(hr);

    rewriterWrapper.LoadToken(function_token);
    rewriterWrapper.LoadToken(caller->type.id);
    rewriterWrapper.LoadInt32(instrumentedMethodIndex);
    rewriterWrapper.LoadInt32(probeLineNumber);
    rewriterWrapper.LoadStr(lineProbeFilePathToken);

    // *** Emit BeginLine call

    ILInstr* beginLineCallInstruction;
    hr = debuggerTokens->WriteBeginLine(&rewriterWrapper, &caller->type, &beginLineCallInstruction);

    IfFailRet(hr);

    rewriterWrapper.StLocal(lineProbeCallTargetStateIndex);

    // *** Emit LogLocal call(s)
    hr = WriteCallsToLogLocal(corProfiler, debuggerTokens, isStatic, methodLocals, numLocals, rewriterWrapper,
                              lineProbeCallTargetStateIndex, &beginLineCallInstruction,
                              /* isMethodProbe */ false);

    IfFailRet(hr);

    // *** Emit LogArg call(s)
    hr = WriteCallsToLogArg(corProfiler, debuggerTokens, isStatic, methodArguments, numArgs, rewriterWrapper,
                            lineProbeCallTargetStateIndex, &beginLineCallInstruction, /* isMethodProbe */ false);

    IfFailRet(hr);

    // Load the DebuggerState
    rewriterWrapper.LoadLocalAddress(lineProbeCallTargetStateIndex);
    hr = debuggerTokens->WriteEndLine(&rewriterWrapper, &beginLineCallInstruction);

    IfFailRet(hr);

    AdjustBranchTargets(lineProbeFirstInstruction, prevInstruction->m_pNext, branchTargets);
    AdjustExceptionHandlingClauses(lineProbeFirstInstruction, prevInstruction->m_pNext, &rewriter);

    ILInstr* pStateLeaveToBeginLineOriginalMethodInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    // *** BeginMethod call catch
    ILInstr* beginLineCatchFirstInstr = rewriterWrapper.LoadLocalAddress(lineProbeCallTargetStateIndex);
    debuggerTokens->WriteLogException(&rewriterWrapper, &caller->type, /* isMethodProbe */ false);
    ILInstr* beginLineCatchLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    // *** BeginMethod exception handling clause
    EHClause beginLineExClause{};
    beginLineExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    beginLineExClause.m_pTryBegin = prevInstruction->m_pNext;
    beginLineExClause.m_pTryEnd = beginLineCatchFirstInstr;
    beginLineExClause.m_pHandlerBegin = beginLineCatchFirstInstr;
    beginLineExClause.m_pHandlerEnd = beginLineCatchLeaveInstr;
    beginLineExClause.m_ClassToken = debuggerTokens->GetExceptionTypeRef();

    ILInstr* beginOriginalLineInstr = rewriterWrapper.GetCurrentILInstr();
    pStateLeaveToBeginLineOriginalMethodInstr->m_pTarget = beginOriginalLineInstr;
    beginLineCatchLeaveInstr->m_pTarget = beginOriginalLineInstr;

    lineProbesEHClauses.emplace_back(beginLineExClause);
    return S_OK;
}

HRESULT DebuggerMethodRewriter::ApplyLineProbes(
    const int instrumentedMethodIndex,
    const LineProbeDefinitions& lineProbes, 
    CorProfiler* corProfiler, 
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
    std::vector<EHClause>& newClauses) const
{
    Logger::Info("Applying ", lineProbes.size(), " line probe(s) instrumentation.");

    const auto branchTargets = std::move(GetBranchTargets(&rewriter));

    for (const auto& lineProbe : lineProbes)
    {
        HRESULT hr = CallLineProbe(instrumentedMethodIndex, corProfiler, module_id, module_metadata, caller, debuggerTokens,
                           function_token, isStatic, methodArguments, numArgs, rewriter, methodLocals, numLocals,
                           rewriterWrapper, lineProbeCallTargetStateIndex, newClauses, branchTargets, lineProbe);
        if (FAILED(hr))
        {
            // Appropriate error message is already logged in CallLineProbe.
            return S_FALSE;
        }
    }

    return S_OK;
}

HRESULT DebuggerMethodRewriter::ApplyMethodProbe(
    CorProfiler* corProfiler, 
    ModuleID module_id, 
    ModuleMetadata& module_metadata, 
    FunctionInfo* caller, 
    DebuggerTokens* debuggerTokens, 
    mdToken function_token, 
    TypeSignature retFuncArg, 
    bool isVoid, 
    bool isStatic, 
    std::vector<TypeSignature> methodArguments, 
    int numArgs, 
    const shared::WSTRING& methodProbeId, 
    ILRewriter& rewriter, 
    std::vector<TypeSignature>& methodLocals, 
    int numLocals, 
    ILRewriterWrapper& rewriterWrapper, 
    ULONG callTargetStateIndex, 
    ULONG exceptionIndex, 
    ULONG callTargetReturnIndex, 
    ULONG returnValueIndex,
    mdToken callTargetReturnToken, 
    ILInstr* firstInstruction, 
    const int instrumentedMethodIndex, 
    ILInstr* const& beforeLineProbe,
    std::vector<EHClause>& newClauses) const
{
    rewriterWrapper.SetILPosition(beforeLineProbe);

    // ***
    // BEGIN METHOD PART
    // ***

    // Define ProbeId as string
    mdString methodProbeIdToken;
    HRESULT hr = module_metadata.metadata_emit->DefineUserString(methodProbeId.data(), static_cast<ULONG>(methodProbeId.length()), &methodProbeIdToken);

    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() DefineUserStringFailed. MethodProbeId = ", methodProbeId,
                     " module_id= ", module_id, ", functon_token=",
                     function_token);
        return hr;
    }

    rewriterWrapper.LoadStr(methodProbeIdToken);

    ILInstr* loadInstanceInstr;
    hr = LoadInstanceIntoStack(caller, isStatic, rewriterWrapper, &loadInstanceInstr);

    IfFailRet(hr);

    rewriterWrapper.LoadToken(function_token);
    rewriterWrapper.LoadToken(caller->type.id);
    rewriterWrapper.LoadInt32(instrumentedMethodIndex);

    // *** Emit BeginMethod call
    if (IsDebugEnabled())
    {
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
        //
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

    ILInstr* beginCallInstruction;
    hr = debuggerTokens->WriteBeginMethod_StartMarker(&rewriterWrapper, &caller->type, &beginCallInstruction);

    IfFailRet(hr);

    rewriterWrapper.StLocal(callTargetStateIndex);

    // *** Emit LogArg call(s)
    hr = WriteCallsToLogArg(corProfiler, debuggerTokens, isStatic, methodArguments, numArgs, rewriterWrapper,
                            callTargetStateIndex, &beginCallInstruction, /* isMethodProbe */ true);

    IfFailRet(hr);

    // Load the DebuggerState
    rewriterWrapper.LoadLocalAddress(callTargetStateIndex);
    hr = debuggerTokens->WriteBeginMethod_EndMarker(&rewriterWrapper, &beginCallInstruction);

    IfFailRet(hr);

    ILInstr* pStateLeaveToBeginOriginalMethodInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    // *** BeginMethod call catch
    ILInstr* beginMethodCatchFirstInstr = rewriterWrapper.LoadLocalAddress(callTargetStateIndex);
    debuggerTokens->WriteLogException(&rewriterWrapper, &caller->type, /* isMethodProbe */ true);
    ILInstr* beginMethodCatchLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    EHClause beginMethodExClause = {};
    beginMethodExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    beginMethodExClause.m_pTryBegin = firstInstruction;
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
    hr = LoadInstanceIntoStack(caller, isStatic, rewriterWrapper, &endMethodTryStartInstr);

    IfFailRet(hr);

    // *** Load the return value is is not void
    if (!isVoid)
    {
        rewriterWrapper.LoadLocal(returnValueIndex);
    }

    rewriterWrapper.LoadLocal(exceptionIndex);
    // Load the DebuggerState
    rewriterWrapper.LoadLocalAddress(callTargetStateIndex);

    ILInstr* endMethodCallInstr;
    if (isVoid)
    {
        debuggerTokens->WriteEndVoidReturnMemberRef(&rewriterWrapper, &caller->type, &endMethodCallInstr);
    }
    else
    {
        debuggerTokens->WriteEndReturnMemberRef(&rewriterWrapper, &caller->type, &retFuncArg, &endMethodCallInstr);
    }
    rewriterWrapper.StLocal(callTargetReturnIndex);

    // *** Emit LogLocal call(s)
    hr = WriteCallsToLogLocal(corProfiler, debuggerTokens, isStatic, methodLocals, numLocals, rewriterWrapper,
                              callTargetStateIndex, &endMethodCallInstr, /* isMethodProbe */ true);

    IfFailRet(hr);
    
    // *** Emit LogArg call(s)
    hr = WriteCallsToLogArg(corProfiler, debuggerTokens, isStatic, methodArguments, numArgs, rewriterWrapper,
                            callTargetStateIndex, &endMethodCallInstr, /* isMethodProbe */ true);

    IfFailRet(hr);

    // Load the DebuggerState
    rewriterWrapper.LoadLocalAddress(callTargetStateIndex);
    hr = debuggerTokens->WriteEndMethod_EndMarker(&rewriterWrapper, &endMethodCallInstr);

    IfFailRet(hr);

    if (!isVoid)
    {
        ILInstr* callTargetReturnGetReturnInstr;
        rewriterWrapper.LoadLocalAddress(callTargetReturnIndex);
        debuggerTokens->WriteCallTargetReturnGetReturnValue(&rewriterWrapper, callTargetReturnToken,
                                                            &callTargetReturnGetReturnInstr);
        rewriterWrapper.StLocal(returnValueIndex);
    }

    ILInstr* endMethodTryLeave = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    // *** EndMethod call catch

    // Load the DebuggerState
    ILInstr* endMethodCatchFirstInstr = rewriterWrapper.LoadLocalAddress(callTargetStateIndex);
    debuggerTokens->WriteLogException(&rewriterWrapper, &caller->type, /* isMethodProbe */ true);
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
        switch (pInstr->m_opcode)
        {
            case CEE_RET:
            {
                if (pInstr != methodReturnInstr)
                {
                    if (!isVoid)
                    {
                        rewriterWrapper.SetILPosition(pInstr);
                        rewriterWrapper.StLocal(returnValueIndex);
                    }
                    pInstr->m_opcode = CEE_LEAVE_S;
                    pInstr->m_pTarget = endFinallyInstr->m_pNext;
                }
                break;
            }
            default:
                break;
        }
    }

    EHClause exClause = {};
    exClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    exClause.m_pTryBegin = firstInstruction;
    exClause.m_pTryEnd = startExceptionCatch;
    exClause.m_pHandlerBegin = startExceptionCatch;
    exClause.m_pHandlerEnd = rethrowInstr;
    exClause.m_ClassToken = debuggerTokens->GetExceptionTypeRef();

    EHClause finallyClause = {};
    finallyClause.m_Flags = COR_ILEXCEPTION_CLAUSE_FINALLY;
    finallyClause.m_pTryBegin = firstInstruction;
    finallyClause.m_pTryEnd = rethrowInstr->m_pNext;
    finallyClause.m_pHandlerBegin = rethrowInstr->m_pNext;
    finallyClause.m_pHandlerEnd = endFinallyInstr;

    newClauses.push_back(beginMethodExClause);
    newClauses.push_back(endMethodExClause);
    newClauses.push_back(exClause);
    newClauses.push_back(finallyClause);

    return S_OK;
}

HRESULT DebuggerMethodRewriter::Rewrite(RejitHandlerModule* moduleHandler,
                                        RejitHandlerModuleMethod* methodHandler,
                                        MethodProbeDefinitions& methodProbes,
                                        LineProbeDefinitions& lineProbes) const
{
    auto corProfiler = trace::profiler;

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

    // First we check if the managed profiler has not been loaded yet
    if (!corProfiler->ProfilerAssemblyIsLoadedIntoAppDomain(module_metadata.app_domain_id))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() skipping method: The managed profiler has "
                     "not yet been loaded into AppDomain with id=",
                     module_metadata.app_domain_id, " token=", function_token, " caller_name=", caller->type.name, ".",
                     caller->name, "()");
        return S_FALSE;
    }

    // *** Create rewriter
    ILRewriter rewriter(corProfiler->info_, methodHandler->GetFunctionControl(), module_id, function_token);
    auto hr = rewriter.Import();
    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() Call to ILRewriter.Import() failed for ", module_id, " ",
                     function_token);
        return E_FAIL;
    }

    // *** Store the original il code text if the dump_il option is enabled.
    std::string original_code;
    if (IsDumpILRewriteEnabled())
    {
        original_code = corProfiler->GetILCodes("*** DebuggerMethodRewriter::Rewrite() Original Code: ", &rewriter,
                                                *caller, module_metadata.metadata_import);
    }

    // *** Get the locals signature.
    FunctionLocalSignature localSignature;
    hr = GetFunctionLocalSignature(module_metadata, rewriter, localSignature);

    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() failed to parse locals signature for ", module_id, " ",
                     function_token);
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
    hr = debuggerTokens->ModifyLocalSigAndInitialize(&rewriterWrapper, caller, &callTargetStateIndex, &exceptionIndex,
                                                     &callTargetReturnIndex, &returnValueIndex, &callTargetStateToken,
                                                     &exceptionToken, &callTargetReturnToken, &firstInstruction);

    if (FAILED(hr))
    {
        // Error message is already written in ModifyLocalSigAndInitialize
        return S_FALSE; // TODO https://datadoghq.atlassian.net/browse/DEBUG-706
    }

    ULONG lineProbeCallTargetStateIndex = static_cast<ULONG>(ULONG_MAX);
    mdToken lineProbeCallTargetStateToken = mdTokenNil;
    hr = debuggerTokens->GetDebuggerLocals(&rewriterWrapper, &lineProbeCallTargetStateIndex,
                                           &lineProbeCallTargetStateToken);

    if (FAILED(hr))
    {
        Logger::Error("Failed to get DebuggerLocals for ", module_id, " ", function_token);
        return E_FAIL;
    }

    const auto instrumentedMethodIndex = GetNextInstrumentedMethodIndex();
    std::vector<EHClause> newClauses;

    // ***
    // BEGIN LINE PROBES PART
    // ***

    const auto& beforeLineProbe = rewriterWrapper.GetCurrentILInstr();

    // TODO support multiple line probes & multiple line probes on the same bytecode offset (by deduplicating the probe ids)

    if (!lineProbes.empty())
    {
        hr = ApplyLineProbes(instrumentedMethodIndex, lineProbes, corProfiler, module_id, module_metadata, caller,
                            debuggerTokens, function_token, isStatic, methodArguments, numArgs, rewriter, methodLocals,
                            numLocals, rewriterWrapper, lineProbeCallTargetStateIndex, newClauses);
        if (FAILED(hr))
        {
            // Appropriate error message is already logged in ApplyLineProbes.
            return E_FAIL;
        }
    }

    // ***
    // BEGIN METHOD PROBE PART
    // ***

    if (!methodProbes.empty())
    {
        const auto& methodProbeId = methodProbes[0]->probeId; // TODO accept multiple probeIds

        Logger::Info("Applying Method Probe instrumentation with probeId.", methodProbeId);

        hr = ApplyMethodProbe(corProfiler, module_id, module_metadata, caller, debuggerTokens, function_token,
                              retFuncArg, isVoid, isStatic, methodArguments, numArgs, methodProbeId, rewriter,
                              methodLocals, numLocals, rewriterWrapper, callTargetStateIndex, exceptionIndex,
                              callTargetReturnIndex, returnValueIndex, callTargetReturnToken, firstInstruction,
                              instrumentedMethodIndex, beforeLineProbe, newClauses);
        if (FAILED(hr))
        {
            // Appropriate error message is already logged in ApplyMethodProbe.
            return E_FAIL;
        }
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

    std::sort(newClauses.begin(), newClauses.end(), 
        [](EHClause a, EHClause b) { return a.m_pTryBegin->m_offset < b.m_pTryBegin->m_offset; });

    for (auto ehClauseIndex = 0; ehClauseIndex < newClausesCount; ehClauseIndex++)
    {
        newEHClauses[ehCount - newClausesCount + ehClauseIndex] = newClauses[ehClauseIndex];
    }

    rewriter.SetEHClause(newEHClauses, ehCount);

    if (IsDumpILRewriteEnabled())
    {
        Logger::Info(original_code);
        Logger::Info(corProfiler->GetILCodes("*** Rewriter(): Modified Code: ", &rewriter, *caller,
                                             module_metadata.metadata_import));
    }

    hr = rewriter.Export();

    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite() Call to ILRewriter.Export() failed for "
                     "ModuleID=",
                     module_id, " ", function_token);
        return E_FAIL;
    }

    Logger::Info("*** DebuggerMethodRewriter::Rewrite() Finished: ", caller->type.name, ".", caller->name,
                 "() [IsVoid=", isVoid, ", IsStatic=", isStatic, ", Arguments=", numArgs, "]");
    return S_OK;
}

void DebuggerMethodRewriter::AdjustExceptionHandlingClauses(ILInstr* pFromInstr, ILInstr* pToInstr,
    ILRewriter* pRewriter)
{
    const auto& ehClauses = pRewriter->GetEHPointer();
    const auto ehCount = pRewriter->GetEHCount();

    for (unsigned ehIndex = 0; ehIndex < ehCount; ehIndex++)
    {
        if (ehClauses[ehIndex].m_pTryBegin == pFromInstr)
        {
            // TODO log
            ehClauses[ehIndex].m_pTryEnd = pToInstr;
        }

        if (ehClauses[ehIndex].m_pHandlerBegin == pFromInstr)
        {
            // TODO log
            ehClauses[ehIndex].m_pHandlerBegin = pToInstr;
        }

        if (ehClauses[ehIndex].m_pFilter == pFromInstr)
        {
            // TODO log
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