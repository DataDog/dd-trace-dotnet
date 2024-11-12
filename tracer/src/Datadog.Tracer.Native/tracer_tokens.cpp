#include "tracer_tokens.h"

#include "dd_profiler_constants.h"
#include "il_rewriter_wrapper.h"
#include "logger.h"
#include "module_metadata.h"
#include "cor_profiler.h"

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
static const shared::WSTRING managed_profiler_calltarget_refstruct = WStr("Datadog.Trace.ClrProfiler.CallTarget.CallTargetRefStruct");

static const shared::WSTRING managed_profiler_calltarget_beginmethod_name = WStr("BeginMethod");
static const shared::WSTRING managed_profiler_calltarget_endmethod_name = WStr("EndMethod");
static const shared::WSTRING managed_profiler_calltarget_logexception_name = WStr("LogException");
static const shared::WSTRING managed_profiler_calltarget_createrefstruct_name = WStr("CreateRefStruct");

static const shared::WSTRING managed_profiler_trace_attribute_type = WStr("Datadog.Trace.Annotations.TraceAttribute");

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

const shared::WSTRING& TracerTokens::GetCallTargetRefStructType()
{
    return managed_profiler_calltarget_refstruct;
}

HRESULT TracerTokens::EnsureBaseCalltargetTokens()
{
    std::lock_guard<std::recursive_mutex> guard(metadata_mutex);

    HRESULT hr = CallTargetTokens::EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    const ModuleMetadata* module_metadata = GetMetadata();

    // *** Ensure Datadog.Trace.ClrProfiler.CallTarget.CallTargetBubbleUpException type ref, might not be available if tracer version is < 2.22
    if (profiler->IsCallTargetBubbleUpExceptionTypeAvailable() && bubbleUpExceptionTypeRef == mdTypeRefNil)
    {
        const auto defined_calltargetbubbleup_byname_hrresult = module_metadata->metadata_emit->DefineTypeRefByName(profilerAssemblyRef,
                                                            calltargetbubbleexception_tracer_type_name.c_str(),
                                                            &bubbleUpExceptionTypeRef);
        if (SUCCEEDED(defined_calltargetbubbleup_byname_hrresult))
        {
            if (profiler->IsCallTargetBubbleUpFunctionAvailable() && bubbleUpExceptionFunctionRef == mdMemberRefNil)
            {
                // now reference the method IsCallTargetBubbleUpException in the type's reference
                COR_SIGNATURE createInstanceSig[32];
                COR_SIGNATURE* sigBuilder = createInstanceSig;
                sigBuilder += CorSigCompressData(IMAGE_CEE_CS_CALLCONV_DEFAULT, sigBuilder); // static
                sigBuilder += CorSigCompressData(1, sigBuilder);                             // arguments count
                sigBuilder += CorSigCompressElementType(ELEMENT_TYPE_BOOLEAN, sigBuilder);   // return type
                sigBuilder += CorSigCompressElementType(ELEMENT_TYPE_CLASS, sigBuilder);     // argument type
                sigBuilder += CorSigCompressToken(exTypeRef, sigBuilder);

                const auto defined_iscalltargetbubbleup_member_hrresult = module_metadata->metadata_emit->
                    DefineMemberRef(
                        bubbleUpExceptionTypeRef, calltargetbubbleexception_tracer_function_name.c_str(),
                        createInstanceSig,
                        sigBuilder - createInstanceSig, &bubbleUpExceptionFunctionRef);
               
                if (SUCCEEDED(defined_iscalltargetbubbleup_member_hrresult))
                {
                    Logger::Debug("Defined function ", calltargetbubbleexception_tracer_function_name, " on ",
                             calltargetbubbleexception_tracer_type_name, " type: ",
                             defined_iscalltargetbubbleup_member_hrresult);
                }
                else
                {
                    bubbleUpExceptionFunctionRef = mdMemberRefNil;
                }
            }
        }
        else
        {
            bubbleUpExceptionTypeRef = mdTypeRefNil;
        }
    }

    if (callTargetTypeRef != mdTypeRefNil && createRefStructMemberRef == mdMemberRefNil)
    {
        COR_SIGNATURE createInstanceSig[32];
        COR_SIGNATURE* sigBuilder = createInstanceSig;
        sigBuilder += CorSigCompressData(IMAGE_CEE_CS_CALLCONV_DEFAULT, sigBuilder); // static
        sigBuilder += CorSigCompressData(2, sigBuilder);                             // arguments count
        sigBuilder += CorSigCompressElementType(ELEMENT_TYPE_VALUETYPE, sigBuilder);   // return type
        sigBuilder += CorSigCompressToken(callTargetRefStructTypeRef, sigBuilder);
        sigBuilder += CorSigCompressElementType(ELEMENT_TYPE_PTR, sigBuilder);           // ptr
        sigBuilder += CorSigCompressElementType(ELEMENT_TYPE_VOID, sigBuilder);           // void
        sigBuilder += CorSigCompressElementType(ELEMENT_TYPE_VALUETYPE, sigBuilder);     // RuntimeTypeHandle type
        sigBuilder += CorSigCompressToken(runtimeTypeHandleRef, sigBuilder);

        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            callTargetTypeRef, managed_profiler_calltarget_createrefstruct_name.data(), createInstanceSig, sigBuilder - createInstanceSig,
            &createRefStructMemberRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper CreateRefStruct could not be defined.");
            return hr;
        }
    }

    return hr;
}

