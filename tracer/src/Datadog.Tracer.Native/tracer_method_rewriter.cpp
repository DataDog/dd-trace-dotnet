#include "tracer_method_rewriter.h"
#include "cor_profiler.h"
#include "il_rewriter_wrapper.h"
#include "integration.h"
#include "logger.h"
#include "stats.h"
#include "../../../shared/src/native-src/version.h"
#include "environment_variables_util.h"
#include "dd_profiler_constants.h"
#include "tracer_handler_module_method.h"

namespace trace
{
/// <summary>
/// Rewrite the target method body with the calltarget implementation. (This is function is triggered by the ReJIT
/// handler) Resulting code structure:
///
/// - Add locals for TReturn (if non-void method), CallTargetState, CallTargetReturn/CallTargetReturn<TReturn>,
/// Exception
/// - Initialize locals
///
/// try
/// {
///   try
///   {
///     try
///     {
///       - Invoke BeginMethod with object instance (or null if static method) and original method arguments
///       - Store result into CallTargetState local
///     }
///     catch when (CallTargetBubbleUpException.IsCallTargetBubbleUpException(exception) == false)
///     {
///       - Invoke LogException(Exception)
///     }
///
///     - Execute original method instructions
///       * All RET instructions are replaced with a LEAVE_S. If non-void method, the value on the stack is first stored
///       in the TReturn local.
///   }
///   catch (Exception)
///   {
///     - Store exception into Exception local
///     - throw
///   }
/// }
/// finally
/// {
///   try
///   {
///     - Invoke EndMethod with object instance (or null if static method), TReturn local (if non-void method),
///     CallTargetState local, and Exception local
///     - Store result into CallTargetReturn/CallTargetReturn<TReturn> local
///     - If non-void method, store CallTargetReturn<TReturn>.GetReturnValue() into TReturn local
///   }
///     catch when (CallTargetBubbleUpException.IsCallTargetBubbleUpException(exception) == false)
///   {
///     - Invoke LogException(Exception)
///   }
/// }
///
/// - If non-void method, load TReturn local
/// - RET
/// </summary>
/// <param name="moduleHandler">Module ReJIT handler representation</param>
/// <param name="methodHandler">Method ReJIT handler representation</param>
/// <returns>Result of the rewriting</returns>
HRESULT TracerMethodRewriter::Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler,
                                      ICorProfilerFunctionControl* pFunctionControl, ICorProfilerInfo* corProfilerInfo)
{
    /*  ===============================
        Current CallTarget Limitations:
        ===============================

        1. Static methods in a ValueType (struct) cannot be instrumented.
        2. Generic ValueTypes (struct) cannot be instrumented.
        3. Nested ValueTypes (struct) inside a Generic parent type will not expose the type instance (the instance will
       be always null).
        4. Nested types (reference types) inside a Generic parent type will not expose the type instance (the instance
       will be casted as an object type).
        5. Methods in a Generic type will not expose the Generic type instance (the instance will be casted as a non
       generic base type or object type).
    */

    if (methodHandler == nullptr)
    {
        Logger::Error("TracerMethodRewriter::Rewrite: methodHandler is null.");
        return S_FALSE;
    }

    auto tracerMethodHandler = static_cast<TracerRejitHandlerModuleMethod*>(methodHandler);
    if (tracerMethodHandler->GetIntegrationDefinition() == nullptr)
    {
        Logger::Warn("TracerMethodRewriter::Rewrite: IntegrationDefinition is missing for "
                     "MethodDef: ",
                     methodHandler->GetMethodDef());

        return S_FALSE;
    }

    if (!tracerMethodHandler->GetIntegrationDefinition()->GetEnabled())
    {
        return S_FALSE;
    }

    auto _ = trace::Stats::Instance()->CallTargetRewriterCallbackMeasure();

    ModuleID module_id = moduleHandler->GetModuleId();
    ModuleMetadata& module_metadata = *moduleHandler->GetModuleMetadata();
    FunctionInfo* caller = methodHandler->GetFunctionInfo();
    TracerTokens* tracerTokens = module_metadata.GetTracerTokens();
    tracerTokens->SetCorProfilerInfo(m_corProfiler->info_);
    mdToken function_token = caller->id;
    TypeSignature retFuncArg = caller->method_signature.GetReturnValue();
    IntegrationDefinition* integration_definition = tracerMethodHandler->GetIntegrationDefinition();
    bool is_integration_method =
        integration_definition->target_method.type.assembly.name != tracemethodintegration_assemblyname;
    bool ignoreByRefInstrumentation = !is_integration_method;
    const auto [retFuncElementType, retTypeFlags] = retFuncArg.GetElementTypeAndFlags();
    bool isVoid = (retTypeFlags & TypeFlagVoid) > 0;
    bool isStatic = !(caller->method_signature.CallingConvention() & IMAGE_CEE_CS_CALLCONV_HASTHIS);
    std::vector<trace::TypeSignature> methodArguments = caller->method_signature.GetMethodArguments();
    std::vector<trace::TypeSignature> traceAnnotationArguments;

    // DO NOT move the definition of these buffers into an inner scope. It will cause memory corruption since they are
    // referenced in a TypeSignature that is used later in this function.
    COR_SIGNATURE runtimeMethodHandleBuffer[10];
    COR_SIGNATURE runtimeTypeHandleBuffer[10];

    int numArgs = caller->method_signature.NumberOfArguments();
    auto metaEmit = module_metadata.metadata_emit;
    auto metaImport = module_metadata.metadata_import;

    // *** Get reference to the integration type
    mdTypeRef integration_type_ref = mdTypeRefNil;
    if (!m_corProfiler->GetIntegrationTypeRef(module_metadata, module_id, *integration_definition,
                                              integration_type_ref))
    {
        Logger::Warn("*** CallTarget_RewriterCallback() skipping method: Integration Type Ref cannot be found for ",
                     " token=", function_token, " caller_name=", caller->type.name, ".", caller->name, "()");
        return S_FALSE;
    }

    if (trace::Logger::IsDebugEnabled())
    {
        Logger::Debug("*** CallTarget_RewriterCallback() Start: ", caller->type.name, ".", caller->name,
                      "() [IsVoid=", isVoid, ", IsStatic=", isStatic,
                      ", IntegrationType=", integration_definition->integration_type.name, ", Arguments=", numArgs,
                      "]");
    }

    // First we check if the managed profiler has not been loaded yet
    if (!m_corProfiler->ProfilerAssemblyIsLoadedIntoAppDomain(module_metadata.app_domain_id))
    {
        Logger::Warn(
            "*** CallTarget_RewriterCallback() skipping method: Method replacement found but the managed profiler has "
            "not yet been loaded into AppDomain with id=",
            module_metadata.app_domain_id, " token=", function_token, " caller_name=", caller->type.name, ".",
            caller->name, "()");
        return S_FALSE;
    }

    // *** Create rewriter
    ILRewriter rewriter(corProfilerInfo, pFunctionControl, module_id, function_token);
    bool modified = false;
    auto hr = rewriter.Import();
    if (FAILED(hr))
    {
        Logger::Warn("*** CallTarget_RewriterCallback(): Call to ILRewriter.Import() failed for ", module_id, " ",
                     function_token);
        return S_FALSE;
    }

    // *** Store the original il code text if the dump_il option is enabled.
    std::string original_code;
    if (IsDumpILRewriteEnabled())
    {
        original_code = m_corProfiler->GetILCodes("*** CallTarget_RewriterCallback(): Original Code: ", &rewriter,
                                                  *caller, module_metadata.metadata_import);
    }

    // *** Create the rewriter wrapper helper
    ILRewriterWrapper reWriterWrapper(&rewriter);
    reWriterWrapper.SetILPosition(rewriter.GetILList()->m_pNext);

    // *** Modify the Local Var Signature of the method and initialize the new local vars
    ULONG callTargetStateIndex = static_cast<ULONG>(ULONG_MAX);
    ULONG exceptionIndex = static_cast<ULONG>(ULONG_MAX);
    ULONG callTargetReturnIndex = static_cast<ULONG>(ULONG_MAX);
    ULONG returnValueIndex = static_cast<ULONG>(ULONG_MAX);

    std::vector<ULONG> indexes(tracerTokens->GetAdditionalLocalsCount(methodArguments));
    mdToken callTargetStateToken = mdTokenNil;
    mdToken exceptionToken = mdTokenNil;
    mdToken callTargetReturnToken = mdTokenNil;
    ILInstr* firstInstruction = nullptr;
    auto returnType = caller->method_signature.GetReturnValue();

    tracerTokens->ModifyLocalSigAndInitialize(
        &reWriterWrapper, &returnType, &methodArguments, &callTargetStateIndex, &exceptionIndex, &callTargetReturnIndex,
        &returnValueIndex, &callTargetStateToken, &exceptionToken, &callTargetReturnToken, &firstInstruction, indexes);

    ULONG exceptionValueIndex = indexes[0];
    ULONG exceptionValueEndIndex = indexes[1];
    const auto refStructIndexes = indexes.data() + 2;

    // ***
    // BEGIN METHOD PART
    // ***

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
            Logger::Warn("*** CallTarget_RewriterCallback(): Static methods in a ValueType cannot be instrumented. ");
            return S_FALSE;
        }
        reWriterWrapper.LoadNull();
    }
    else
    {
        bool callerTypeIsValueType = caller->type.valueType;
        mdToken callerTypeToken = tracerTokens->GetCurrentTypeRef(&caller->type, callerTypeIsValueType);
        if (callerTypeToken == mdTokenNil)
        {
            reWriterWrapper.LoadNull();
        }
        else
        {
            reWriterWrapper.LoadArgument(0);

            if (caller->type.valueType)
            {
                if (caller->type.type_spec != mdTypeSpecNil)
                {
                    reWriterWrapper.LoadObj(caller->type.type_spec);
                }
                else if (!caller->type.isGeneric)
                {
                    reWriterWrapper.LoadObj(caller->type.id);
                }
                else
                {
                    // Generic struct instrumentation is not supported
                    // IMetaDataImport::GetMemberProps and IMetaDataImport::GetMemberRefProps returns
                    // The parent token as mdTypeDef and not as a mdTypeSpec
                    // that's because the method definition is stored in the mdTypeDef
                    // The problem is that we don't have the exact Spec of that generic
                    // We can't emit LoadObj or Box because that would result in an invalid IL.
                    // This problem doesn't occur on a class type because we can always relay in the
                    // object type.
                    return S_FALSE;
                }
            }
        }
    }

    // *** Load the method arguments to the stack
    if (is_integration_method)
    {
        int structRefCount = 0;
        if (numArgs < FASTPATH_COUNT)
        {
            // Load the arguments directly (FastPath)
            for (int i = 0; i < numArgs; i++)
            {
                const auto [elementType, argTypeFlags] = methodArguments[i].GetElementTypeAndFlags();
                if (m_corProfiler->enable_by_ref_instrumentation)
                {
                    if (argTypeFlags & TypeFlagByRef)
                    {
                        reWriterWrapper.LoadArgument(i + (isStatic ? 0 : 1));
                    }
                    else
                    {
                        reWriterWrapper.LoadArgumentRef(i + (isStatic ? 0 : 1));
                    }

                    bool isByRefLike = false;
                    if (SUCCEEDED(IsTypeByRefLike(m_corProfiler->info_, module_metadata, methodArguments[i],
                                                  tracerTokens->GetCorLibAssemblyRef(), isByRefLike)) &&
                        isByRefLike)
                    {
                        const auto& argumentToken =
                            methodArguments[i].GetTypeTok(metaEmit, tracerTokens->GetCorLibAssemblyRef());
                        tracerTokens->WriteRefStructCall(&reWriterWrapper, argumentToken,
                                                         refStructIndexes[structRefCount++]);
                    }
                }
                else
                {
                    reWriterWrapper.LoadArgument(i + (isStatic ? 0 : 1));
                    if (argTypeFlags & TypeFlagByRef)
                    {
                        Logger::Warn("*** CallTarget_RewriterCallback(): Methods with ref parameters "
                                     "cannot be instrumented. ");
                        return S_FALSE;
                    }
                }
            }
        }
        else
        {
            // Load the arguments inside an object array (SlowPath)
            reWriterWrapper.CreateArray(tracerTokens->GetObjectTypeRef(), numArgs);
            for (int i = 0; i < numArgs; i++)
            {
                reWriterWrapper.BeginLoadValueIntoArray(i);
                reWriterWrapper.LoadArgument(i + (isStatic ? 0 : 1));
                const auto [elementType, argTypeFlags] = methodArguments[i].GetElementTypeAndFlags();
                if (argTypeFlags & TypeFlagByRef || argTypeFlags & TypeFlagPinnedType)
                {
                    Logger::Warn("*** CallTarget_RewriterCallback(): Methods with ref parameters or pinned locals"
                                 "cannot be instrumented. ");
                    return S_FALSE;
                }
                if (argTypeFlags & TypeFlagBoxedType)
                {
                    const auto& tok = methodArguments[i].GetTypeTok(metaEmit, tracerTokens->GetCorLibAssemblyRef());
                    if (tok == mdTokenNil)
                    {
                        return S_FALSE;
                    }
                    reWriterWrapper.Box(tok);
                }
                reWriterWrapper.EndLoadValueIntoArray();
            }
        }
    }
    else
    {
        // Load the methodDef token to produce a RuntimeMethodHandle on the stack
        reWriterWrapper.LoadToken(caller->id);

        runtimeMethodHandleBuffer[0] = ELEMENT_TYPE_VALUETYPE;
        ULONG runtimeMethodHandleTokenLength =
            CorSigCompressToken(tracerTokens->GetRuntimeMethodHandleTypeRef(), &runtimeMethodHandleBuffer[1]);

        // Load the typeDef token to produce a RuntimeTypeHandle on the stack
        reWriterWrapper.LoadToken(caller->type.id);

        runtimeTypeHandleBuffer[0] = ELEMENT_TYPE_VALUETYPE;
        ULONG runtimeTypeHandleTokenLength =
            CorSigCompressToken(tracerTokens->GetRuntimeTypeHandleTypeRef(), &runtimeTypeHandleBuffer[1]);

        // Replace method arguments with one RuntimeMethodHandle argument and one RuntimeTypeHandle argument
        trace::TypeSignature runtimeMethodHandleArgument{};
        runtimeMethodHandleArgument.pbBase = runtimeMethodHandleBuffer;
        runtimeMethodHandleArgument.length = runtimeMethodHandleTokenLength + 1;
        runtimeMethodHandleArgument.offset = 0;
        traceAnnotationArguments.push_back(runtimeMethodHandleArgument);

        trace::TypeSignature runtimeTypeHandleArgument{};
        runtimeTypeHandleArgument.pbBase = runtimeTypeHandleBuffer;
        runtimeTypeHandleArgument.length = runtimeTypeHandleTokenLength + 1;
        runtimeTypeHandleArgument.offset = 0;
        traceAnnotationArguments.push_back(runtimeTypeHandleArgument);

        methodArguments = traceAnnotationArguments;
    }

    // *** Emit BeginMethod call
    if (IsDebugEnabled())
    {
        Logger::Debug("Caller Type.Id: ", shared::HexStr(&caller->type.id, sizeof(mdToken)));
        Logger::Debug("Caller Type.IsGeneric: ", caller->type.isGeneric);
        Logger::Debug("Caller Type.IsValid: ", caller->type.IsValid());
        Logger::Debug("Caller Type.Name: ", caller->type.name);
        Logger::Debug("Caller Type.TokenType: ", caller->type.token_type);
        Logger::Debug("Caller Type.Spec: ", shared::HexStr(&caller->type.type_spec, sizeof(mdTypeSpec)));
        Logger::Debug("Caller Type.ValueType: ", caller->type.valueType);
        //
        if (caller->type.extend_from != nullptr)
        {
            Logger::Debug("Caller Type Extend From.Id: ",
                          shared::HexStr(&caller->type.extend_from->id, sizeof(mdToken)));
            Logger::Debug("Caller Type Extend From.IsGeneric: ", caller->type.extend_from->isGeneric);
            Logger::Debug("Caller Type Extend From.IsValid: ", caller->type.extend_from->IsValid());
            Logger::Debug("Caller Type Extend From.Name: ", caller->type.extend_from->name);
            Logger::Debug("Caller Type Extend From.TokenType: ", caller->type.extend_from->token_type);
            Logger::Debug("Caller Type Extend From.Spec: ",
                          shared::HexStr(&caller->type.extend_from->type_spec, sizeof(mdTypeSpec)));
            Logger::Debug("Caller Type Extend From.ValueType: ", caller->type.extend_from->valueType);
        }
        //
        if (caller->type.parent_type != nullptr)
        {
            Logger::Debug("Caller ParentType.Id: ", shared::HexStr(&caller->type.parent_type->id, sizeof(mdToken)));
            Logger::Debug("Caller ParentType.IsGeneric: ", caller->type.parent_type->isGeneric);
            Logger::Debug("Caller ParentType.IsValid: ", caller->type.parent_type->IsValid());
            Logger::Debug("Caller ParentType.Name: ", caller->type.parent_type->name);
            Logger::Debug("Caller ParentType.TokenType: ", caller->type.parent_type->token_type);
            Logger::Debug("Caller ParentType.Spec: ",
                          shared::HexStr(&caller->type.parent_type->type_spec, sizeof(mdTypeSpec)));
            Logger::Debug("Caller ParentType.ValueType: ", caller->type.parent_type->valueType);
        }
    }

    ILInstr* beginCallInstruction;
    hr = tracerTokens->WriteBeginMethod(&reWriterWrapper, integration_type_ref, &caller->type, methodArguments,
                                        ignoreByRefInstrumentation, &beginCallInstruction);
    if (FAILED(hr))
    {
        // Error message is written to the log in WriteBeginMethod.
        return S_FALSE;
    }
    reWriterWrapper.StLocal(callTargetStateIndex);
    ILInstr* pStateLeaveToBeginOriginalMethodInstr = reWriterWrapper.CreateInstr(CEE_LEAVE_S);

    // *** Filter exception
    ILInstr* filter = nullptr;
    ILInstr* beginMethodCatchFirstInstr = nullptr;
    mdTypeRef bubbleup_exception_typeref = tracerTokens->GetBubbleUpExceptionTypeRef();
    if (m_corProfiler->call_target_bubble_up_exception_available && bubbleup_exception_typeref != mdTypeRefNil)
    {
        filter = CreateFilterForException(&reWriterWrapper, tracerTokens->GetExceptionTypeRef(),
                                          tracerTokens->GetBubbleUpExceptionTypeRef(),
                                          tracerTokens->GetBubbleUpExceptionFunctionDef(), exceptionValueIndex);
        Logger::Debug("Creating filter for try / catch for CallTargetBubbleUpException. (begin method)");
        beginMethodCatchFirstInstr = reWriterWrapper.Pop();
        reWriterWrapper.LoadLocal(exceptionValueIndex);
    }
    // *** BeginMethod call catch
    tracerTokens->WriteLogException(&reWriterWrapper, integration_type_ref, &caller->type, &beginMethodCatchFirstInstr);
    ILInstr* beginMethodCatchLeaveInstr = reWriterWrapper.CreateInstr(CEE_LEAVE_S);

    // *** BeginMethod exception handling clause
    EHClause beginMethodExClause{};
    if (filter != nullptr)
    {
        beginMethodExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_FILTER;
        beginMethodExClause.m_pTryBegin = firstInstruction;
        beginMethodExClause.m_pTryEnd = filter;
        beginMethodExClause.m_pHandlerBegin = beginMethodCatchFirstInstr;
        beginMethodExClause.m_pHandlerEnd = beginMethodCatchLeaveInstr;
        beginMethodExClause.m_pFilter = filter;
    }
    else
    {
        beginMethodExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
        beginMethodExClause.m_pTryBegin = firstInstruction;
        beginMethodExClause.m_pTryEnd = beginMethodCatchFirstInstr;
        beginMethodExClause.m_pHandlerBegin = beginMethodCatchFirstInstr;
        beginMethodExClause.m_pHandlerEnd = beginMethodCatchLeaveInstr;
        beginMethodExClause.m_ClassToken = tracerTokens->GetExceptionTypeRef();
    }

    // ***
    // METHOD EXECUTION
    // ***
    ILInstr* beginOriginalMethodInstr = reWriterWrapper.GetCurrentILInstr();
    pStateLeaveToBeginOriginalMethodInstr->m_pTarget = beginOriginalMethodInstr;
    beginMethodCatchLeaveInstr->m_pTarget = beginOriginalMethodInstr;

    // ***
    // ENDING OF THE METHOD EXECUTION
    // ***

    // *** Create return instruction and insert it at the end
    ILInstr* methodReturnInstr = rewriter.NewILInstr();
    methodReturnInstr->m_opcode = CEE_RET;
    rewriter.InsertAfter(rewriter.GetILList()->m_pPrev, methodReturnInstr);
    reWriterWrapper.SetILPosition(methodReturnInstr);

    // ***
    // EXCEPTION CATCH
    // ***
    ILInstr* startExceptionCatch = reWriterWrapper.StLocal(exceptionIndex);
    reWriterWrapper.SetILPosition(methodReturnInstr);
    ILInstr* rethrowInstr = reWriterWrapper.Rethrow();

    // ***
    // EXCEPTION FINALLY / END METHOD PART
    // ***
    ILInstr* endMethodTryStartInstr;

    // *** Load instance into the stack (if not static)
    if (isStatic)
    {
        if (caller->type.valueType)
        {
            // Static methods in a ValueType can't be instrumented.
            // In the future this can be supported by adding a local for the valuetype
            // and initialize it to the default value. After the signature
            // modification we need to emit the following IL to initialize and load
            // into the stack.
            //    ldloca.s [localIndex]
            //    initobj [valueType]
            //    ldloc.s [localIndex]
            Logger::Warn("CallTarget_RewriterCallback: Static methods in a ValueType cannot "
                         "be instrumented. ");
            return S_FALSE;
        }
        endMethodTryStartInstr = reWriterWrapper.LoadNull();
    }
    else
    {
        bool callerTypeIsValueType = caller->type.valueType;
        mdToken callerTypeToken = tracerTokens->GetCurrentTypeRef(&caller->type, callerTypeIsValueType);
        if (callerTypeToken == mdTokenNil)
        {
            endMethodTryStartInstr = reWriterWrapper.LoadNull();
        }
        else
        {
            endMethodTryStartInstr = reWriterWrapper.LoadArgument(0);

            if (caller->type.valueType)
            {
                if (caller->type.type_spec != mdTypeSpecNil)
                {
                    reWriterWrapper.LoadObj(caller->type.type_spec);
                }
                else if (!caller->type.isGeneric)
                {
                    reWriterWrapper.LoadObj(caller->type.id);
                }
                else
                {
                    // Generic struct instrumentation is not supported
                    // IMetaDataImport::GetMemberProps and IMetaDataImport::GetMemberRefProps returns
                    // The parent token as mdTypeDef and not as a mdTypeSpec
                    // that's because the method definition is stored in the mdTypeDef
                    // The problem is that we don't have the exact Spec of that generic
                    // We can't emit LoadObj or Box because that would result in an invalid IL.
                    // This problem doesn't occur on a class type because we can always relay in the
                    // object type.
                    return S_FALSE;
                }
            }
        }
    }

    // *** Load the return value is is not void
    if (!isVoid)
    {
        reWriterWrapper.LoadLocal(returnValueIndex);
    }

    reWriterWrapper.LoadLocal(exceptionIndex);
    if (m_corProfiler->enable_calltarget_state_by_ref)
    {
        reWriterWrapper.LoadLocalAddress(callTargetStateIndex);
    }
    else
    {
        reWriterWrapper.LoadLocal(callTargetStateIndex);
    }

    ILInstr* endMethodCallInstr;
    if (isVoid)
    {
        tracerTokens->WriteEndVoidReturnMemberRef(&reWriterWrapper, integration_type_ref, &caller->type,
                                                  &endMethodCallInstr);
    }
    else
    {
        tracerTokens->WriteEndReturnMemberRef(&reWriterWrapper, integration_type_ref, &caller->type, &retFuncArg,
                                              &endMethodCallInstr);
    }
    reWriterWrapper.StLocal(callTargetReturnIndex);

    if (!isVoid)
    {
        ILInstr* callTargetReturnGetReturnInstr;
        reWriterWrapper.LoadLocalAddress(callTargetReturnIndex);
        tracerTokens->WriteCallTargetReturnGetReturnValue(&reWriterWrapper, callTargetReturnToken,
                                                          &callTargetReturnGetReturnInstr);
        reWriterWrapper.StLocal(returnValueIndex);
    }

    ILInstr* endMethodTryLeave = reWriterWrapper.CreateInstr(CEE_LEAVE_S);

    ILInstr* filterEnd = nullptr;
    ILInstr* endMethodCatchFirstInstr = nullptr;
    if (m_corProfiler->call_target_bubble_up_exception_available && bubbleup_exception_typeref != mdTypeRefNil)
    {
        Logger::Debug("Creating filter for try / catch for CallTargetBubbleUpException (end method).");
        filterEnd = CreateFilterForException(&reWriterWrapper, tracerTokens->GetExceptionTypeRef(),
                                             tracerTokens->GetBubbleUpExceptionTypeRef(),
                                             tracerTokens->GetBubbleUpExceptionFunctionDef(), exceptionValueEndIndex);
        endMethodCatchFirstInstr = reWriterWrapper.Pop();
        reWriterWrapper.LoadLocal(exceptionValueEndIndex);
    }

    // transfer->m_pTarget = endFilter;
    // *** EndMethod call catch
    tracerTokens->WriteLogException(&reWriterWrapper, integration_type_ref, &caller->type, &endMethodCatchFirstInstr);

    ILInstr* endMethodCatchLeaveInstr = reWriterWrapper.CreateInstr(CEE_LEAVE_S);

    // *** EndMethod exception handling clause
    EHClause endMethodExClause{};
    if (filterEnd != nullptr)
    {
        endMethodExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_FILTER;
        endMethodExClause.m_pTryBegin = endMethodTryStartInstr;
        endMethodExClause.m_pTryEnd = filterEnd;
        endMethodExClause.m_pHandlerBegin = endMethodCatchFirstInstr;
        endMethodExClause.m_pHandlerEnd = endMethodCatchLeaveInstr;
        endMethodExClause.m_pFilter = filterEnd;
    }
    else
    {
        endMethodExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
        endMethodExClause.m_pTryBegin = endMethodTryStartInstr;
        endMethodExClause.m_pTryEnd = endMethodCatchFirstInstr;
        endMethodExClause.m_pHandlerBegin = endMethodCatchFirstInstr;
        endMethodExClause.m_pHandlerEnd = endMethodCatchLeaveInstr;
        endMethodExClause.m_ClassToken = tracerTokens->GetExceptionTypeRef();
    }

    // *** EndMethod leave to finally
    ILInstr* endFinallyInstr = reWriterWrapper.EndFinally();
    endMethodTryLeave->m_pTarget = endFinallyInstr;
    endMethodCatchLeaveInstr->m_pTarget = endFinallyInstr;

    // ***
    // METHOD RETURN
    // ***

    // Load the current return value from the local var
    if (!isVoid)
    {
        reWriterWrapper.LoadLocal(returnValueIndex);
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
                            Logger::Error("The local variable index for the return value ('returnValueIndex') cannot "
                                          "be lower than zero.");
                            return S_FALSE;
                        }

                        ILInstr* leaveInstr = rewriter.NewILInstr();
                        leaveInstr->m_opcode = CEE_LEAVE_S;
                        leaveInstr->m_pTarget = endFinallyInstr->m_pNext;
                        rewriter.InsertAfter(pInstr, leaveInstr);
                    }
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
    exClause.m_ClassToken = tracerTokens->GetExceptionTypeRef();

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
        Logger::Info(m_corProfiler->GetILCodes("*** Rewriter(): Modified Code: ", &rewriter, *caller,
                                               module_metadata.metadata_import));
    }

    hr = rewriter.Export();

    if (FAILED(hr))
    {
        Logger::Warn("*** CallTarget_RewriterCallback(): Call to ILRewriter.Export() failed for "
                     "ModuleID=",
                     module_id, " ", function_token);
        return S_FALSE;
    }

    Logger::Info("*** CallTarget_RewriterCallback() Finished: ", caller->type.name, ".", caller->name,
                 "() [IsVoid=", isVoid, ", IsStatic=", isStatic,
                 ", IntegrationType=", integration_definition->integration_type.name, ", Arguments=", numArgs, "]");
    return S_OK;
}

