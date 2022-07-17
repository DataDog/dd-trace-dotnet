#include "debugger_tokens.h"

#include <utility>

#include "dd_profiler_constants.h"
#include "il_rewriter_wrapper.h"
#include "logger.h"
#include "module_metadata.h"

using namespace shared;

namespace debugger
{

const int signatureBufferSize = 500;

/**
 * PRIVATE
 **/

HRESULT DebuggerTokens::WriteLogArgOrLocal(void* rewriterWrapperPtr, const TypeSignature& argOrLocal,
                                           ILInstr** instruction, bool isArg, ProbeType probeType)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    mdMemberRef logArgOrLocalRef;
    auto [invokerTypeRef, stateTypeRef] = GetDebuggerInvokerAndState(probeType);

    if (isArg)
    {
        logArgOrLocalRef = GetLogArgMemberRef(probeType);
    }
    else
    {
        logArgOrLocalRef = GetLogLocalMemberRef(probeType);
    }
    
    if (logArgOrLocalRef == mdMemberRefNil)
    {
        auto targetMemberName =
            isArg ? managed_profiler_debugger_logarg_name.data() : managed_profiler_debugger_loglocal_name.data();
        unsigned callTargetStateBuffer;
        auto callTargetStateSize = CorSigCompressToken(stateTypeRef, &callTargetStateBuffer);

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
        signature[offset++] = probeType == AsyncMethod ? ELEMENT_TYPE_CLASS : ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(invokerTypeRef, targetMemberName, signature,
                                                                  signatureLength, &logArgOrLocalRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper ", isArg ? "methodLogArgRef" : "methodLogLocalRef", " could not be defined.");
            return hr;
        }

        // Set the appropriate field
        if (isArg)
        {
            SetLogArgMemberRef(probeType, logArgOrLocalRef);
        }
        else
        {
            SetLogLocalMemberRef(probeType, logArgOrLocalRef);
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

HRESULT DebuggerTokens::EnsureBaseCalltargetTokens()
{
    auto hr = CallTargetTokens::EnsureBaseCalltargetTokens();

    IfFailRet(hr);

    ModuleMetadata* module_metadata = GetMetadata();

    // *** Ensure lineInvoker type ref
    if (lineInvokerTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_debugger_line_type.data(), &lineInvokerTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper lineInvokerTypeRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure lineDebuggerState type ref
    if (lineDebuggerStateTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_debugger_linestatetype.data(), &lineDebuggerStateTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper lineDebuggerStateTypeRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure AsyncMethodDebuggerInvoker type ref
    if (asyncMethodDebuggerInvokerTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_debugger_async_method_invoker_type.data(), &asyncMethodDebuggerInvokerTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper asyncMethodDebuggerInvokerTypeRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure AsyncMethodDebuggerState type ref
    if (asyncMethodDebuggerStateTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_debugger_async_method_state_type.data(), &asyncMethodDebuggerStateTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper asyncMethodDebuggerStateTypeRef could not be defined.");
            return hr;
        }
    }

    return S_OK;
}

/**
 * PROTECTED
 **/

const WSTRING& DebuggerTokens::GetCallTargetType()
{
    return managed_profiler_debugger_method_type;
}

const WSTRING& DebuggerTokens::GetCallTargetStateType()
{
    return managed_profiler_debugger_methodstatetype;
}

const WSTRING& DebuggerTokens::GetCallTargetReturnType()
{
    return managed_profiler_debugger_returntype;
}

const WSTRING& DebuggerTokens::GetCallTargetReturnGenericType()
{
    return managed_profiler_debugger_returntype_generics;
}

int DebuggerTokens::GetAdditionalLocalsCount()
{
    return 2;
}

void DebuggerTokens::AddAdditionalLocals(COR_SIGNATURE (&signatureBuffer)[500], ULONG& signatureOffset, ULONG& signatureSize)
{
    // Gets the calltarget state of line probe type buffer and size
    unsigned callTargetStateTypeRefBuffer;
    const auto callTargetStateTypeRefSize = CorSigCompressToken(lineDebuggerStateTypeRef, &callTargetStateTypeRefBuffer);

    // Enlarge the *new* signature size
    signatureSize += (1 + callTargetStateTypeRefSize);

    // CallTarget state of line probe
    signatureBuffer[signatureOffset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signatureBuffer[signatureOffset], &callTargetStateTypeRefBuffer, callTargetStateTypeRefSize);
    signatureOffset += callTargetStateTypeRefSize;

    // Gets the calltarget state of async method probe type buffer and size
    unsigned asyncMethodStateTypeRefBuffer;
    const auto asyncMethodStateTypeRefSize = CorSigCompressToken(asyncMethodDebuggerStateTypeRef, &asyncMethodStateTypeRefBuffer);

    // Enlarge the *new* signature size
    signatureSize += (1 + asyncMethodStateTypeRefSize);

    // CallTarget state of async method probe
    signatureBuffer[signatureOffset++] = ELEMENT_TYPE_CLASS;
    memcpy(&signatureBuffer[signatureOffset], &asyncMethodStateTypeRefBuffer, asyncMethodStateTypeRefSize);
    signatureOffset += asyncMethodStateTypeRefSize;
}

/**
 * PUBLIC
 **/

DebuggerTokens::DebuggerTokens(ModuleMetadata* module_metadata_ptr) :
    CallTargetTokens(module_metadata_ptr, true, true)
{
}

HRESULT DebuggerTokens::WriteBeginMethod_StartMarker(void* rewriterWrapperPtr, const TypeInfo* currentType,
                                                     ILInstr** instruction, ProbeType probeType)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    mdMemberRef beginMethodRef = GetBeginMethodStartMarker(probeType);

    if (beginMethodRef == mdMemberRefNil)
    {
        hr = CreateBeginMethodStartMarkerRefSignature(probeType, beginMethodRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper beginMethod could not be defined.");
            return hr;
        }
        SetBeginMethodStartMarker(probeType, beginMethodRef);
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

    hr = module_metadata->metadata_emit->DefineMethodSpec(beginMethodRef, signature, signatureLength,
                                                          &beginMethodSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating begin method spec.");
        return hr;
    }

    *instruction = rewriterWrapper->CallMember(beginMethodSpec, false);
    return S_OK;
}

HRESULT DebuggerTokens::CreateBeginMethodStartMarkerRefSignature(ProbeType probeType, mdMemberRef& beginMethodRef)
{
    auto [invokerTypeRef, stateTypeRef] = GetDebuggerInvokerAndState(probeType);
    unsigned callTargetStateBuffer;
    auto callTargetStateSize = CorSigCompressToken(stateTypeRef, &callTargetStateBuffer);

    unsigned runtimeMethodHandleBuffer;
    auto runtimeMethodHandleSize = CorSigCompressToken(runtimeMethodHandleRef, &runtimeMethodHandleBuffer);

    unsigned runtimeTypeHandleBuffer;
    auto runtimeTypeHandleSize = CorSigCompressToken(runtimeTypeHandleRef, &runtimeTypeHandleBuffer);

    unsigned long signatureLength = (probeType == AsyncMethod ? 12 : 10) + callTargetStateSize + runtimeMethodHandleSize + runtimeTypeHandleSize;

    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
    signature[offset++] = 0x01; // generic arguments count
    signature[offset++] = probeType == AsyncMethod ? 0x06 : 0x05; // arguments count

    signature[offset++] = probeType == AsyncMethod ? ELEMENT_TYPE_CLASS : ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
    offset += callTargetStateSize;

    signature[offset++] = ELEMENT_TYPE_STRING;

    signature[offset++] = ELEMENT_TYPE_MVAR;
    signature[offset++] = 0x00;

    // RuntimeMethodHandle
    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &runtimeMethodHandleBuffer, runtimeMethodHandleSize);
    offset += runtimeMethodHandleSize;

    // RuntimeTypeHandle
    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &runtimeTypeHandleBuffer, runtimeTypeHandleSize);
    offset += runtimeTypeHandleSize;

    signature[offset++] = ELEMENT_TYPE_I4; // methodMetadataIndex

    WSTRING methodName;
    if (probeType == AsyncMethod)
    {
        methodName = managed_profiler_debugger_begin_async_method_name;
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_BOOLEAN; // isFirstEntry
    }
    else
    {
        methodName = managed_profiler_debugger_beginmethod_startmarker_name;
    }

    return GetMetadata()->metadata_emit->DefineMemberRef(
        invokerTypeRef, methodName.data(), signature,
        signatureLength, &beginMethodRef);
}

// endmethod with void return
HRESULT DebuggerTokens::WriteEndVoidReturnMemberRef(void* rewriterWrapperPtr, const TypeInfo* currentType,
                                                    ILInstr** instruction,  ProbeType probeType)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    const ModuleMetadata* module_metadata = GetMetadata();

    mdMemberRef endMethodMemberRef = GetEndMethodStartMarker(probeType, true);

    if (endMethodMemberRef == mdMemberRefNil)
    {
        hr = CreateEndMethodStartMarkerRefSignature(probeType, endMethodMemberRef, callTargetReturnVoidTypeRef, true);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper endVoidMemberRef could not be defined.");
            return hr;
        }
        SetEndMethodStartMarker(probeType, true, endMethodMemberRef);
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

    hr = module_metadata->metadata_emit->DefineMethodSpec(endMethodMemberRef, signature, signatureLength,
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
                                                TypeSignature* returnArgument, ILInstr** instruction, ProbeType probeType)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    mdMemberRef endMethodMemberRef = GetEndMethodStartMarker(probeType, false);

    if (endMethodMemberRef == mdMemberRefNil)
    {
        hr = CreateEndMethodStartMarkerRefSignature(probeType, endMethodMemberRef, callTargetReturnTypeRef, false);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper endVoidMemberRef could not be defined.");
            return hr;
        }
        SetEndMethodStartMarker(probeType, false, endMethodMemberRef);
    }

    //todo: for what?
    // GetTargetReturnValueTypeRef(returnArgument);

    // *** Define Method Spec

    mdMethodSpec endMethodSpec = mdMethodSpecNil;

    bool isValueType = currentType->valueType;
    mdToken currentTypeRef = GetCurrentTypeRef(currentType, isValueType);

    unsigned currentTypeBuffer;
    ULONG currentTypeSize = CorSigCompressToken(currentTypeRef, &currentTypeBuffer);

    PCCOR_SIGNATURE returnSignatureBuffer;
    auto returnSignatureLength = returnArgument->GetSignature(returnSignatureBuffer);

    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;

    const auto signatureLength = 3 + currentTypeSize + returnSignatureLength;
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

HRESULT DebuggerTokens::CreateEndMethodStartMarkerRefSignature(ProbeType probeType, mdMemberRef& endMethodRef, mdTypeRef returnTypeRef, bool isVoid)
{
    auto [invokerTypeRef, stateTypeRef] = GetDebuggerInvokerAndState(probeType);

    unsigned returnTypeBuffer;
    const auto returnTypeSize = CorSigCompressToken(returnTypeRef, &returnTypeBuffer);

    unsigned exTypeRefBuffer;
    const auto exTypeRefSize = CorSigCompressToken(exTypeRef, &exTypeRefBuffer);

    unsigned callTargetStateBuffer;
    const auto callTargetStateSize = CorSigCompressToken(stateTypeRef, &callTargetStateBuffer);

    auto signatureLength = (isVoid ? 8 : 14) + returnTypeSize + exTypeRefSize + callTargetStateSize;
    signatureLength++; // ByRef

    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;

    if (isVoid)
    {
        signature[offset++] = 0x01;
        signature[offset++] = 0x03;
    }
    else
    {
        signature[offset++] = 0x02;
        signature[offset++] = 0x04;
        signature[offset++] = ELEMENT_TYPE_GENERICINST;
    }

    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &returnTypeBuffer, returnTypeSize);
    offset += returnTypeSize;

    if (!isVoid)
    {
        signature[offset++] = 0x01;
        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x01;
    }

    signature[offset++] = ELEMENT_TYPE_MVAR;
    signature[offset++] = 0x00;

    if (!isVoid)
    {
        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x01;
    }

    signature[offset++] = ELEMENT_TYPE_CLASS;
    memcpy(&signature[offset], &exTypeRefBuffer, exTypeRefSize);
    offset += exTypeRefSize;

    signature[offset++] = ELEMENT_TYPE_BYREF;

    signature[offset++] = probeType == AsyncMethod ? ELEMENT_TYPE_CLASS : ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
    offset += callTargetStateSize;

   return GetMetadata()->metadata_emit->DefineMemberRef(
        invokerTypeRef, managed_profiler_debugger_endmethod_startmarker_name.data(), signature, signatureLength,
        &endMethodRef);
}

// write log exception
HRESULT DebuggerTokens::WriteLogException(void* rewriterWrapperPtr, const TypeInfo* currentType, ProbeType probeType)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    mdTypeRef stateTypeRef = GetDebuggerState(probeType);
    mdTypeRef methodOrLineTypeRef = GetDebuggerInvoker(probeType);
    mdMemberRef logExceptionRef = GetLogExceptionMemberRef(probeType);

