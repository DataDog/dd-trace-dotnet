#include "tracer_tokens.h"

#include "dd_profiler_constants.h"
#include "il_rewriter_wrapper.h"
#include "logger.h"
#include "module_metadata.h"

namespace trace
{

const int signatureBufferSize = 500;

/**
 * TRACER CONSTANTS
 **/

static const shared::WSTRING managed_profiler_calltarget_type = WStr("Datadog.Trace.ClrProfiler.CallTarget.CallTargetInvoker");
static const shared::WSTRING managed_profiler_calltarget_statetype = WStr("Datadog.Trace.ClrProfiler.CallTarget.CallTargetState");
static const shared::WSTRING managed_profiler_calltarget_returntype = WStr("Datadog.Trace.ClrProfiler.CallTarget.CallTargetReturn");
static const shared::WSTRING managed_profiler_calltarget_returntype_generics = WStr("Datadog.Trace.ClrProfiler.CallTarget.CallTargetReturn`1");
static const shared::WSTRING managed_profiler_calltarget_beginmethod_name = WStr("BeginMethod");
static const shared::WSTRING managed_profiler_calltarget_endmethod_name = WStr("EndMethod");
static const shared::WSTRING managed_profiler_calltarget_logexception_name = WStr("LogException");

/**
 * PRIVATE
 **/

// slowpath BeginMethod
HRESULT TracerTokens::WriteBeginMethodWithArgumentsArray(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                                             const TypeInfo* currentType, ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (beginArrayMemberRef == mdMemberRefNil)
    {
        unsigned callTargetStateBuffer;
        auto callTargetStateSize = CorSigCompressToken(callTargetStateTypeRef, &callTargetStateBuffer);

        auto signatureLength = 8 + callTargetStateSize;
        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x02;
        signature[offset++] = 0x02;

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;

        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x01;

        signature[offset++] = ELEMENT_TYPE_SZARRAY;
        signature[offset++] = ELEMENT_TYPE_OBJECT;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(callTargetTypeRef,
                                                                  managed_profiler_calltarget_beginmethod_name.data(),
                                                                  signature, signatureLength, &beginArrayMemberRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper beginArrayMemberRef could not be defined.");
            return hr;
        }
    }

    mdMethodSpec beginArrayMethodSpec = mdMethodSpecNil;

    unsigned integrationTypeBuffer;
    ULONG integrationTypeSize = CorSigCompressToken(integrationTypeRef, &integrationTypeBuffer);

    bool isValueType = currentType->valueType;
    mdToken currentTypeRef = GetCurrentTypeRef(currentType, isValueType);
    if (currentTypeRef == mdTokenNil)
    {
        isValueType = false;
        currentTypeRef = GetObjectTypeRef();
    }

    unsigned currentTypeBuffer;
    ULONG currentTypeSize = CorSigCompressToken(currentTypeRef, &currentTypeBuffer);

    auto signatureLength = 4 + integrationTypeSize + currentTypeSize;
    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;
    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
    signature[offset++] = 0x02;

    signature[offset++] = ELEMENT_TYPE_CLASS;
    memcpy(&signature[offset], &integrationTypeBuffer, integrationTypeSize);
    offset += integrationTypeSize;

    if (isValueType)
    {
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    }
    else
    {
        signature[offset++] = ELEMENT_TYPE_CLASS;
    }
    memcpy(&signature[offset], &currentTypeBuffer, currentTypeSize);
    offset += currentTypeSize;

    hr = module_metadata->metadata_emit->DefineMethodSpec(beginArrayMemberRef, signature, signatureLength,
                                                          &beginArrayMethodSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating begin method spec.");
        return hr;
    }

    *instruction = rewriterWrapper->CallMember(beginArrayMethodSpec, false);
    return S_OK;
}

/**
 * PROTECTED
 **/

const shared::WSTRING& TracerTokens::GetCallTargetType()
{
    return managed_profiler_calltarget_type;
}

const shared::WSTRING& TracerTokens::GetCallTargetStateType()
{
    return managed_profiler_calltarget_statetype;
}

const shared::WSTRING& TracerTokens::GetCallTargetReturnType()
{
    return managed_profiler_calltarget_returntype;
}

const shared::WSTRING& TracerTokens::GetCallTargetReturnGenericType()
{
    return managed_profiler_calltarget_returntype_generics;
}


/**
 * PUBLIC
 **/

TracerTokens::TracerTokens(ModuleMetadata* module_metadata_ptr, const bool enableByRefInstrumentation,
                           const bool enableCallTargetStateByRef) :
    CallTargetTokens(module_metadata_ptr, enableByRefInstrumentation, enableCallTargetStateByRef)
{
    for (int i = 0; i < FASTPATH_COUNT; i++)
    {
        beginMethodFastPathRefs[i] = mdMemberRefNil;
    }
}

HRESULT TracerTokens::WriteBeginMethod(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                           const TypeInfo* currentType,
                                           const std::vector<TypeSignature>& methodArguments,
                                           const bool ignoreByRefInstrumentation,
                                           ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    auto numArguments = (int) methodArguments.size();
    if (numArguments >= FASTPATH_COUNT)
    {
        return WriteBeginMethodWithArgumentsArray(rewriterWrapperPtr, integrationTypeRef, currentType, instruction);
    }

    //
    // FastPath
    //

    mdMemberRef beginMethodFastPathRef;
    if (ignoreByRefInstrumentation ||
        beginMethodFastPathRefs[numArguments] == mdMemberRefNil)
    {
        unsigned callTargetStateBuffer;
        auto callTargetStateSize = CorSigCompressToken(callTargetStateTypeRef, &callTargetStateBuffer);

        unsigned long signatureLength;
        if (enable_by_ref_instrumentation)
        {
            signatureLength = 6 + (numArguments * 3) + callTargetStateSize;
        }
        else
        {
            signatureLength = 6 + (numArguments * 2) + callTargetStateSize;
        }
        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x02 + numArguments;
        signature[offset++] = 0x01 + numArguments;

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;

        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x01;

        for (auto i = 0; i < numArguments; i++)
        {
            if (!ignoreByRefInstrumentation && enable_by_ref_instrumentation)
            {
                signature[offset++] = ELEMENT_TYPE_BYREF;
            }
            signature[offset++] = ELEMENT_TYPE_MVAR;
            signature[offset++] = 0x01 + (i + 1);
        }

        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            callTargetTypeRef, managed_profiler_calltarget_beginmethod_name.data(), signature, signatureLength,
            &beginMethodFastPathRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper beginMethod for ", numArguments, " arguments could not be defined.");
            return hr;
        }

        if (!ignoreByRefInstrumentation)
        {
            beginMethodFastPathRefs[numArguments] = beginMethodFastPathRef;
        }
    }
    else
    {
        beginMethodFastPathRef = beginMethodFastPathRefs[numArguments];
    }

