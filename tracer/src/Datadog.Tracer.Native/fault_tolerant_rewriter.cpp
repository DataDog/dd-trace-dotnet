#include "fault_tolerant_rewriter.h"

#include "cor_profiler.h"
#include "dd_profiler_constants.h"
#include "fault_tolerant_envionrment_variables_util.h"
#include "fault_tolerant_tracker.h"
#include "il_rewriter_wrapper.h"
#include "logger.h"

using namespace fault_tolerant;

FaultTolerantRewriter::FaultTolerantRewriter(CorProfiler* corProfiler, std::unique_ptr<MethodRewriter> methodRewriter) :
    MethodRewriter(corProfiler),
    is_fault_tolerant_instrumentation_enabled(IsFaultTolerantInstrumentationEnabled()),
    m_methodRewriter(std::move(methodRewriter))
{
}

HRESULT FaultTolerantRewriter::ApplyKickoffInstrumentation(RejitHandlerModule* moduleHandler,
                                                           RejitHandlerModuleMethod* methodHandler) const
{
    // Kickoff instrumentation

    const auto moduleId = moduleHandler->GetModuleId();
    const auto methodId = methodHandler->GetMethodDef();

    LPCBYTE pMethodBytes;
    ULONG methodSize;
    auto hr = m_corProfiler->info_->GetILFunctionBody(moduleId, methodId, &pMethodBytes, &methodSize);
    FaultTolerantTracker::Instance()->KeepILBodyAndSize(moduleId, methodId, pMethodBytes, methodSize);

    if (FAILED(hr))
    {
        Logger::Warn("Failed to call GetILFunctionBody, ModuleID=", moduleId);
        return hr;
    }

    auto methodIdOfOriginalMethod = FaultTolerantTracker::Instance()->GetOriginalMethod(moduleId, methodId);
    auto methodIdOfInstrumentedMethod = FaultTolerantTracker::Instance()->GetInstrumentedMethod(moduleId, methodId);

    FunctionInfo* caller = methodHandler->GetFunctionInfo();
    int numArgs = caller->method_signature.NumberOfArguments();
    bool isStatic = !(caller->method_signature.CallingConvention() & IMAGE_CEE_CS_CALLCONV_HASTHIS);
    TypeSignature retFuncArg = caller->method_signature.GetReturnValue();
    const auto [retFuncElementType, retTypeFlags] = retFuncArg.GetElementTypeAndFlags();
    bool isVoid = (retTypeFlags & TypeFlagVoid) > 0;
    auto methodReturnType = caller->method_signature.GetReturnValue();
    auto [functionSignature, functionSignatureLength] = caller->method_signature.GetFunctionSignatureAndLength();
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
        int number = std::stoi(caller->type.name.substr(caller->type.name.find(L'`') + 1));
        argGenericCount += number;
    }

    auto currentType = caller->type.parent_type;
    while (currentType != nullptr)
    {
        if (currentType->isGeneric)
        {
            isGenericOrNestedType = true;
            int number = std::stoi(currentType->name.substr(currentType->name.find(L'`') + 1));
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

    ILRewriter rewriter(m_corProfiler->info_, methodHandler->GetFunctionControl(), moduleId, methodId);
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

    mdString methodNameIdToken;
    hr = moduleHandler->GetModuleMetadata()->metadata_emit->DefineUserString(
        instrumentedMethodName.c_str(), static_cast<ULONG>(instrumentedMethodName.length()), &methodNameIdToken);

    if (FAILED(hr))
    {
        Logger::Warn("*** FaultTolerantRewriter::Rewrite() DefineUserStringFailed.");
        return hr;
    }

    ILInstr* catchBegin = rewriterWrapper.LoadStr(methodNameIdToken);

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

    std::string original_code =
        m_corProfiler->GetILCodes("*** FaultTolerantRewriter::Rewrite() Original Code: ", &rewriter, *caller,
                                  moduleHandler->GetModuleMetadata()->metadata_import);

    Logger::Info(original_code);

    if (FAILED(kickOffHr))
    {
        Logger::Warn("Failed to emit IL for kickoff, ModuleID=", moduleId);
        return hr;
    }

    Logger::Info("Successfully instrumented kickoff, moduleId = ", moduleId);
    return hr;
}

HRESULT FaultTolerantRewriter::ApplyOriginalInstrumentation(RejitHandlerModule* moduleHandler,
                                                            RejitHandlerModuleMethod* methodHandler)
{
    const auto moduleId = moduleHandler->GetModuleId();
    const auto methodId = methodHandler->GetMethodDef();

    const auto methodIdOfKickoff =
        FaultTolerantTracker::Instance()->GetKickoffMethodFromOriginalMethod(moduleId, methodId);

    const auto [pMethodBytes, methodSize] =
        FaultTolerantTracker::Instance()->GetILBodyAndSize(moduleId, methodIdOfKickoff);

    auto hr = methodHandler->GetFunctionControl()->SetILFunctionBody(methodSize, pMethodBytes);

    if (FAILED(hr))
    {
        Logger::Error("Failed to set il function body!");
    }

    return hr;
}

HRESULT FaultTolerantRewriter::RewriteInternal(RejitHandlerModule* moduleHandler,
                                               RejitHandlerModuleMethod* methodHandler) const
{
    if (!is_fault_tolerant_instrumentation_enabled)
    {
        return m_methodRewriter->Rewrite(moduleHandler, methodHandler);
    }

    const auto moduleId = moduleHandler->GetModuleId();
    const auto methodId = methodHandler->GetMethodDef();

    if (FaultTolerantTracker::Instance()->IsKickoffMethod(moduleId, methodId))
    {
        return ApplyKickoffInstrumentation(moduleHandler, methodHandler);
    }
    else if (FaultTolerantTracker::Instance()->IsOriginalMethod(moduleId, methodId))
    {
        return ApplyOriginalInstrumentation(moduleHandler, methodHandler);
    }
    else if (FaultTolerantTracker::Instance()->IsInstrumentedMethod(moduleId, methodId))
    {
        auto hr = m_methodRewriter->Rewrite(moduleHandler, methodHandler);

        if (hr != S_OK)
        {
            const auto methodIdOfKickoff =
                FaultTolerantTracker::Instance()->GetKickoffMethodFromInstrumentedMethod(moduleId, methodId);
            const auto [pMethodBytes, methodSize] =
                FaultTolerantTracker::Instance()->GetILBodyAndSize(moduleId, methodIdOfKickoff);

            methodHandler->GetFunctionControl()->SetILFunctionBody(methodSize, pMethodBytes);
        }

        return hr;
    }
    else
    {
        return E_NOTIMPL;
    }
}

HRESULT FaultTolerantRewriter::Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler)
{
    const auto hr = RewriteInternal(moduleHandler, methodHandler);
    return FAILED(hr) ? S_FALSE : S_OK;
}