    if (logExceptionRef == mdMemberRefNil)
    {
        unsigned exTypeRefBuffer;
        auto exTypeRefSize = CorSigCompressToken(exTypeRef, &exTypeRefBuffer);

        unsigned callTargetStateBuffer;
        auto callTargetStateSize = CorSigCompressToken(stateTypeRef, &callTargetStateBuffer);

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
        signature[offset++] = probeType == AsyncMethod ? ELEMENT_TYPE_CLASS : ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(methodOrLineTypeRef,
                                                                  managed_profiler_debugger_logexception_name.data(),
                                                                  signature, signatureLength, &logExceptionRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper methodLogExceptionRef could not be defined.");
            return hr;
        }

        SetLogExceptionMemberRef(probeType, logExceptionRef);
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

HRESULT DebuggerTokens::WriteLogArg(void* rewriterWrapperPtr, const TypeSignature& argument, ILInstr** instruction, ProbeType probeType)
{
    return WriteLogArgOrLocal(rewriterWrapperPtr, argument, instruction, true /* isArg */, probeType);
}

HRESULT DebuggerTokens::WriteLogLocal(void* rewriterWrapperPtr, const TypeSignature& local, ILInstr** instruction, ProbeType probeType)
{
    return WriteLogArgOrLocal(rewriterWrapperPtr, local, instruction, false /* isArg */, probeType);
}

HRESULT DebuggerTokens::WriteBeginOrEndMethod_EndMarker(void* rewriterWrapperPtr, bool isBeginMethod,
                                                        ILInstr** instruction, ProbeType probeType)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    auto [invokerTypeRef, stateTypeRef] = GetDebuggerInvokerAndState(probeType);
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();
    auto [beginOrEndEndMarker, beginOrEndMethodName ] = GetBeginOrEndMethodEndMarker(probeType, isBeginMethod);

    if (beginOrEndEndMarker == mdMemberRefNil)
    {
        unsigned callTargetStateBuffer;
        const auto callTargetStateSize = CorSigCompressToken(stateTypeRef, &callTargetStateBuffer);

        unsigned long signatureLength = 4 + callTargetStateSize;

        signatureLength += 1; // ByRef

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x01; // arguments count
        signature[offset++] = ELEMENT_TYPE_VOID;

        // DebuggerState
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = probeType == AsyncMethod ? ELEMENT_TYPE_CLASS : ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(invokerTypeRef, beginOrEndMethodName.c_str(), signature,
                                                                  signatureLength, &beginOrEndEndMarker);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper beginMethod could not be defined.");
            return hr;
        }

        SetBeginOrEndMethodEndMarker(probeType, isBeginMethod, beginOrEndEndMarker);
    }

