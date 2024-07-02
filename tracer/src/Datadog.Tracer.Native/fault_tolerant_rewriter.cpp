#include "fault_tolerant_rewriter.h"

#include "cor_profiler.h"
#include "dd_profiler_constants.h"
#include "environment_variables_util.h"
#include "fault_tolerant_cor_profiler_function_control.h"
#include "fault_tolerant_envionrment_variables_util.h"
#include "fault_tolerant_tracker.h"
#include "il_rewriter_wrapper.h"
#include "logger.h"

using namespace fault_tolerant;

FaultTolerantRewriter::FaultTolerantRewriter(CorProfiler* corProfiler, std::unique_ptr<MethodRewriter> methodRewriter,
                                             std::shared_ptr<RejitHandler> rejit_handler) :
    MethodRewriter(corProfiler),
    is_fault_tolerant_instrumentation_enabled(IsFaultTolerantInstrumentationEnabled()),
    m_methodRewriter(std::move(methodRewriter)),
    m_rejit_handler(std::move(rejit_handler))
{
}

HRESULT FaultTolerantRewriter::ApplyKickoffInstrumentation(RejitHandlerModule* moduleHandler,
                                                           RejitHandlerModuleMethod* methodHandler,
                                                           ICorProfilerFunctionControl* pFunctionControl)
{
    // Kickoff instrumentation

    const auto moduleId = moduleHandler->GetModuleId();
    const auto methodId = methodHandler->GetMethodDef();

    LPCBYTE pMethodBytes;
    ULONG methodSize;
    auto hr = m_corProfiler->info_->GetILFunctionBody(moduleId, methodId, &pMethodBytes, &methodSize);

    if (FAILED(hr))
    {
        Logger::Warn("Failed to call GetILFunctionBody, ModuleID=", moduleId);
        return hr;
    }

    FaultTolerantTracker::Instance()->CacheILBodyIfEmpty(moduleId, methodId, pMethodBytes, methodSize);

    auto methodIdOfOriginalMethod = FaultTolerantTracker::Instance()->GetOriginalMethod(moduleId, methodId);
    auto methodIdOfInstrumentedMethod = FaultTolerantTracker::Instance()->GetInstrumentedMethod(moduleId, methodId);

    // Request ReJIT for instrumented and original duplications.
    std::vector<MethodIdentifier> requests = {{moduleId, methodIdOfOriginalMethod}, {moduleId, methodIdOfInstrumentedMethod}};
    auto promise = std::make_shared<std::promise<void>>();
    auto future = promise->get_future();
    m_rejit_handler->EnqueueRequestRejit(requests, promise);
    future.get();

    FunctionInfo* caller = methodHandler->GetFunctionInfo();
    int numArgs = caller->method_signature.NumberOfArguments();
    bool isStatic = !(caller->method_signature.CallingConvention() & IMAGE_CEE_CS_CALLCONV_HASTHIS);
    TypeSignature retFuncArg = caller->method_signature.GetReturnValue();
    const auto [retFuncElementType, retTypeFlags] = retFuncArg.GetElementTypeAndFlags();
    bool isVoid = (retTypeFlags & TypeFlagVoid) > 0;
    auto methodReturnType = caller->method_signature.GetReturnValue();
    auto [functionSignature, functionSignatureLength] = caller->method_signature.GetFunctionSignatureAndLength();
    const auto instrumentationId = m_methodRewriter->GetInstrumentationId(moduleHandler, methodHandler);
    const auto instrumentingProduct = m_methodRewriter->GetInstrumentingProduct(moduleHandler, methodHandler);
    FaultTolerantTokens* faultTolerantTokens = moduleHandler->GetModuleMetadata()->GetFaultTolerantTokens();
    faultTolerantTokens->EnsureCorLibTokens();

    auto instrumentedMethodName = caller->name + WStr("<Instrumented>");
    instrumentedMethodName.erase(std::remove(instrumentedMethodName.begin(), instrumentedMethodName.end(), L'.'),
                                 instrumentedMethodName.end());
    instrumentedMethodName.erase(std::remove(instrumentedMethodName.begin(), instrumentedMethodName.end(), L'_'),
                                 instrumentedMethodName.end());
    auto originalMethodName = caller->name + WStr("<Original>");
    originalMethodName.erase(std::remove(originalMethodName.begin(), originalMethodName.end(), L'.'),
                             originalMethodName.end());
    originalMethodName.erase(std::remove(originalMethodName.begin(), originalMethodName.end(), L'_'),
                             originalMethodName.end());

    // Is the type generic:
    auto metadataImport = moduleHandler->GetModuleMetadata()->metadata_import;
    auto isGenericOrNestedType = false;
    auto argGenericCount = 0;

    if (caller->type.isGeneric)
    {
        isGenericOrNestedType = true;
        std::string str(caller->type.name.begin(), caller->type.name.end());
        int number = std::stoi(str.substr(str.find('`') + 1));
        argGenericCount += number;
    }

    auto currentType = caller->type.parent_type;
    while (currentType != nullptr)
    {
        if (currentType->isGeneric)
        {
            isGenericOrNestedType = true;
            std::string str(currentType->name.begin(), currentType->name.end());
            int number = std::stoi(str.substr(str.find('`') + 1));
            argGenericCount += number;
        }

        currentType = currentType->parent_type;
    }

    if (isGenericOrNestedType)
    {
        unsigned typeBuffer;
        auto typeSize = CorSigCompressToken(caller->type.id, &typeBuffer);
        COR_SIGNATURE signature[500];
        unsigned offset = 0;
        unsigned argCountBuffer;
        auto argCountSize = CorSigCompressData(argGenericCount, &argCountBuffer);
        mdTypeSpec containingTypeSpec = mdTypeSpecNil;

        signature[offset++] = ELEMENT_TYPE_GENERICINST;
        signature[offset++] = caller->type.valueType ? ELEMENT_TYPE_VALUETYPE : ELEMENT_TYPE_CLASS;
        memcpy(&signature[offset], &typeBuffer, typeSize);
        offset += typeSize;
        memcpy(&signature[offset], &argCountBuffer, argCountSize);
        offset += argCountSize;

        for (int genArgIndex = 0; genArgIndex < argGenericCount; genArgIndex++)
        {
            signature[offset++] = ELEMENT_TYPE_VAR;
            signature[offset++] = genArgIndex;
        }

        hr = moduleHandler->GetModuleMetadata()->metadata_emit->GetTokenFromTypeSpec(signature, offset,
                                                                                     &containingTypeSpec);

        if (FAILED(hr))
        {
            Logger::Warn("Error creating TypeSpec token.");
            return hr;
        }

        hr = moduleHandler->GetModuleMetadata()->metadata_emit->DefineMemberRef(
            containingTypeSpec, instrumentedMethodName.c_str(), functionSignature, functionSignatureLength,
            &methodIdOfInstrumentedMethod);
        if (FAILED(hr))
        {
            Logger::Warn("Failed in DefineMemberRef of Instrumented Version.");
            return hr;
        }

        hr = moduleHandler->GetModuleMetadata()->metadata_emit->DefineMemberRef(
            containingTypeSpec, originalMethodName.c_str(), functionSignature, functionSignatureLength,
            &methodIdOfOriginalMethod);
        if (FAILED(hr))
        {
            Logger::Warn("Failed in DefineMemberRef of Original Version.");
            return hr;
        }
    }

    ILRewriter rewriter(m_corProfiler->info_, pFunctionControl, moduleId, methodId);
    ILRewriterWrapper rewriterWrapper(&rewriter);

    rewriterWrapper.SetILPosition(rewriter.GetILList()->m_pNext);

    // LocalVarSig
    PCCOR_SIGNATURE returnSignatureType = nullptr;

    auto returnSignatureTypeSize = methodReturnType.GetSignature(returnSignatureType);

    unsigned exTypeRefBuffer;
    auto exTypeRefSize = CorSigCompressToken(faultTolerantTokens->GetExceptionTypeRef(), &exTypeRefBuffer);

    ULONG newSignatureOffset = 0;
    COR_SIGNATURE newSignatureBuffer[BUFFER_SIZE];
    newSignatureBuffer[newSignatureOffset++] = IMAGE_CEE_CS_CALLCONV_LOCAL_SIG;
    newSignatureBuffer[newSignatureOffset++] = isVoid ? 2 : 3;

    // shouldSelfHeal, index = 0
    newSignatureBuffer[newSignatureOffset++] = ELEMENT_TYPE_BOOLEAN;

    auto shouldSelfHealLocalIndex = 0;

    // Exception value, index = 1
    newSignatureBuffer[newSignatureOffset++] = ELEMENT_TYPE_CLASS;
    memcpy(&newSignatureBuffer[newSignatureOffset], &exTypeRefBuffer, exTypeRefSize);
    newSignatureOffset += exTypeRefSize;

    auto exceptionLocalIndex = 1;

    if (!isVoid)
    {
        // return value, index = 2
        memcpy(&newSignatureBuffer[newSignatureOffset], returnSignatureType, returnSignatureTypeSize);
        newSignatureOffset += returnSignatureTypeSize;
    }

    auto returnValueLocalIndex = 2;

    // Get new locals token
    mdToken newLocalVarSig;
    hr = moduleHandler->GetModuleMetadata()->metadata_emit->GetTokenFromSig(newSignatureBuffer, newSignatureOffset,
                                                                            &newLocalVarSig);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating new locals var signature.");
        return hr;
    }

    rewriter.SetTkLocalVarSig(newLocalVarSig);

    // Call instrumented
    for (int argIndex = 0; argIndex < (numArgs + (isStatic ? 0 : 1)); argIndex++)
    {
        rewriterWrapper.LoadArgument(argIndex);
    }

    if (caller->method_signature.CallingConvention() & IMAGE_CEE_CS_CALLCONV_GENERIC)
    {
        ULONG cGenericTypeParameters;
        CorSigUncompressData(functionSignature + 1, &cGenericTypeParameters);

        unsigned argCountBuffer;
        auto argCountSize = CorSigCompressData(cGenericTypeParameters, &argCountBuffer);

        auto signatureLength = 2 + (cGenericTypeParameters * 2);

        COR_SIGNATURE signature[500];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
        memcpy(&signature[offset], &argCountBuffer, argCountSize);
        offset += argCountSize;

        for (int genArgIndex = 0; genArgIndex < static_cast<int>(cGenericTypeParameters); genArgIndex++)
        {
            signature[offset++] = ELEMENT_TYPE_MVAR;
            signature[offset++] = genArgIndex;
        }

        mdMethodSpec beginMethodSpec = mdMethodSpecNil;
        hr = moduleHandler->GetModuleMetadata()->metadata_emit->DefineMethodSpec(
            methodIdOfInstrumentedMethod, signature, signatureLength, &beginMethodSpec);
        rewriterWrapper.CallMember(beginMethodSpec, false);
    }
    else
    {
        rewriterWrapper.CallMember(methodIdOfInstrumentedMethod, false);
    }

    if (!isVoid)
    {
        rewriterWrapper.StLocal(returnValueLocalIndex);
    }

    ILInstr* tryLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    mdString instrumentationIdToken;
    hr = moduleHandler->GetModuleMetadata()->metadata_emit->DefineUserString(
        instrumentationId.c_str(), static_cast<ULONG>(instrumentationId.length()),
        &instrumentationIdToken);

    if (FAILED(hr))
    {
        Logger::Warn("*** FaultTolerantRewriter::Rewrite() Failed to define instrumentation "
                     "version string ",
                     moduleId, " ", methodId);
        return hr;
    }

    // static bool IsInstrumentationIdSucceeded(Exception ex, IntPtr moduleId, int methodToken, string instrumentationId,
    // InstrumentingProducts products)

    ILInstr* catchBegin;
    if (sizeof(UINT_PTR) == 4) // 32-bit
    {
        catchBegin = rewriterWrapper.LoadInt32(static_cast<INT32>(moduleId));
    }
    else if (sizeof(UINT_PTR) == 8) // 64-bit
    {
        catchBegin = rewriterWrapper.LoadInt64(static_cast<INT64>(moduleId));
    }

    rewriterWrapper.LoadInt32(methodId);
    rewriterWrapper.LoadStr(instrumentationIdToken);

    rewriterWrapper.LoadInt32(static_cast<INT32>(instrumentingProduct));

    ILInstr* shouldHeal;
    faultTolerantTokens->WriteShouldHeal(&rewriterWrapper, &shouldHeal);

    ILInstr* brFalse = rewriterWrapper.CreateInstr(CEE_BRFALSE_S);

    // Call original
    for (int argIndex = 0; argIndex < (numArgs + (isStatic ? 0 : 1)); argIndex++)
    {
        rewriterWrapper.LoadArgument(argIndex);
    }

    if (caller->method_signature.CallingConvention() & IMAGE_CEE_CS_CALLCONV_GENERIC)
    {
        ULONG cGenericTypeParameters;
        CorSigUncompressData(functionSignature + 1, &cGenericTypeParameters);

        unsigned argCountBuffer;
        auto argCountSize = CorSigCompressData(cGenericTypeParameters, &argCountBuffer);

        auto signatureLength = 2 + (cGenericTypeParameters * 2);

        COR_SIGNATURE signature[500];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
        memcpy(&signature[offset], &argCountBuffer, argCountSize);
        offset += argCountSize;

        for (int genArgIndex = 0; genArgIndex < static_cast<int>(cGenericTypeParameters); genArgIndex++)
        {
            signature[offset++] = ELEMENT_TYPE_MVAR;
            signature[offset++] = genArgIndex;
        }

        mdMethodSpec beginMethodSpec = mdMethodSpecNil;
        hr = moduleHandler->GetModuleMetadata()->metadata_emit->DefineMethodSpec(methodIdOfOriginalMethod, signature,
                                                                                 signatureLength, &beginMethodSpec);
        rewriterWrapper.CallMember(beginMethodSpec, false);
    }
    else
    {
        rewriterWrapper.CallMember(methodIdOfOriginalMethod, false);
    }

    if (!isVoid)
    {
        rewriterWrapper.StLocal(returnValueLocalIndex);
    }

    ILInstr* catchLeaveInstr = rewriterWrapper.CreateInstr(CEE_LEAVE_S);

    auto rethrow = rewriterWrapper.Rethrow();
    brFalse->m_pTarget = rethrow;

    // Return block
    ILInstr* firstInstructionOfReturnBlock = nullptr;
    if (!isVoid)
    {
        firstInstructionOfReturnBlock = rewriterWrapper.LoadLocal(returnValueLocalIndex);
    }

    auto ret = rewriterWrapper.Return();

    if (firstInstructionOfReturnBlock == nullptr)
    {
        firstInstructionOfReturnBlock = ret;
    }

    catchLeaveInstr->m_pTarget = firstInstructionOfReturnBlock;
    tryLeaveInstr->m_pTarget = firstInstructionOfReturnBlock;

    auto tryBegin = rewriter.GetILList()->m_pNext;

    EHClause beginMethodExClause = {};
    beginMethodExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    beginMethodExClause.m_pTryBegin = tryBegin;
    beginMethodExClause.m_pTryEnd = catchBegin;
    beginMethodExClause.m_pHandlerBegin = catchBegin;
    beginMethodExClause.m_pHandlerEnd = rethrow;
    beginMethodExClause.m_ClassToken = faultTolerantTokens->GetExceptionTypeRef();

    auto newEHClauses = new EHClause[1];
    newEHClauses[0] = beginMethodExClause;

    rewriter.SetEHClause(newEHClauses, 1);
    const auto kickOffHr = rewriter.Export();

    if (IsDumpILRewriteEnabled())
    {
        std::string original_code =
            m_corProfiler->GetILCodes("*** FaultTolerantRewriter::Rewrite() Original Code: ", &rewriter, *caller,
                                      moduleHandler->GetModuleMetadata()->metadata_import);
        Logger::Info(original_code);
    }

    if (FAILED(kickOffHr))
    {
        Logger::Warn("Failed to emit IL for kickoff, ModuleID=", moduleId);
        return hr;
    }

    Logger::Info("Successfully instrumented kickoff, moduleId = ", moduleId);
    return hr;
}

