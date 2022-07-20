#include "debugger_method_rewriter.h"
#include "debugger_rejit_handler_module_method.h"
#include "cor_profiler.h"
#include "il_rewriter_wrapper.h"
#include "logger.h"
#include "stats.h"
#include "version.h"
#include "environment_variables_util.h"

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

HRESULT DebuggerMethodRewriter::LoadArgument(CorProfiler* corProfiler, bool isStatic, const ILRewriterWrapper& rewriterWrapper, int argumentIndex, const TypeSignature& argument)
{
    unsigned elementType;
    // Load the argument into the stack
    const auto& argTypeFlags = argument.GetTypeFlags(elementType);
    if (corProfiler->enable_by_ref_instrumentation)
    {
        if (argTypeFlags & TypeFlagByRef)
        {
            rewriterWrapper.LoadArgument(argumentIndex + (isStatic ? 0 : 1));
        }
        else
        {
            rewriterWrapper.LoadArgumentRef(argumentIndex + (isStatic ? 0 : 1));
        }
    }
    else
    {
        rewriterWrapper.LoadArgument(argumentIndex + (isStatic ? 0 : 1));
        if (argTypeFlags & TypeFlagByRef)
        {
            Logger::Warn("*** DebuggerMethodRewriter::Rewrite Methods with ref parameters "
                "cannot be instrumented. ");
            return E_FAIL;
        }
    }

    return S_OK;
}

HRESULT DebuggerMethodRewriter::LoadLocal(CorProfiler* corProfiler, const ILRewriterWrapper& rewriterWrapper, int localIndex, const TypeSignature& local)
{
    unsigned elementType;
    // Load the argument into the stack
    const auto& localTypeFlags = local.GetTypeFlags(elementType);
    if (corProfiler->enable_by_ref_instrumentation)
    {
        if (localTypeFlags & TypeFlagByRef)
        {
            rewriterWrapper.LoadLocal(localIndex);
        }
        else
        {
            rewriterWrapper.LoadLocalAddress(localIndex);
        }
    }
    else
    {
        rewriterWrapper.LoadLocal(localIndex);
        if (localTypeFlags & TypeFlagByRef)
        {
            Logger::Warn("*** DebuggerMethodRewriter::Rewrite Methods with ref parameters "
                         "cannot be instrumented. ");
            return E_FAIL;
        }
    }

    return S_OK;
}

void DebuggerMethodRewriter::LoadDebuggerState(bool enableDebuggerStateByRef, const ILRewriterWrapper& rewriterWrapper, ULONG callTargetStateIndex)
{
    if (enableDebuggerStateByRef)
    {
        rewriterWrapper.LoadLocalAddress(callTargetStateIndex);
    }
    else
    {
        rewriterWrapper.LoadLocal(callTargetStateIndex);
    }
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
    bool isArgs)
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
        LoadDebuggerState(corProfiler->enable_calltarget_state_by_ref, rewriterWrapper, callTargetStateIndex);

        if (isArgs)
        {
            hr = debuggerTokens->WriteLogArg(&rewriterWrapper, methodArgsOrLocals[argOrLocalIndex], beginCallInstruction);
        }
        else
        {
            hr = debuggerTokens->WriteLogLocal(&rewriterWrapper, methodArgsOrLocals[argOrLocalIndex], beginCallInstruction);
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
                                             int numArgs, ILRewriterWrapper& rewriterWrapper,
                                             ULONG callTargetStateIndex, ILInstr** beginCallInstruction)
{
    return WriteCallsToLogArgOrLocal(corProfiler, debuggerTokens, isStatic, args, numArgs,
                                rewriterWrapper, callTargetStateIndex, beginCallInstruction, true /*IsArgs*/);
}