    *instruction = rewriterWrapper->CallMember(beginOrEndEndMarker, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteBeginMethod_EndMarker(void* rewriterWrapperPtr, ILInstr** instruction, ProbeType probeType)
{
    return WriteBeginOrEndMethod_EndMarker(rewriterWrapperPtr, true /* isBeginMethod */, instruction, probeType);
}

HRESULT DebuggerTokens::WriteEndMethod_EndMarker(void* rewriterWrapperPtr, ILInstr** instruction, ProbeType probeType)
{
    return WriteBeginOrEndMethod_EndMarker(rewriterWrapperPtr, false /* isBeginMethod */, instruction, probeType);
}

HRESULT DebuggerTokens::WriteBeginLine(void* rewriterWrapperPtr, const TypeInfo* currentType,
    ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (beginLineRef == mdMemberRefNil)
    {
        unsigned callTargetStateBuffer;
        auto lineStateSize = CorSigCompressToken(lineDebuggerStateTypeRef, &callTargetStateBuffer);

        unsigned runtimeMethodHandleBuffer;
        auto runtimeMethodHandleSize = CorSigCompressToken(runtimeMethodHandleRef, &runtimeMethodHandleBuffer);

        unsigned runtimeTypeHandleBuffer;
        auto runtimeTypeHandleSize = CorSigCompressToken(runtimeTypeHandleRef, &runtimeTypeHandleBuffer);

        unsigned long signatureLength = 12 + lineStateSize + runtimeMethodHandleSize + runtimeTypeHandleSize;

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x01; // generic arguments count
        signature[offset++] = 0x07; // arguments count

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, lineStateSize);
        offset += lineStateSize;

        signature[offset++] = ELEMENT_TYPE_STRING;

        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x00;

        // RuntimeMethodHandle
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &runtimeMethodHandleBuffer, runtimeMethodHandleSize);
        offset += runtimeMethodHandleSize;

        // RuntimeTypeHandle
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &runtimeTypeHandleBuffer, runtimeTypeHandleSize);
        offset += runtimeTypeHandleSize;