int TracerTokens::GetAdditionalLocalsCount(const std::vector<TypeSignature>& methodTypeArguments)
{
    int refStructCount = 0;
    if (enable_by_ref_instrumentation)
    {
        const auto module_metadata = GetMetadata();
        for (auto const& typeArgument : methodTypeArguments)
        {
            bool isByRefLike = false;
            if (SUCCEEDED(IsTypeByRefLike(_profiler_info, *module_metadata, typeArgument, GetCorLibAssemblyRef(), isByRefLike)) &&
                isByRefLike)
            {
                refStructCount++;
            }
        }
    }

    // 2 for the exception variable caught by the filter CallTargetBubbleUpException for begin and end methods
    // with a filter, the catch handler needs to load the exception in the eval. stack, it's not available by default anymore
    return 2 + refStructCount;
}

void TracerTokens::AddAdditionalLocals(TypeSignature* methodReturnValue, std::vector<TypeSignature>* methodTypeArguments,
                                       COR_SIGNATURE (&signatureBuffer)[BUFFER_SIZE], ULONG& signatureOffset,
                                       ULONG& signatureSize, bool isAsyncMethod)
{
    // Gets the exception type buffer and size
    unsigned exTypeRefBuffer;
    auto exTypeRefSize = CorSigCompressToken(exTypeRef, &exTypeRefBuffer);
    
    // Exception value for calltarget exception filters
    signatureBuffer[signatureOffset++] = ELEMENT_TYPE_CLASS;
    memcpy(&signatureBuffer[signatureOffset], &exTypeRefBuffer, exTypeRefSize);
    signatureOffset += exTypeRefSize;
    signatureSize += 1 + exTypeRefSize;


    signatureBuffer[signatureOffset++] = ELEMENT_TYPE_CLASS;
    memcpy(&signatureBuffer[signatureOffset], &exTypeRefBuffer, exTypeRefSize);
    signatureOffset += exTypeRefSize;
    signatureSize += 1 + exTypeRefSize;

    if (enable_by_ref_instrumentation)
    {
        // Adds the CallTargetRefStruct locals for each ref struct argument in the method
        unsigned callTargetRefStructTypeRefBuffer;
        auto callTargetRefStructTypeRefSize = CorSigCompressToken(callTargetRefStructTypeRef, &callTargetRefStructTypeRefBuffer);

        const auto module_metadata = GetMetadata();
        for (auto const& typeArgument : *methodTypeArguments)
        {
            bool isByRefLike = false;
            if (SUCCEEDED(IsTypeByRefLike(_profiler_info, *module_metadata, typeArgument, GetCorLibAssemblyRef(), isByRefLike)) &&
                isByRefLike)
            {
                signatureBuffer[signatureOffset++] = ELEMENT_TYPE_VALUETYPE;
                memcpy(&signatureBuffer[signatureOffset], &callTargetRefStructTypeRefBuffer, callTargetRefStructTypeRefSize);
                signatureOffset += callTargetRefStructTypeRefSize;
                signatureSize += 1 + callTargetRefStructTypeRefSize;
            }
        }
    }
}

