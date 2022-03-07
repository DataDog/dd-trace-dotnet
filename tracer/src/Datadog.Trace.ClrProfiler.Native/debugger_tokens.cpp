#include "debugger_tokens.h"

#include "dd_profiler_constants.h"
#include "il_rewriter_wrapper.h"
#include "logger.h"
#include "module_metadata.h"

using namespace shared;

namespace debugger
{

const int signatureBufferSize = 500;

/**
 * CALLTARGET CONSTANTS
 **/

static const WSTRING managed_profiler_debugger_beginmethod_startmarker_name = WStr("BeginMethod_StartMarker");
static const WSTRING managed_profiler_debugger_beginmethod_endmarker_name = WStr("BeginMethod_EndMarker");
static const WSTRING managed_profiler_debugger_endmethod_startmarker_name = WStr("EndMethod_StartMarker");
static const WSTRING managed_profiler_debugger_endmethod_endmarker_name = WStr("EndMethod_EndMarker");
static const WSTRING managed_profiler_debugger_logexception_name = WStr("LogException");
static const WSTRING managed_profiler_debugger_logarg_name = WStr("LogArg");
static const WSTRING managed_profiler_debugger_loglocal_name = WStr("LogLocal");
static const WSTRING managed_profiler_debugger_type = WStr("Datadog.Trace.Debugger.DebuggerInvoker");
static const WSTRING managed_profiler_debugger_statetype = WStr("Datadog.Trace.Debugger.DebuggerState");
static const WSTRING managed_profiler_debugger_returntype = WStr("Datadog.Trace.Debugger.DebuggerReturn");
static const WSTRING managed_profiler_debugger_returntype_generics = WStr("Datadog.Trace.Debugger.DebuggerReturn`1");

/**
 * PRIVATE
 **/

HRESULT DebuggerTokens::WriteLogArgOrLocal(void* rewriterWrapperPtr, const TypeSignature& argOrLocal,
                                           ILInstr** instruction, bool isArg)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    mdMemberRef logArgOrLocalRef = isArg ? logArgRef : logLocalRef;

    if (logArgOrLocalRef == mdMemberRefNil)
    {
        auto targetMemberName =
            isArg ? managed_profiler_debugger_logarg_name.data() : managed_profiler_debugger_loglocal_name.data();
        unsigned callTargetStateBuffer;
        auto callTargetStateSize = CorSigCompressToken(callTargetStateTypeRef, &callTargetStateBuffer);

        unsigned long signatureLength = 10 + callTargetStateSize;

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x01; // one generic argOrLocal (of the argOrLocal)
        signature[offset++] = 0x03; // (argumentIndex, argOrLocal, DebuggerState)

        signature[offset++] = ELEMENT_TYPE_VOID;

        // the argOrLocal
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x00;

        // argumentIndex
        signature[offset++] = ELEMENT_TYPE_I4;

        // DebuggerState
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(callTargetTypeRef, targetMemberName, signature,
                                                                  signatureLength, &logArgOrLocalRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper ", isArg ? "logArgRef" : "logLocalRef", " could not be defined.");
            return hr;
        }

        // Set the appropriate field
        if (isArg)
        {
            logArgRef = logArgOrLocalRef;
        }
        else
        {
            logLocalRef = logArgOrLocalRef;
        }
    }

    mdMethodSpec logArgMethodSpec = mdMethodSpecNil;

    auto signatureLength = 2;

    PCCOR_SIGNATURE argumentSignatureBuffer;
    ULONG argumentSignatureSize;
    const auto [elementType, argTypeFlags] = argOrLocal.GetElementTypeAndFlags();
    if (argTypeFlags & TypeFlagByRef)
    {
        PCCOR_SIGNATURE argSigBuff;
        auto signatureSize = argOrLocal.GetSignature(argSigBuff);
        if (argSigBuff[0] == ELEMENT_TYPE_BYREF)
        {
            argumentSignatureBuffer = argSigBuff + 1;
            argumentSignatureSize = signatureSize - 1;
            signatureLength += signatureSize - 1;
        }
        else
        {
            argumentSignatureBuffer = argSigBuff;
            argumentSignatureSize = signatureSize;
            signatureLength += signatureSize;
        }
    }
    else
    {
        auto signatureSize = argOrLocal.GetSignature(argumentSignatureBuffer);
        argumentSignatureSize = signatureSize;
        signatureLength += signatureSize;
    }

    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;
    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
    signature[offset++] = 0x01;

    memcpy(&signature[offset], argumentSignatureBuffer, argumentSignatureSize);
    offset += argumentSignatureSize;

    hr = module_metadata->metadata_emit->DefineMethodSpec(logArgOrLocalRef, signature, signatureLength,
                                                          &logArgMethodSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating log exception method spec.");
        return hr;
    }

    *instruction = rewriterWrapper->CallMember(logArgMethodSpec, false);
    return S_OK;
}

/**
 * PROTECTED
 **/