HRESULT FaultTolerantRewriter::ApplyOriginalInstrumentation(RejitHandlerModule* moduleHandler,
                                                            RejitHandlerModuleMethod* methodHandler,
                                                            ICorProfilerFunctionControl* pFunctionControl)
{
    const auto moduleId = moduleHandler->GetModuleId();
    const auto methodId = methodHandler->GetMethodDef();

    const auto methodIdOfKickoff =
        FaultTolerantTracker::Instance()->GetKickoffMethodFromOriginalMethod(moduleId, methodId);

    const auto [pMethodBytes, methodSize] =
        FaultTolerantTracker::Instance()->GetILBodyAndSize(moduleId, methodIdOfKickoff);

    auto hr = pFunctionControl->SetILFunctionBody(methodSize, pMethodBytes);

    if (FAILED(hr))
    {
        Logger::Error("Failed to set il function body!");
    }

    return hr;
}

HRESULT FaultTolerantRewriter::InjectSuccessfulInstrumentation(RejitHandlerModule* moduleHandler,
                                                               RejitHandlerModuleMethod* methodHandler,
                                                               ICorProfilerFunctionControl* pFunctionControl,
                                                               ICorProfilerInfo* pCorProfilerInfo,
                                                               LPCBYTE pMethodBytes) const
{
    const auto moduleId = moduleHandler->GetModuleId();
    const auto methodId = methodHandler->GetMethodDef();
    const auto instrumentationId = m_methodRewriter->GetInstrumentationId(moduleHandler, methodHandler);
    const auto instrumentingProduct = m_methodRewriter->GetInstrumentingProduct(moduleHandler, methodHandler);
    FunctionInfo* caller = methodHandler->GetFunctionInfo();
    FaultTolerantTokens* faultTolerantTokens = moduleHandler->GetModuleMetadata()->GetFaultTolerantTokens();
    faultTolerantTokens->EnsureCorLibTokens();

    ILRewriter rewriter(pCorProfilerInfo, pFunctionControl, moduleId, methodId);
    auto hr = rewriter.Import(pMethodBytes);

    if (FAILED(hr))
    {
        Logger::Warn(
            "*** FaultTolerantRewriter::InjectSuccessfulInstrumentation() Call to ILRewriter.Import() failed for ",
            moduleId, " ", methodId);
        return hr;
    }

    ILRewriterWrapper rewriterWrapper(&rewriter);
    rewriterWrapper.SetILPosition(rewriter.GetILList()->m_pNext);

    mdString instrumentationIdToken;
    hr = moduleHandler->GetModuleMetadata()->metadata_emit->DefineUserString(
        instrumentationId.c_str(), static_cast<ULONG>(instrumentationId.length()),
        &instrumentationIdToken);

    if (FAILED(hr))
    {
        Logger::Warn("*** FaultTolerantRewriter::InjectSuccessfulInstrumentation() Failed to define instrumentation "
                     "version string ",
                     moduleId, " ", methodId);
        return hr;
    }

    // static void AddSuccessfulInstrumentationId(IntPtr moduleId, int methodToken, string instrumentationId,
    // InstrumentingProducts products)

    if (sizeof(UINT_PTR) == 4) // 32-bit
    {
        rewriterWrapper.LoadInt32(static_cast<INT32>(moduleId));
    }
    else if (sizeof(UINT_PTR) == 8) // 64-bit
    {
        rewriterWrapper.LoadInt64(static_cast<INT64>(moduleId));
    }

    rewriterWrapper.LoadInt32(methodId);
    rewriterWrapper.LoadStr(instrumentationIdToken);
    rewriterWrapper.LoadInt32(static_cast<INT32>(instrumentingProduct));
    ILInstr* reportSuccessfulInstrumentation;
    faultTolerantTokens->WriteReportSuccessfulInstrumentation(&rewriterWrapper, &reportSuccessfulInstrumentation);

    hr = rewriter.Export();

    if (IsDumpILRewriteEnabled())
    {
        std::string original_code = m_corProfiler->GetILCodes(
            "*** FaultTolerantRewriter::InjectSuccessfulInstrumentation() Original Code: ", &rewriter, *caller,
            moduleHandler->GetModuleMetadata()->metadata_import);
        Logger::Info(original_code);
    }

    if (FAILED(hr))
    {
        Logger::Warn("Failed to emit IL for injecting successful instrumentation, ModuleID=", moduleId);
        return hr;
    }

    Logger::Info("Successfully added injection of successful instrumentation report, moduleId = ", moduleId);
    return hr;
}