    mdMethodSpec beginMethodSpec = mdMethodSpecNil;

    unsigned integrationTypeBuffer;
    ULONG integrationTypeSize = CorSigCompressToken(integrationTypeRef, &integrationTypeBuffer);

    bool isValueType = currentType->valueType;
    mdToken currentTypeRef = GetCurrentTypeRef(currentType, isValueType);
    if (currentTypeRef == mdTokenNil)
    {
        isValueType = false;
        currentTypeRef = GetObjectTypeRef();
    }

    unsigned currentTypeBuffer;
    ULONG currentTypeSize = CorSigCompressToken(currentTypeRef, &currentTypeBuffer);

    auto signatureLength = 4 + integrationTypeSize + currentTypeSize;

    PCCOR_SIGNATURE argumentsSignatureBuffer[FASTPATH_COUNT];
    ULONG argumentsSignatureSize[FASTPATH_COUNT];
    for (auto i = 0; i < numArguments; i++)
    {
        const auto [elementType, argTypeFlags] = methodArguments[i].GetElementTypeAndFlags();

        if (enable_by_ref_instrumentation && (argTypeFlags & TypeFlagByRef))
        {
            PCCOR_SIGNATURE argSigBuff;
            auto signatureSize = methodArguments[i].GetSignature(argSigBuff);
            if (argSigBuff[0] == ELEMENT_TYPE_BYREF)
            {
                argumentsSignatureBuffer[i] = argSigBuff + 1;
                argumentsSignatureSize[i] = signatureSize - 1;
                signatureLength += signatureSize - 1;
            }
            else
            {
                argumentsSignatureBuffer[i] = argSigBuff;
                argumentsSignatureSize[i] = signatureSize;
                signatureLength += signatureSize;
            }
        }
        else
        {
            auto signatureSize = methodArguments[i].GetSignature(argumentsSignatureBuffer[i]);
            argumentsSignatureSize[i] = signatureSize;
            signatureLength += signatureSize;
        }
    }

    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
    signature[offset++] = 0x02 + numArguments;