const WSTRING& DebuggerTokens::GetCallTargetType()
{
    return managed_profiler_debugger_type;
}

const WSTRING& DebuggerTokens::GetCallTargetStateType()
{
    return managed_profiler_debugger_statetype;
}

const WSTRING& DebuggerTokens::GetCallTargetReturnType()
{
    return managed_profiler_debugger_returntype;
}

const WSTRING& DebuggerTokens::GetCallTargetReturnGenericType()
{
    return managed_profiler_debugger_returntype_generics;
}

/**
 * PUBLIC
 **/

DebuggerTokens::DebuggerTokens(ModuleMetadata* module_metadata_ptr) :
    CallTargetTokens(module_metadata_ptr, true, true)
{
}

HRESULT DebuggerTokens::WriteBeginMethod_StartMarker(void* rewriterWrapperPtr, const TypeInfo* currentType,
                                                     ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (beginMethodStartMarkerRef == mdMemberRefNil)
    {
        unsigned callTargetStateBuffer;
        auto callTargetStateSize = CorSigCompressToken(callTargetStateTypeRef, &callTargetStateBuffer);

        unsigned long signatureLength = 8 + callTargetStateSize;

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x01; // generic arguments count
        signature[offset++] = 0x03; // arguments count

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;

        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x00;

        signature[offset++] = ELEMENT_TYPE_U4; // methodMetadataToken
        signature[offset++] = ELEMENT_TYPE_I4; // methodMetadataIndex

        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            callTargetTypeRef, managed_profiler_debugger_beginmethod_startmarker_name.data(), signature,
            signatureLength, &beginMethodStartMarkerRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper beginMethod could not be defined.");
            return hr;
        }
    }

    mdMethodSpec beginMethodSpec = mdMethodSpecNil;

    bool isValueType = currentType->valueType;
    mdToken currentTypeRef = GetCurrentTypeRef(currentType, isValueType);

    unsigned currentTypeBuffer;
    ULONG currentTypeSize = CorSigCompressToken(currentTypeRef, &currentTypeBuffer);

    auto signatureLength = 3 + currentTypeSize;

    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
    signature[offset++] = 0x01;

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

    hr = module_metadata->metadata_emit->DefineMethodSpec(beginMethodStartMarkerRef, signature, signatureLength,
                                                          &beginMethodSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating begin method spec.");
        return hr;
    }

    *instruction = rewriterWrapper->CallMember(beginMethodSpec, false);
    return S_OK;
}