InstrumentingProducts TracerMethodRewriter::GetInstrumentingProduct(RejitHandlerModule* moduleHandler,
                                                                    RejitHandlerModuleMethod* methodHandler)
{
    return InstrumentingProducts::Tracer;
}

WSTRING TracerMethodRewriter::GetInstrumentationId(RejitHandlerModule* moduleHandler,
                                                   RejitHandlerModuleMethod* methodHandler)
{
    return EmptyWStr;
}

std::tuple<WSTRING, WSTRING>
TracerMethodRewriter::GetResourceNameAndOperationName(const ComPtr<IMetaDataImport2>& metadataImport, const FunctionInfo* caller, TracerTokens* tracerTokens) const
{
    const BYTE* data = nullptr;
    ULONG pcbData = 0;
    auto hr = metadataImport->GetCustomAttributeByName(caller->id, tracerTokens->GetTraceAttributeType().data(),
                                                       reinterpret_cast<const void**>(&data), &pcbData);
    WSTRING resourceName;
    WSTRING operationName;

    // Parse the TraceAttribute
    if (hr == S_OK)
    {
        PCCOR_SIGNATURE signature = data;
        signature += 2; // skip prolog
        const ULONG numOfNamedArgs{CorSigUncompressData(signature)};
        signature += 1; // skip fixed arguments length

        for (ULONG argIndex = 0; argIndex < numOfNamedArgs; argIndex++)
        {
            signature += 2; // skip FIELD/PROPERTY and ELEM

            ULONG argNameLength{CorSigUncompressData(signature)}; // length of the name

            // OperationName (13 characters), ResourceName (12 characters)
            if (argNameLength != 12 && argNameLength != 13)
            {
                Logger::Error("TracerMethodRewriter::Rewrite: Failed to parse Trace Attribute for ",
                              " token=", caller->id, " caller_name=", caller->type.name, ".", caller->name, "()");
                break;
            }

            const bool isOperationName = argNameLength == 13;
            signature += argNameLength; // skip the argument name
            const auto value = GetStringValueFromBlob(signature);
            if (isOperationName)
            {
                operationName = value;
            }
            else
            {
                resourceName = value;
            }
        }
    }

    if (resourceName.empty())
    {
        if (caller->type.name.empty())
        {
            resourceName = caller->name;
        }
        else
        {
            resourceName =
                caller->type.name.empty()
                    ? caller->name
                    : caller->type.name.substr(caller->type.name.find_last_of(L'.') + 1) + WStr(".") + caller->name;
        }
    }

    if (operationName.empty())
    {
        operationName = WStr("trace.annotation");
    }

    return {std::move(resourceName), std::move(operationName)};
}