        signature[offset++] = ELEMENT_TYPE_I4; // methodMetadataIndex
        signature[offset++] = ELEMENT_TYPE_I4; // lineNumber
        signature[offset++] = ELEMENT_TYPE_STRING; // probeFilePath

        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            lineInvokerTypeRef, managed_profiler_debugger_beginline_name.data(), signature, signatureLength, &beginLineRef);
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

    hr = module_metadata->metadata_emit->DefineMethodSpec(beginLineRef, signature, signatureLength,
                                                          &beginMethodSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating begin method spec.");
        return hr;
    }

    *instruction = rewriterWrapper->CallMember(beginMethodSpec, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteEndLine(void* rewriterWrapperPtr, ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (endLineRef == mdMemberRefNil)
    {
        unsigned callTargetStateBuffer;
        auto callTargetStateSize = CorSigCompressToken(lineDebuggerStateTypeRef, &callTargetStateBuffer);

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

        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            lineInvokerTypeRef, managed_profiler_debugger_endline_name.data(), signature, signatureLength, &endLineRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper beginMethod could not be defined.");
            return hr;
        }
    }

    *instruction = rewriterWrapper->CallMember(endLineRef, false);
    return S_OK;
}

HRESULT DebuggerTokens::ModifyLocalSigForLineProbe(ILRewriter* reWriter, ULONG* callTargetStateIndex, mdToken* callTargetStateToken)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ModuleMetadata* module_metadata = GetMetadata();

    PCCOR_SIGNATURE originalSignature = nullptr;
    ULONG originalSignatureSize = 0;
    mdToken localVarSig = reWriter->GetTkLocalVarSig();

    if (localVarSig != mdTokenNil)
    {
        IfFailRet(
            module_metadata->metadata_import->GetSigFromToken(localVarSig, &originalSignature, &originalSignatureSize));

        // Check if the localvarsig has been already rewritten (the last local
        // should be the LineDebuggerState)
        unsigned temp = 0;
        const auto len = CorSigCompressToken(lineDebuggerStateTypeRef, &temp);
        if (originalSignatureSize - len > 0)
        {
            if (originalSignature[originalSignatureSize - len - 1] == ELEMENT_TYPE_VALUETYPE)
            {
                if (memcmp(&originalSignature[originalSignatureSize - len], &temp, len) == 0)
                {
                    Logger::Warn("The signature for this method has been already modified.");
                    return E_FAIL;
                }
            }
        }
    }

    ULONG newLocalsCount = 1;

    // Gets the calltarget state type buffer and size
    unsigned callTargetStateTypeRefBuffer;
    auto callTargetStateTypeRefSize = CorSigCompressToken(lineDebuggerStateTypeRef, &callTargetStateTypeRefBuffer);

    // New signature size
    ULONG newSignatureSize = originalSignatureSize + (1 + callTargetStateTypeRefSize);
    ULONG newSignatureOffset = 0;

    ULONG oldLocalsBuffer;
    ULONG oldLocalsLen = 0;
    unsigned newLocalsBuffer;
    ULONG newLocalsLen;

    // Calculate the new locals count
    if (originalSignatureSize == 0)
    {
        newSignatureSize += 2;
        newLocalsLen = CorSigCompressData(newLocalsCount, &newLocalsBuffer);
    }
    else
    {
        oldLocalsLen = CorSigUncompressData(originalSignature + 1, &oldLocalsBuffer);
        newLocalsCount += oldLocalsBuffer;
        newLocalsLen = CorSigCompressData(newLocalsCount, &newLocalsBuffer);
        newSignatureSize += newLocalsLen - oldLocalsLen;
    }

    // New signature declaration
    COR_SIGNATURE newSignatureBuffer[signatureBufferSize];
    newSignatureBuffer[newSignatureOffset++] = IMAGE_CEE_CS_CALLCONV_LOCAL_SIG;

    // Set the locals count
    memcpy(&newSignatureBuffer[newSignatureOffset], &newLocalsBuffer, newLocalsLen);
    newSignatureOffset += newLocalsLen;

    // Copy previous locals to the signature
    if (originalSignatureSize > 0)
    {
        const auto copyLength = originalSignatureSize - 1 - oldLocalsLen;
        memcpy(&newSignatureBuffer[newSignatureOffset], originalSignature + 1 + oldLocalsLen, copyLength);
        newSignatureOffset += copyLength;
    }

    // Add new locals

    // CallTarget state value
    newSignatureBuffer[newSignatureOffset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&newSignatureBuffer[newSignatureOffset], &callTargetStateTypeRefBuffer, callTargetStateTypeRefSize);
    newSignatureOffset += callTargetStateTypeRefSize;

    // Get new locals token
    mdToken newLocalVarSig;
    hr = module_metadata->metadata_emit->GetTokenFromSig(newSignatureBuffer, newSignatureSize, &newLocalVarSig);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating new locals var signature.");
        return hr;
    }

    reWriter->SetTkLocalVarSig(newLocalVarSig);
    *callTargetStateToken = lineDebuggerStateTypeRef;
    *callTargetStateIndex = newLocalsCount - 1;
    return hr;
}