/**
 * PUBLIC
 **/

TracerTokens::TracerTokens(ModuleMetadata* module_metadata_ptr, const bool enableByRefInstrumentation,
                           const bool enableCallTargetStateByRef) :
    CallTargetTokens(module_metadata_ptr, enableByRefInstrumentation, enableCallTargetStateByRef)
{
    callTargetRefStructTypeRef = mdTypeRefNil;
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

    const auto dynamicSignatureAllocation = new std::vector<COR_SIGNATURE*>();
    PCCOR_SIGNATURE argumentsSignatureBuffer[FASTPATH_COUNT];
    ULONG argumentsSignatureSize[FASTPATH_COUNT];
    for (auto i = 0; i < numArguments; i++)
    {
        const auto [elementType, argTypeFlags] = methodArguments[i].GetElementTypeAndFlags();

        bool isByRefLike = false;
        if (FAILED(IsTypeByRefLike(_profiler_info, *module_metadata, methodArguments[i], GetCorLibAssemblyRef(), isByRefLike)))
        {
            isByRefLike = false;
        }

        if (enable_by_ref_instrumentation && isByRefLike)
        {
            unsigned calltargetRefStructTypeBuffer;
            ULONG calltargetRefStructTypeSize = CorSigCompressToken(callTargetRefStructTypeRef, &calltargetRefStructTypeBuffer);

            auto argSignature = new COR_SIGNATURE[calltargetRefStructTypeSize + 1];
            dynamicSignatureAllocation->push_back(argSignature);

            argSignature[0] = ELEMENT_TYPE_VALUETYPE;
            memcpy(&argSignature[1], &calltargetRefStructTypeBuffer, calltargetRefStructTypeSize);

            argumentsSignatureBuffer[i] = argSignature;
            argumentsSignatureSize[i] = calltargetRefStructTypeSize + 1;
            signatureLength += calltargetRefStructTypeSize + 1;
        }
        else if (enable_by_ref_instrumentation && (argTypeFlags & TypeFlagByRef))
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

    // freeing the dynamic signature allocation
    for (auto i = 0; i < dynamicSignatureAllocation->size(); i++)
    {
        delete dynamicSignatureAllocation->at(i);
    }

    delete dynamicSignatureAllocation;

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
    ILInstr* call_instruction = rewriterWrapper->CallMember(logExceptionMethodSpec, false);
    // if we have an exception filter, this instruction can't be the first as the filter needs to pop and load first (with a filter, catch clauses dont automatically get the exception in the eval. stack)
    if (*instruction == nullptr)
    {
        *instruction = call_instruction;
    }
    return S_OK;
}

mdTypeRef TracerTokens::GetBubbleUpExceptionTypeRef() const
{
    return bubbleUpExceptionTypeRef;
}

mdMemberRef TracerTokens::GetBubbleUpExceptionFunctionDef() const
{
    return bubbleUpExceptionFunctionRef;
}

const shared::WSTRING& TracerTokens::GetTraceAttributeType()
{
    return managed_profiler_trace_attribute_type;
}

void TracerTokens::SetCorProfilerInfo(ICorProfilerInfo4* profilerInfo)
{
    _profiler_info = profilerInfo;
}

HRESULT TracerTokens::WriteRefStructCall(void* rewriterWrapperPtr, mdTypeRef refStructTypeRef, int refStructIndex)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;

    if (createRefStructMemberRef == mdMemberRefNil)
    {
        Logger::Error("CreateRefStruct memberRef is null.");
        return E_FAIL;
    }

    rewriterWrapper->CreateInstr(CEE_CONV_U);
    rewriterWrapper->LoadToken(refStructTypeRef);
    rewriterWrapper->CallMember(createRefStructMemberRef, false);
    rewriterWrapper->StLocal(refStructIndex);
    rewriterWrapper->LoadLocalAddress(refStructIndex);
    return S_OK;
}

} // namespace trace