HRESULT FaultTolerantRewriter::RewriteInternal(RejitHandlerModule* moduleHandler,
                                               RejitHandlerModuleMethod* methodHandler,
                                               ICorProfilerFunctionControl* pFunctionControl,
                                               ICorProfilerInfo* pCorProfilerInfo)
{
    if (!is_fault_tolerant_instrumentation_enabled)
    {
        return m_methodRewriter->Rewrite(moduleHandler, methodHandler, pFunctionControl, pCorProfilerInfo);
    }

    const auto moduleId = moduleHandler->GetModuleId();
    const auto methodId = methodHandler->GetMethodDef();

    if (FaultTolerantTracker::Instance()->IsKickoffMethod(moduleId, methodId))
    {
        const auto instrumentationId = m_methodRewriter->GetInstrumentationId(moduleHandler, methodHandler);
        const auto instrumentingProduct = m_methodRewriter->GetInstrumentingProduct(moduleHandler, methodHandler);

        if (FaultTolerantTracker::Instance()->IsInstrumentationIdSucceeded(moduleId, methodId, instrumentationId, instrumentingProduct))
        {
            // Request Revert for the original method
            //std::vector<MethodIdentifier> requests = {{moduleId, methodId}};
            //auto promise = std::make_shared<std::promise<void>>();
            //auto future = promise->get_future();
            //m_rejit_handler->EnqueueRequestRevert(requests, promise);
            //future.get();

            // We also set the original IL function body, in case the revert did not succeeded.
            // We do that just as a safeguard, it has harmless effect.
            //const auto [pMethodBytes, methodSize] = FaultTolerantTracker::Instance()->GetILBodyAndSize(moduleId, methodId);
            //pFunctionControl->SetILFunctionBody(methodSize, pMethodBytes);

            return m_methodRewriter->Rewrite(moduleHandler, methodHandler, pFunctionControl, pCorProfilerInfo);
        }
        else
        {
            return ApplyKickoffInstrumentation(moduleHandler, methodHandler, pFunctionControl);
        }
    }
    else if (FaultTolerantTracker::Instance()->IsOriginalMethod(moduleId, methodId))
    {
        return ApplyOriginalInstrumentation(moduleHandler, methodHandler, pFunctionControl);
    }
    else if (FaultTolerantTracker::Instance()->IsInstrumentedMethod(moduleId, methodId))
    {
        const auto methodIdOfKickoff =
            FaultTolerantTracker::Instance()->GetKickoffMethodFromInstrumentedMethod(moduleId, methodId);
        const auto [pMethodBytes, methodSize] =
            FaultTolerantTracker::Instance()->GetILBodyAndSize(moduleId, methodIdOfKickoff);

        pFunctionControl->SetILFunctionBody(methodSize, pMethodBytes);

        // substitute the methodHandler of the instrumented duplication with the original (the one of the kickoff)
        if (!moduleHandler->TryGetMethod(methodIdOfKickoff, &methodHandler))
        {
            Logger::Warn("FaultTolerantRewriter::RewriteInternal(): Failed to substitute the methodHandler of the instrumented duplication with the original's.");
            return S_FALSE;
        }

        InjectSuccessfulInstrumentationLambda injectSuccessfulInstrumentation =
            [this](RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler,
                   ICorProfilerFunctionControl* pFunctionControl, ICorProfilerInfo* pCorProfilerInfo, LPCBYTE pbILMethod) -> HRESULT {
            return this->InjectSuccessfulInstrumentation(moduleHandler, methodHandler, pFunctionControl, pCorProfilerInfo, pbILMethod);
        };


        auto faultTolerantFunctionControl = std::make_unique<fault_tolerant::FaultTolerantCorProfilerFunctionControl>(pFunctionControl, pCorProfilerInfo, moduleId, methodId, moduleHandler, methodHandler, injectSuccessfulInstrumentation);
        auto hr = m_methodRewriter->Rewrite(moduleHandler, methodHandler, faultTolerantFunctionControl.get(), pCorProfilerInfo);

        //if (hr == S_OK)
        //{
        //    return InjectSuccessfulInstrumentation(moduleHandler, methodHandler, pFunctionControl);
        //}
        // else
        //{
        //     const auto methodIdOfKickoff =
        //         FaultTolerantTracker::Instance()->GetKickoffMethodFromInstrumentedMethod(moduleId, methodId);
        //     const auto [pMethodBytes, methodSize] =
        //         FaultTolerantTracker::Instance()->GetILBodyAndSize(moduleId, methodIdOfKickoff);

        //    methodHandler->GetFunctionControl()->SetILFunctionBody(methodSize, pMethodBytes);
        //}

        return hr;
    }
    else
    {
        return E_NOTIMPL;
    }
}

HRESULT FaultTolerantRewriter::Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler,
                                       ICorProfilerFunctionControl* pFunctionControl, ICorProfilerInfo* pCorProfilerInfo)
{
    const auto hr = RewriteInternal(moduleHandler, methodHandler, pFunctionControl, pCorProfilerInfo);
    return (FAILED(hr) || hr == S_FALSE) ? S_FALSE : S_OK;
}

InstrumentingProducts FaultTolerantRewriter::GetInstrumentingProduct(RejitHandlerModule* moduleHandler,
                                                                    RejitHandlerModuleMethod* methodHandler)
{
    return m_methodRewriter->GetInstrumentingProduct(moduleHandler, methodHandler);
}

WSTRING FaultTolerantRewriter::GetInstrumentationId(RejitHandlerModule* moduleHandler,
                                                         RejitHandlerModuleMethod* methodHandler)
{
    return m_methodRewriter->GetInstrumentationId(moduleHandler, methodHandler);
}