HRESULT DebuggerTokens::GetDebuggerLocals(void* rewriterWrapperPtr, ULONG* callTargetStateIndex, mdToken* callTargetStateToken, ULONG* asyncMethodStateIndex)
{
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    const auto reWriter = rewriterWrapper->GetILRewriter();

    PCCOR_SIGNATURE originalSignature = nullptr;
    ULONG originalSignatureSize = 0;
    const mdToken localVarSig = reWriter->GetTkLocalVarSig();

    if (localVarSig == mdTokenNil)
    {
        // There must be local signature as the modification of the CallTarget already took place.
        Logger::Error("localVarSig is mdTokenNil in GetDebuggerLocals.");
        return E_FAIL;
    }

    HRESULT hr;
    ModuleMetadata* module_metadata = GetMetadata();
    IfFailRet(module_metadata->metadata_import->GetSigFromToken(localVarSig, &originalSignature, &originalSignatureSize));

    ULONG localsLen;
    auto bytesRead = CorSigUncompressData(originalSignature + 1, &localsLen);

    const auto debuggerAdditionalLocalsCount = GetAdditionalLocalsCount();
    *callTargetStateToken = lineDebuggerStateTypeRef;
    *callTargetStateIndex = localsLen - /* Accounting for MethodDebuggerState */ 1 - debuggerAdditionalLocalsCount;
    *asyncMethodStateIndex = localsLen - debuggerAdditionalLocalsCount; // last local
    return S_OK;
}

mdFieldDef DebuggerTokens::GetIsFirstEntryToMoveNextFieldToken(const mdToken type)
{
    const ModuleMetadata* module_metadata = GetMetadata();
    mdFieldDef token;
    ULONG cTokens;
    HCORENUM henum = nullptr;
    const HRESULT hr = module_metadata->metadata_import->EnumFieldsWithName (
        &henum, type, 
        managed_profiler_debugger_is_first_entry_field_name.c_str(),
        &token, 1, &cTokens);
    module_metadata->metadata_import->CloseEnum(henum);
    if (SUCCEEDED(hr))
    {
        return token;
    }
    
    return mdFieldDefNil;
}
} // namespace debugger