ILInstr* TracerMethodRewriter::CreateFilterForException(ILRewriterWrapper* rewriter, mdTypeRef exception,
                                                               mdTypeRef type_ref,
                                                               mdMemberRef containsCallTargetBubbleUpMemberRef,
                                                               ULONG exceptionValueIndex)
{
    ILInstr* filter = rewriter->CreateInstr(CEE_ISINST);
    filter->m_Arg32 = exception;
    rewriter->CreateInstr(CEE_DUP);
    ILInstr* isException = rewriter->CreateInstr(CEE_BRTRUE_S);
    rewriter->CreateInstr(CEE_POP);
    rewriter->LoadInt32(0);
    ILInstr* endNotException = rewriter->CreateInstr(CEE_BR_S);
    ILInstr* storeExceptionInIndex = rewriter->StLocal(exceptionValueIndex);
    isException->m_pTarget = storeExceptionInIndex;
    rewriter->LoadLocal(exceptionValueIndex);

    if (m_corProfiler->call_target_bubble_up_exception_function_available &&
        containsCallTargetBubbleUpMemberRef != mdMemberRefNil)
    {
        rewriter->CallMember(containsCallTargetBubbleUpMemberRef, false);
    }
    else
    {
        ILInstr* testBubbleUp = rewriter->CreateInstr(CEE_ISINST);
        testBubbleUp->m_Arg32 = type_ref;
        rewriter->LoadNull();
        rewriter->CreateInstr(CEE_CGT_UN);
    }

    rewriter->LoadInt32(0);
    rewriter->CreateInstr(CEE_CEQ);
    rewriter->LoadInt32(0);
    rewriter->CreateInstr(CEE_CGT_UN);
    ILInstr* endFilter = rewriter->CreateInstr(CEE_ENDFILTER);
    endNotException->m_pTarget = endFilter;
    return filter;
}
} // namespace trace