// endmethod with void return
HRESULT DebuggerTokens::WriteEndVoidReturnMemberRef(void* rewriterWrapperPtr, const TypeInfo* currentType,
                                                    ILInstr** instruction)
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
        signatureLength++; // ByRef

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x01;
        signature[offset++] = 0x03;

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetReturnVoidBuffer, callTargetReturnVoidSize);
        offset += callTargetReturnVoidSize;

        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x00;

        signature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&signature[offset], &exTypeRefBuffer, exTypeRefSize);
        offset += exTypeRefSize;

        signature[offset++] = ELEMENT_TYPE_BYREF;

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            callTargetTypeRef, managed_profiler_debugger_endmethod_startmarker_name.data(), signature, signatureLength,
            &endVoidMemberRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper endVoidMemberRef could not be defined.");
            return hr;
        }
    }

    mdMethodSpec endVoidMethodSpec = mdMethodSpecNil;

    bool isValueType = currentType->valueType;
    mdToken currentTypeRef = GetCurrentTypeRef(currentType, isValueType);

    unsigned currentTypeBuffer;
    ULONG currentTypeSize = CorSigCompressToken(currentTypeRef, &currentTypeBuffer);

    auto signatureLength = 3 + currentTypeSize;
    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;
    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
    signature[offset++] = 0x01;

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
HRESULT DebuggerTokens::WriteEndReturnMemberRef(void* rewriterWrapperPtr, const TypeInfo* currentType,
                                                TypeSignature* returnArgument, ILInstr** instruction)
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
    signatureLength++; // ByRef

    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
    signature[offset++] = 0x02;
    signature[offset++] = 0x04;

    signature[offset++] = ELEMENT_TYPE_GENERICINST;
    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &callTargetReturnTypeRefBuffer, callTargetReturnTypeRefSize);
    offset += callTargetReturnTypeRefSize;
    signature[offset++] = 0x01;
    signature[offset++] = ELEMENT_TYPE_MVAR;
    signature[offset++] = 0x01;

    signature[offset++] = ELEMENT_TYPE_MVAR;
    signature[offset++] = 0x00;

    signature[offset++] = ELEMENT_TYPE_MVAR;
    signature[offset++] = 0x01;

    signature[offset++] = ELEMENT_TYPE_CLASS;
    memcpy(&signature[offset], &exTypeRefBuffer, exTypeRefSize);
    offset += exTypeRefSize;

    signature[offset++] = ELEMENT_TYPE_BYREF;
    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
    offset += callTargetStateSize;

    hr = module_metadata->metadata_emit->DefineMemberRef(callTargetTypeRef,
                                                         managed_profiler_debugger_endmethod_startmarker_name.data(),
                                                         signature, signatureLength, &endMethodMemberRef);
    if (FAILED(hr))
    {
        Logger::Warn("Wrapper endMethodMemberRef could not be defined.");
        return hr;
    }

    // *** Define Method Spec

    mdMethodSpec endMethodSpec = mdMethodSpecNil;

    bool isValueType = currentType->valueType;
    mdToken currentTypeRef = GetCurrentTypeRef(currentType, isValueType);

    unsigned currentTypeBuffer;
    ULONG currentTypeSize = CorSigCompressToken(currentTypeRef, &currentTypeBuffer);

    PCCOR_SIGNATURE returnSignatureBuffer;
    auto returnSignatureLength = returnArgument->GetSignature(returnSignatureBuffer);

    signatureLength = 3 + currentTypeSize + returnSignatureLength;
    offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
    signature[offset++] = 0x02;

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
HRESULT DebuggerTokens::WriteLogException(void* rewriterWrapperPtr, const TypeInfo* currentType)
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

        unsigned callTargetStateBuffer;
        auto callTargetStateSize = CorSigCompressToken(callTargetStateTypeRef, &callTargetStateBuffer);

        auto signatureLength = 7 + exTypeRefSize + callTargetStateSize;
        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x01; // One generic argument: TTarget
        signature[offset++] = 0x02; // (Exception, DebuggerState)

        signature[offset++] = ELEMENT_TYPE_VOID;
        signature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&signature[offset], &exTypeRefBuffer, exTypeRefSize);
        offset += exTypeRefSize;

        // DebuggerState
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(callTargetTypeRef,
                                                                  managed_profiler_debugger_logexception_name.data(),
                                                                  signature, signatureLength, &logExceptionRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper logExceptionRef could not be defined.");
            return hr;
        }
    }

    mdMethodSpec logExceptionMethodSpec = mdMethodSpecNil;

    bool isValueType = currentType->valueType;
    mdToken currentTypeRef = GetCurrentTypeRef(currentType, isValueType);

    unsigned currentTypeBuffer;
    ULONG currentTypeSize = CorSigCompressToken(currentTypeRef, &currentTypeBuffer);

    auto signatureLength = 3 + currentTypeSize;
    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;
    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
    signature[offset++] = 0x01;

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

    rewriterWrapper->CallMember(logExceptionMethodSpec, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteLogArg(void* rewriterWrapperPtr, const TypeSignature& argument, ILInstr** instruction)
{
    return WriteLogArgOrLocal(rewriterWrapperPtr, argument, instruction, true /* isArg */);
}

HRESULT DebuggerTokens::WriteLogLocal(void* rewriterWrapperPtr, const TypeSignature& local, ILInstr** instruction)
{
    return WriteLogArgOrLocal(rewriterWrapperPtr, local, instruction, false /* isArg */);
}

HRESULT DebuggerTokens::WriteBeginOrEndMethod_EndMarker(void* rewriterWrapperPtr, bool isBeginMethod,
                                                        ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    mdMemberRef beginOrEndMethodRef = isBeginMethod ? beginMethodEndMarkerRef : endMethodEndMarkerRef;
    const auto beginOrEndMethodName = isBeginMethod ? managed_profiler_debugger_beginmethod_endmarker_name.data()
                                                    : managed_profiler_debugger_endmethod_endmarker_name.data();

    if (beginOrEndMethodRef == mdMemberRefNil)
    {
        unsigned callTargetStateBuffer;
        auto callTargetStateSize = CorSigCompressToken(callTargetStateTypeRef, &callTargetStateBuffer);

        unsigned long signatureLength = 4 + callTargetStateSize;

        signatureLength += 1; // ByRef

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x01; // arguments count
        signature[offset++] = ELEMENT_TYPE_VOID;

        // DebuggerState
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(callTargetTypeRef, beginOrEndMethodName, signature,
                                                                  signatureLength, &beginOrEndMethodRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper beginMethod could not be defined.");
            return hr;
        }

        if (isBeginMethod)
        {
            beginMethodEndMarkerRef = beginOrEndMethodRef;
        }
        else
        {
            endMethodEndMarkerRef = beginOrEndMethodRef;
        }
    }

    *instruction = rewriterWrapper->CallMember(beginOrEndMethodRef, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteBeginMethod_EndMarker(void* rewriterWrapperPtr, ILInstr** instruction)
{
    return WriteBeginOrEndMethod_EndMarker(rewriterWrapperPtr, true /* isBeginMethod */, instruction);
}

HRESULT DebuggerTokens::WriteEndMethod_EndMarker(void* rewriterWrapperPtr, ILInstr** instruction)
{
    return WriteBeginOrEndMethod_EndMarker(rewriterWrapperPtr, false /* isBeginMethod */, instruction);
}
} // namespace debugger