    signature[offset++] = ELEMENT_TYPE_CLASS;
    memcpy(&signature[offset], &integrationTypeBuffer, integrationTypeSize);
    offset += integrationTypeSize;

    if (isValueType)
    {
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    }
    else
    {
        signature[offset++] = ELEMENT_TYPE_CLASS;
    }
    memcpy(&signature[offset], &currentTypeBuffer, currentTypeSize);
    offset += currentTypeSize;

    for (auto i = 0; i < numArguments; i++)
    {
        memcpy(&signature[offset], argumentsSignatureBuffer[i], argumentsSignatureSize[i]);
        offset += argumentsSignatureSize[i];
    }

    hr = module_metadata->metadata_emit->DefineMethodSpec(beginMethodFastPathRef, signature,
                                                          signatureLength, &beginMethodSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating begin method spec.");
        return hr;
    }

    *instruction = rewriterWrapper->CallMember(beginMethodSpec, false);
    return S_OK;
}

// endmethod with void return
HRESULT TracerTokens::WriteEndVoidReturnMemberRef(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                                      const TypeInfo* currentType, ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (endVoidMemberRef == mdMemberRefNil)
    {
        unsigned callTargetReturnVoidBuffer;
        auto callTargetReturnVoidSize = CorSigCompressToken(callTargetReturnVoidTypeRef, &callTargetReturnVoidBuffer);

        unsigned exTypeRefBuffer;
        auto exTypeRefSize = CorSigCompressToken(exTypeRef, &exTypeRefBuffer);

        unsigned callTargetStateBuffer;
        auto callTargetStateSize = CorSigCompressToken(callTargetStateTypeRef, &callTargetStateBuffer);

        auto signatureLength = 8 + callTargetReturnVoidSize + exTypeRefSize + callTargetStateSize;
        if (enable_calltarget_state_by_ref)
        {
            signatureLength++;
        }

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x02;
        signature[offset++] = 0x03;

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetReturnVoidBuffer, callTargetReturnVoidSize);
        offset += callTargetReturnVoidSize;

        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x01;

        signature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&signature[offset], &exTypeRefBuffer, exTypeRefSize);
        offset += exTypeRefSize;

        if (enable_calltarget_state_by_ref)
        {
            signature[offset++] = ELEMENT_TYPE_BYREF;
        }

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(callTargetTypeRef,
                                                                  managed_profiler_calltarget_endmethod_name.data(),
                                                                  signature, signatureLength, &endVoidMemberRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper endVoidMemberRef could not be defined.");
            return hr;
        }
    }

    mdMethodSpec endVoidMethodSpec = mdMethodSpecNil;

    unsigned integrationTypeBuffer;
    ULONG integrationTypeSize = CorSigCompressToken(integrationTypeRef, &integrationTypeBuffer);

    bool isValueType = currentType->valueType;
    mdToken currentTypeRef = GetCurrentTypeRef(currentType, isValueType);
    if (currentTypeRef == mdTokenNil)
    {
        isValueType = false;
        currentTypeRef = GetObjectTypeRef();
    }

    unsigned currentTypeBuffer;
    ULONG currentTypeSize = CorSigCompressToken(currentTypeRef, &currentTypeBuffer);

    auto signatureLength = 4 + integrationTypeSize + currentTypeSize;
    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;
    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
    signature[offset++] = 0x02;

    signature[offset++] = ELEMENT_TYPE_CLASS;
    memcpy(&signature[offset], &integrationTypeBuffer, integrationTypeSize);
    offset += integrationTypeSize;

    if (isValueType)
    {
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    }
    else
    {
        signature[offset++] = ELEMENT_TYPE_CLASS;
    }
    memcpy(&signature[offset], &currentTypeBuffer, currentTypeSize);
    offset += currentTypeSize;

    hr = module_metadata->metadata_emit->DefineMethodSpec(endVoidMemberRef, signature, signatureLength,
                                                          &endVoidMethodSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating end void method method spec.");
        return hr;
    }

    *instruction = rewriterWrapper->CallMember(endVoidMethodSpec, false);
    return S_OK;
}