HRESULT
DebuggerMethodRewriter::WriteCallsToLogLocal(CorProfiler* corProfiler, DebuggerTokens* debuggerTokens, bool isStatic,
                                             const std::vector<TypeSignature>& locals,
                                             int numLocals, ILRewriterWrapper& rewriterWrapper,
                                             ULONG callTargetStateIndex, ILInstr** beginCallInstruction)
{
    return WriteCallsToLogArgOrLocal(corProfiler, debuggerTokens, isStatic, locals, numLocals,
                                rewriterWrapper, callTargetStateIndex, beginCallInstruction, false /*IsArgs*/);
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
            Logger::Warn("*** DebuggerMethodRewriter::Rewrite Static methods in a ValueType cannot be instrumented. ");
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
    auto debuggerMethodHandler = dynamic_cast<DebuggerRejitHandlerModuleMethod*>(methodHandler);

    if (debuggerMethodHandler->GetMethodProbeDefinition() == nullptr)
    {
        Logger::Warn("NotifyReJITCompilationStarted: MethodProbeDefinition is missing for "
                     "MethodDef: ",
                     methodHandler->GetMethodDef());

        return S_FALSE;
    }

    auto _ = trace::Stats::Instance()->CallTargetRewriterCallbackMeasure();

    auto corProfiler = trace::profiler;

    ModuleID module_id = moduleHandler->GetModuleId();
    ModuleMetadata& module_metadata = *moduleHandler->GetModuleMetadata();
    FunctionInfo* caller = methodHandler->GetFunctionInfo();
    DebuggerTokens* debuggerTokens = module_metadata.GetDebuggerTokens();
    mdToken function_token = caller->id;
    TypeSignature retFuncArg = caller->method_signature.GetRet();
    MethodProbeDefinition* integration_definition = debuggerMethodHandler->GetMethodProbeDefinition();
    unsigned int retFuncElementType;
    int retTypeFlags = retFuncArg.GetTypeFlags(retFuncElementType);
    bool isVoid = (retTypeFlags & TypeFlagVoid) > 0;
    bool isStatic = !(caller->method_signature.CallingConvention() & IMAGE_CEE_CS_CALLCONV_HASTHIS);
    std::vector<TypeSignature> methodArguments = caller->method_signature.GetMethodArguments();
    int numArgs = caller->method_signature.NumberOfArguments();

    // First we check if the managed profiler has not been loaded yet
    if (!corProfiler->ProfilerAssemblyIsLoadedIntoAppDomain(module_metadata.app_domain_id))
    {
        Logger::Warn(
            "*** CallTarget_RewriterCallback() skipping method: The managed profiler has "
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
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite Call to ILRewriter.Import() failed for ", module_id, " ",
                     function_token);
        return S_FALSE;
    }

    // *** Store the original il code text if the dump_il option is enabled.
    std::string original_code;
    if (IsDumpILRewriteEnabled())
    {
        original_code = corProfiler->GetILCodes("*** DebuggerMethodRewriter::Rewrite Original Code: ", &rewriter,
                                                *caller, module_metadata);
    }

    // *** Get the locals signature.
    FunctionLocalSignature localSignature;
    hr = GetFunctionLocalSignature(module_metadata, rewriter, localSignature);

    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite failed to parse locals signature for ", module_id, " ",
                     function_token);
        return S_FALSE;
    }

    std::vector<TypeSignature> methodLocals = localSignature.GetMethodLocals();
    int numLocals = localSignature.NumberOfLocals();

    if (trace::Logger::IsDebugEnabled())
    {
        Logger::Debug("*** CallTarget_RewriterCallback() Start: ", caller->type.name, ".", caller->name,
                      "() [IsVoid=", isVoid, ", IsStatic=", isStatic,
                      ", MethodProbeMethodName=", integration_definition->target_method.method_name,
                      ", Arguments=", numArgs, "]");
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
        return S_FALSE;
    }

    // ***
    // BEGIN METHOD PART
    // ***

    ILInstr* loadInstanceInstr;
    hr = LoadInstanceIntoStack(caller, isStatic, rewriterWrapper, &loadInstanceInstr);

    if (FAILED(hr))
    {
        // Error message is already written in LoadInstanceIntoStack
        return S_FALSE;
    }

    // *** Emit BeginMethod call
    if (IsDebugEnabled())
    {
        Logger::Debug("Caller Type.Id: ", HexStr(&caller->type.id, sizeof(mdToken)));
        Logger::Debug("Caller Type.IsGeneric: ", caller->type.isGeneric);
        Logger::Debug("Caller Type.IsValid: ", caller->type.IsValid());
        Logger::Debug("Caller Type.Name: ", caller->type.name);
        Logger::Debug("Caller Type.TokenType: ", caller->type.token_type);
        Logger::Debug("Caller Type.Spec: ", HexStr(&caller->type.type_spec, sizeof(mdTypeSpec)));
        Logger::Debug("Caller Type.ValueType: ", caller->type.valueType);
        //
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
    if (FAILED(hr))
    {
        // Error message is written to the log in WriteBeginMethod_StartMarker.
        return S_FALSE;
    }
    rewriterWrapper.StLocal(callTargetStateIndex);

    // *** Emit LogArg call(s)
    hr = WriteCallsToLogArg(corProfiler, debuggerTokens, isStatic, methodArguments, numArgs, rewriterWrapper, callTargetStateIndex, &beginCallInstruction);

    if (FAILED(hr))
    {
        // Error message is written to the log in WriteCallsToLogArg.
        return S_FALSE;
    }

    // Load the DebuggerState
    LoadDebuggerState(corProfiler->enable_calltarget_state_by_ref, rewriterWrapper, callTargetStateIndex);
    hr = debuggerTokens->WriteBeginMethod_EndMarker(&rewriterWrapper, &beginCallInstruction);
    if (FAILED(hr))
    {
        // Error message is written to the log in WriteBeginMethod_EndMarker.
        return S_FALSE;
    }

    ILInstr* pStateLeaveToBeginOriginalMethodInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    // *** BeginMethod call catch
    ILInstr* beginMethodCatchFirstInstr = nullptr;
    debuggerTokens->WriteLogException(&rewriterWrapper, &caller->type, &beginMethodCatchFirstInstr);
    ILInstr* beginMethodCatchLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    // *** BeginMethod exception handling clause
    EHClause beginMethodExClause{};
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

    if (FAILED(hr))
    {
        // Error message is already written in LoadInstanceIntoStack
        return S_FALSE;
    }

    // *** Load the return value is is not void
    if (!isVoid)
    {
        rewriterWrapper.LoadLocal(returnValueIndex);
    }

    rewriterWrapper.LoadLocal(exceptionIndex);
    LoadDebuggerState(corProfiler->enable_calltarget_state_by_ref, rewriterWrapper, callTargetStateIndex);

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

    // *** Emit LogArg call(s)
    hr = WriteCallsToLogArg(corProfiler, debuggerTokens, isStatic, methodArguments, numArgs, rewriterWrapper,
                      callTargetStateIndex, &endMethodCallInstr);

    // *** Emit LogLocal call(s)
    hr = WriteCallsToLogLocal(corProfiler, debuggerTokens, isStatic, methodLocals, numLocals, rewriterWrapper,
                        callTargetStateIndex, &endMethodCallInstr);

    // Load the DebuggerState
    LoadDebuggerState(corProfiler->enable_calltarget_state_by_ref, rewriterWrapper, callTargetStateIndex);
    hr = debuggerTokens->WriteEndMethod_EndMarker(&rewriterWrapper, &endMethodCallInstr);
    if (FAILED(hr))
    {
        // Error message is written to the log in WriteBeginMethod_EndMarker.
        return S_FALSE;
    }

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
    ILInstr* endMethodCatchFirstInstr = nullptr;
    debuggerTokens->WriteLogException(&rewriterWrapper, &caller->type, &endMethodCatchFirstInstr);
    ILInstr* endMethodCatchLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    // *** EndMethod exception handling clause
    EHClause endMethodExClause{};
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

    // Exception handling clauses
    EHClause exClause{};
    exClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    exClause.m_pTryBegin = firstInstruction;
    exClause.m_pTryEnd = startExceptionCatch;
    exClause.m_pHandlerBegin = startExceptionCatch;
    exClause.m_pHandlerEnd = rethrowInstr;
    exClause.m_ClassToken = debuggerTokens->GetExceptionTypeRef();

    EHClause finallyClause{};
    finallyClause.m_Flags = COR_ILEXCEPTION_CLAUSE_FINALLY;
    finallyClause.m_pTryBegin = firstInstruction;
    finallyClause.m_pTryEnd = rethrowInstr->m_pNext;
    finallyClause.m_pHandlerBegin = rethrowInstr->m_pNext;
    finallyClause.m_pHandlerEnd = endFinallyInstr;

    // ***
    // Update and Add exception clauses
    // ***
    auto ehCount = rewriter.GetEHCount();
    auto ehPointer = rewriter.GetEHPointer();
    auto newEHClauses = new EHClause[ehCount + 4];
    for (unsigned i = 0; i < ehCount; i++)
    {
        newEHClauses[i] = ehPointer[i];
    }

    // *** Add the new EH clauses
    ehCount += 4;
    newEHClauses[ehCount - 4] = beginMethodExClause;
    newEHClauses[ehCount - 3] = endMethodExClause;
    newEHClauses[ehCount - 2] = exClause;
    newEHClauses[ehCount - 1] = finallyClause;
    rewriter.SetEHClause(newEHClauses, ehCount);

    if (IsDumpILRewriteEnabled())
    {
        Logger::Info(original_code);
        Logger::Info(corProfiler->GetILCodes("*** Rewriter(): Modified Code: ", &rewriter, *caller, module_metadata));
    }

    hr = rewriter.Export();

    if (FAILED(hr))
    {
        Logger::Warn("*** DebuggerMethodRewriter::Rewrite Call to ILRewriter.Export() failed for "
                     "ModuleID=",
                     module_id, " ", function_token);
        return S_FALSE;
    }

    Logger::Info("*** CallTarget_RewriterCallback() Finished: ", caller->type.name, ".", caller->name,
                 "() [IsVoid=", isVoid, ", IsStatic=", isStatic,
                 ", Arguments=", numArgs, "]");
    return S_OK;
}

} // namespace debugger