// endmethod with return type
HRESULT TracerTokens::WriteEndReturnMemberRef(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                                  const TypeInfo* currentType, TypeSignature* returnArgument,
                                                  ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();
    GetTargetReturnValueTypeRef(returnArgument);

    // *** Define base MethodMemberRef for the type

    mdMemberRef endMethodMemberRef = mdMemberRefNil;

    unsigned callTargetReturnTypeRefBuffer;
    auto callTargetReturnTypeRefSize = CorSigCompressToken(callTargetReturnTypeRef, &callTargetReturnTypeRefBuffer);

    unsigned exTypeRefBuffer;
    auto exTypeRefSize = CorSigCompressToken(exTypeRef, &exTypeRefBuffer);

    unsigned callTargetStateBuffer;
    auto callTargetStateSize = CorSigCompressToken(callTargetStateTypeRef, &callTargetStateBuffer);

    auto signatureLength = 14 + callTargetReturnTypeRefSize + exTypeRefSize + callTargetStateSize;
    if (enable_calltarget_state_by_ref)
    {
        signatureLength++;
    }

    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
    signature[offset++] = 0x03;
    signature[offset++] = 0x04;

    signature[offset++] = ELEMENT_TYPE_GENERICINST;
    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &callTargetReturnTypeRefBuffer, callTargetReturnTypeRefSize);
    offset += callTargetReturnTypeRefSize;
    signature[offset++] = 0x01;
    signature[offset++] = ELEMENT_TYPE_MVAR;
    signature[offset++] = 0x02;

    signature[offset++] = ELEMENT_TYPE_MVAR;
    signature[offset++] = 0x01;

    signature[offset++] = ELEMENT_TYPE_MVAR;
    signature[offset++] = 0x02;

    signature[offset++] = ELEMENT_TYPE_CLASS;
    memcpy(&signature[offset], &exTypeRefBuffer, exTypeRefSize);
    offset += exTypeRefSize;

    if (enable_calltarget_state_by_ref)
    {
        signature[offset++] = ELEMENT_TYPE_BYREF;
    }
    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
    offset += callTargetStateSize;

    hr = module_metadata->metadata_emit->DefineMemberRef(callTargetTypeRef,
                                                         managed_profiler_calltarget_endmethod_name.data(), signature,
                                                         signatureLength, &endMethodMemberRef);
    if (FAILED(hr))
    {
        Logger::Warn("Wrapper endMethodMemberRef could not be defined.");
        return hr;
    }

    // *** Define Method Spec

    mdMethodSpec endMethodSpec = mdMethodSpecNil;

    unsigned integrationTypeBuffer;
    ULONG integrationTypeSize = CorSigCompressToken(integrationTypeRef, &integrationTypeBuffer);

    bool isValueType = currentType->valueType;
    mdToken currentTypeRef = GetCurrentTypeRef(currentType, isValueType);
    if (currentTypeRef == mdTokenNil)
    {
        isValueType = false;
        currentTypeRef = GetObjectTypeRef();
    }

    unsigned currentTypeBuffer;
    ULONG currentTypeSize = CorSigCompressToken(currentTypeRef, &currentTypeBuffer);

    PCCOR_SIGNATURE returnSignatureBuffer;
    auto returnSignatureLength = returnArgument->GetSignature(returnSignatureBuffer);

    signatureLength = 4 + integrationTypeSize + currentTypeSize + returnSignatureLength;
    offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
    signature[offset++] = 0x03;

    signature[offset++] = ELEMENT_TYPE_CLASS;
    memcpy(&signature[offset], &integrationTypeBuffer, integrationTypeSize);
    offset += integrationTypeSize;

    if (isValueType)
    {
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    }
    else
    {
        signature[offset++] = ELEMENT_TYPE_CLASS;
    }
    memcpy(&signature[offset], &currentTypeBuffer, currentTypeSize);
    offset += currentTypeSize;

    memcpy(&signature[offset], returnSignatureBuffer, returnSignatureLength);
    offset += returnSignatureLength;

    hr = module_metadata->metadata_emit->DefineMethodSpec(endMethodMemberRef, signature, signatureLength,
                                                          &endMethodSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating end method member spec.");
        return hr;
    }

    *instruction = rewriterWrapper->CallMember(endMethodSpec, false);
    return S_OK;
}

// write log exception
HRESULT TracerTokens::WriteLogException(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                            const TypeInfo* currentType, ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (logExceptionRef == mdMemberRefNil)
    {
        unsigned exTypeRefBuffer;
        auto exTypeRefSize = CorSigCompressToken(exTypeRef, &exTypeRefBuffer);

        auto signatureLength = 5 + exTypeRefSize;
        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x02;
        signature[offset++] = 0x01;

        signature[offset++] = ELEMENT_TYPE_VOID;
        signature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&signature[offset], &exTypeRefBuffer, exTypeRefSize);
        offset += exTypeRefSize;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(callTargetTypeRef,
                                                                  managed_profiler_calltarget_logexception_name.data(),
                                                                  signature, signatureLength, &logExceptionRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper methodLogExceptionRef could not be defined.");
            return hr;
        }
    }

    mdMethodSpec logExceptionMethodSpec = mdMethodSpecNil;

    unsigned integrationTypeBuffer;
    ULONG integrationTypeSize = CorSigCompressToken(integrationTypeRef, &integrationTypeBuffer);

    bool isValueType = currentType->valueType;
    mdToken currentTypeRef = GetCurrentTypeRef(currentType, isValueType);
    if (currentTypeRef == mdTokenNil)
    {
        isValueType = false;
        currentTypeRef = GetObjectTypeRef();
    }

    unsigned currentTypeBuffer;
    ULONG currentTypeSize = CorSigCompressToken(currentTypeRef, &currentTypeBuffer);

    auto signatureLength = 4 + integrationTypeSize + currentTypeSize;
    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;
    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
    signature[offset++] = 0x02;

    signature[offset++] = ELEMENT_TYPE_CLASS;
    memcpy(&signature[offset], &integrationTypeBuffer, integrationTypeSize);
    offset += integrationTypeSize;

    if (isValueType)
    {
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    }
    else
    {
        signature[offset++] = ELEMENT_TYPE_CLASS;
    }
    memcpy(&signature[offset], &currentTypeBuffer, currentTypeSize);
    offset += currentTypeSize;

    hr = module_metadata->metadata_emit->DefineMethodSpec(logExceptionRef, signature, signatureLength,
                                                          &logExceptionMethodSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating log exception method spec.");
        return hr;
    }

    *instruction = rewriterWrapper->CallMember(logExceptionMethodSpec, false);
    return S_OK;
}

} // namespace trace
