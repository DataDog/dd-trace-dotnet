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

        const auto isMultiProbe = probeType == NonAsyncMethodMultiProbe;
        const auto isAsyncMethod = probeType == AsyncMethodProbe || probeType == AsyncMethodSpanProbe;

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
        if (isAsyncMethod)
        {
            // The injected field is of type System.Object
            signature[offset++] = ELEMENT_TYPE_BYREF;
            signature[offset++] = ELEMENT_TYPE_OBJECT;
        }
        else
        {
            signature[offset++] = ELEMENT_TYPE_BYREF;
            if (isMultiProbe)
            {
                signature[offset++] = ELEMENT_TYPE_SZARRAY;
            }
            signature[offset++] = ELEMENT_TYPE_VALUETYPE;
            memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
            offset += callTargetStateSize;
        }

        auto hr = module_metadata->metadata_emit->DefineMemberRef(invokerTypeRef, targetMemberName, signature,
                                                                  offset, &logArgOrLocalRef);
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
        if (argSigBuff[0] == ELEMENT_TYPE_BYREF || argSigBuff[0] == ELEMENT_TYPE_PTR)
        {
            argumentSignatureBuffer = argSigBuff + 1;
            argumentSignatureSize = signatureSize - 1;
            signatureLength += signatureSize - 1;
        }
        else if (argSigBuff[0] == ELEMENT_TYPE_PINNED)
        {
            argumentSignatureBuffer = argSigBuff + 2;
            argumentSignatureSize = signatureSize - 2;
            signatureLength += signatureSize - 2;
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
        Logger::Warn("Error creating LogArg or LogLocal method spec.");
        return hr;
    }

    *instruction = rewriterWrapper->CallMember(logArgMethodSpec, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteFlowRecorderLogArgOrLocal(void* rewriterWrapperPtr, const TypeSignature& argOrLocal,
                                                       ILInstr** instruction, bool isArg)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*)rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();
    mdMemberRef logArgOrLocalRef = isArg ? flowRecorderLogArgRef : flowRecorderLogLocalRef;

    if (logArgOrLocalRef == mdMemberRefNil)
    {
        auto targetMemberName = isArg
            ? managed_profiler_debugger_flow_recorder_log_arg_name.data()
            : managed_profiler_debugger_flow_recorder_log_local_name.data();

        unsigned flowRecorderStateTypeRefBuffer;
        const auto flowRecorderStateTypeRefSize =
            CorSigCompressToken(flowRecorderStateTypeRef, &flowRecorderStateTypeRefBuffer);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;
        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x01; // one generic arg/local
        signature[offset++] = 0x03; // value, index, state
        signature[offset++] = ELEMENT_TYPE_VOID;
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x00;
        signature[offset++] = ELEMENT_TYPE_I4;
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &flowRecorderStateTypeRefBuffer, flowRecorderStateTypeRefSize);
        offset += flowRecorderStateTypeRefSize;

        hr = module_metadata->metadata_emit->DefineMemberRef(
            flowRecorderTypeRef, targetMemberName, signature, offset, &logArgOrLocalRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper flow recorder ", isArg ? "LogArg" : "LogLocal", " could not be defined.");
            return hr;
        }

        if (isArg)
        {
            flowRecorderLogArgRef = logArgOrLocalRef;
        }
        else
        {
            flowRecorderLogLocalRef = logArgOrLocalRef;
        }
    }

    PCCOR_SIGNATURE argumentSignatureBuffer;
    ULONG argumentSignatureSize;
    auto signatureLength = 2;
    const auto [elementType, argTypeFlags] = argOrLocal.GetElementTypeAndFlags();
    if (argTypeFlags & TypeFlagByRef)
    {
        PCCOR_SIGNATURE argSigBuff;
        auto signatureSize = argOrLocal.GetSignature(argSigBuff);
        if (argSigBuff[0] == ELEMENT_TYPE_BYREF || argSigBuff[0] == ELEMENT_TYPE_PTR)
        {
            argumentSignatureBuffer = argSigBuff + 1;
            argumentSignatureSize = signatureSize - 1;
            signatureLength += signatureSize - 1;
        }
        else if (argSigBuff[0] == ELEMENT_TYPE_PINNED)
        {
            argumentSignatureBuffer = argSigBuff + 2;
            argumentSignatureSize = signatureSize - 2;
            signatureLength += signatureSize - 2;
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

    COR_SIGNATURE methodSpecSignature[signatureBufferSize];
    unsigned offset = 0;
    methodSpecSignature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
    methodSpecSignature[offset++] = 0x01;
    memcpy(&methodSpecSignature[offset], argumentSignatureBuffer, argumentSignatureSize);
    offset += argumentSignatureSize;

    mdMethodSpec methodSpec = mdMethodSpecNil;
    hr = module_metadata->metadata_emit->DefineMethodSpec(logArgOrLocalRef, methodSpecSignature, signatureLength,
                                                          &methodSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating FlowRecorder LogArg or LogLocal method spec.");
        return hr;
    }

    *instruction = rewriterWrapper->CallMember(methodSpec, false);
    return S_OK;
}

HRESULT DebuggerTokens::EnsureBaseCalltargetTokens()
{
    std::lock_guard<std::recursive_mutex> guard(metadata_mutex);

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

    // *** Ensure lineInvoker type ref
    if (asyncLineInvokerTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_debugger_async_line_type.data(), &asyncLineInvokerTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper asyncLineInvokerTypeRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure lineDebuggerState type ref
    if (asyncLineDebuggerStateTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_debugger_async_linestatetype.data(), &asyncLineDebuggerStateTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper asyncLineDebuggerStateTypeRef could not be defined.");
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

    // *** Ensure AsyncDebuggerState type ref
    if (asyncMethodDebuggerStateTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_debugger_async_method_state_type.data(),
            &asyncMethodDebuggerStateTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper asyncMethodDebuggerStateTypeRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure MethodSpanProbeDebuggerInvokerTypeRef type ref
    if (methodSpanProbeDebuggerInvokerTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(profilerAssemblyRef,
                                                                 managed_profiler_debugger_span_invoker_type.data(),
                                                                 &methodSpanProbeDebuggerInvokerTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper asyncMethodSpanProbeDebuggerInvokerTypeRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure MethodSpanProbeDebuggerState type ref
    if (methodSpanProbeDebuggerStateTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(profilerAssemblyRef,
                                                                 managed_profiler_debugger_span_state_type.data(),
                                                                 &methodSpanProbeDebuggerStateTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper asyncMethodSpanProbeDebuggerStateTypeRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure RentArrayTypeRef type ref
    if (rentArrayTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, instrumentation_allocator_invoker_name.data(), &rentArrayTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper rentArrayTypeRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure FlowRecorder type ref
    if (flowRecorderTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_debugger_flow_recorder_type.data(), &flowRecorderTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper flowRecorderTypeRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure FlowRecorderState type ref
    if (flowRecorderStateTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_debugger_flow_recorder_state_type.data(), &flowRecorderStateTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper flowRecorderStateTypeRef could not be defined.");
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

const WSTRING& DebuggerTokens::GetCallTargetRefStructType()
{
    return EmptyWStr;
}

int DebuggerTokens::GetAdditionalLocalsCount(const std::vector<TypeSignature>& methodTypeArguments)
{
    return 3;
}

void DebuggerTokens::AddAdditionalLocals(TypeSignature* methodReturnValue, std::vector<TypeSignature>* methodTypeArguments,
    SignatureBuilder& signature, bool isAsyncMethod)
{
    // Gets the calltarget state of line probe type buffer and size
    unsigned callTargetStateTypeRefBuffer;
    const auto callTargetStateTypeRefSize = CorSigCompressToken(GetDebuggerState(isAsyncMethod ? AsyncLineProbe : NonAsyncLineProbe), &callTargetStateTypeRefBuffer);

    // CallTarget state of line probe
    signature.Append(ELEMENT_TYPE_VALUETYPE);
    signature.Append(&callTargetStateTypeRefBuffer, callTargetStateTypeRefSize);

    // Gets the calltarget state of span method probe type buffer and size
    unsigned spanStateTypeRefBuffer;
    const auto spanStateTypeRefSize = CorSigCompressToken(methodSpanProbeDebuggerStateTypeRef, &spanStateTypeRefBuffer);

    // CallTarget state of async method probe
    signature.Append(ELEMENT_TYPE_VALUETYPE);
    signature.Append(&spanStateTypeRefBuffer, spanStateTypeRefSize);
    
    // CallTarget states of multi-probe scenario

    // Gets the calltarget state of line probe type buffer and size
    unsigned methodDebuggerStatesTypeRefBuffer;
    const auto methodDebuggerStatesTypeRefSize = CorSigCompressToken(GetDebuggerState(NonAsyncMethodMultiProbe), &methodDebuggerStatesTypeRefBuffer);

    signature.Append(ELEMENT_TYPE_SZARRAY);
    signature.Append(ELEMENT_TYPE_VALUETYPE);
    signature.Append(&methodDebuggerStatesTypeRefBuffer, methodDebuggerStatesTypeRefSize);
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
    if (currentTypeRef == mdTokenNil)
    {
        isValueType = false;
        currentTypeRef = GetObjectTypeRef();
    }

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

    bool isAsyncMethod = probeType == AsyncMethodProbe;
    bool isMultiProbe = probeType == NonAsyncMethodMultiProbe;
    
    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
    signature[offset++] = 0x01;                        // generic arguments count
    signature[offset++] = isMultiProbe ? 0x03 : 0x04; // arguments count
    
    if (isAsyncMethod)
    {
        signature[offset++] = ELEMENT_TYPE_VOID; // return type is void for async method probe (the state is a field)
    }
    else
    {
        if (isMultiProbe)
        {
            signature[offset++] = ELEMENT_TYPE_SZARRAY;
        }

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;
    }

    signature[offset++] = ELEMENT_TYPE_MVAR;
    signature[offset++] = 0x00;
    signature[offset++] = ELEMENT_TYPE_I4; // methodMetadataIndex
    signature[offset++] = ELEMENT_TYPE_I4; // instrumentationVersion / probeMetadataIndex (depending on multi probe / not)

    if (!isMultiProbe && !isAsyncMethod)
    {
        signature[offset++] = ELEMENT_TYPE_STRING; // probeId

    }
    
    WSTRING methodName;
    if (isAsyncMethod)
    {
        methodName = managed_profiler_debugger_begin_async_method_name;
        // The injected field is of type System.Object
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_OBJECT;
    }
    else
    {
        methodName = managed_profiler_debugger_beginmethod_startmarker_name;
    }

    return GetMetadata()->metadata_emit->DefineMemberRef(
        invokerTypeRef, methodName.data(), signature,
        offset, &beginMethodRef);
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
    if (currentTypeRef == mdTokenNil)
    {
        isValueType = false;
        currentTypeRef = GetObjectTypeRef();
    }

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
    if (currentTypeRef == mdTokenNil)
    {
        isValueType = false;
        currentTypeRef = GetObjectTypeRef();
    }

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

    const auto isMultiProbe = probeType == NonAsyncMethodMultiProbe;
    const auto isAsyncMethod = probeType == AsyncMethodProbe || probeType == AsyncMethodSpanProbe;

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

    if (isAsyncMethod)
    {
        // The injected field is of type System.Object
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_OBJECT;
    }
    else
    {
        signature[offset++] = ELEMENT_TYPE_BYREF;
        if (isMultiProbe)
        {
            signature[offset++] = ELEMENT_TYPE_SZARRAY;
        }
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;
    }

   return GetMetadata()->metadata_emit->DefineMemberRef(
        invokerTypeRef, managed_profiler_debugger_endmethod_startmarker_name.data(), signature, offset,
        &endMethodRef);
}

// write log exception
HRESULT DebuggerTokens::WriteLogException(void* rewriterWrapperPtr, ProbeType probeType)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    mdMemberRef logExceptionRef = GetLogExceptionMemberRef(probeType);

    if (logExceptionRef == mdMemberRefNil)
    {
        ModuleMetadata* module_metadata = GetMetadata();

        mdTypeRef stateTypeRef = GetDebuggerState(probeType);
        mdTypeRef methodOrLineTypeRef = GetDebuggerInvoker(probeType);
        const auto isMultiProbe = probeType == NonAsyncMethodMultiProbe;
        const auto isAsyncMethod = probeType == AsyncMethodProbe || probeType == AsyncMethodSpanProbe;

        unsigned exTypeRefBuffer;
        auto exTypeRefSize = CorSigCompressToken(exTypeRef, &exTypeRefBuffer);

        unsigned callTargetStateBuffer;
        auto callTargetStateSize = CorSigCompressToken(stateTypeRef, &callTargetStateBuffer);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x02; // (Exception, DebuggerState)

        signature[offset++] = ELEMENT_TYPE_VOID;
        signature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&signature[offset], &exTypeRefBuffer, exTypeRefSize);
        offset += exTypeRefSize;

        // DebuggerState
        if (isAsyncMethod)
        {
            // The injected field is of type System.Object
            signature[offset++] = ELEMENT_TYPE_BYREF;
            signature[offset++] = ELEMENT_TYPE_OBJECT;
        }
        else
        {
            signature[offset++] = ELEMENT_TYPE_BYREF;
            if (isMultiProbe)
            {
                signature[offset++] = ELEMENT_TYPE_SZARRAY;
            }
            signature[offset++] = ELEMENT_TYPE_VALUETYPE;
            memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
            offset += callTargetStateSize;
        }

        auto hr = module_metadata->metadata_emit->DefineMemberRef(methodOrLineTypeRef,
                                                                  managed_profiler_debugger_logexception_name.data(),
                                                                  signature, offset, &logExceptionRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper methodLogExceptionRef could not be defined.");
            return hr;
        }

        SetLogExceptionMemberRef(probeType, logExceptionRef);
    }

    rewriterWrapper->CallMember(logExceptionRef, false);
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
    const auto isMultiProbe = probeType == NonAsyncMethodMultiProbe;
    const auto isAsyncMethod= probeType == AsyncMethodProbe || probeType == AsyncMethodSpanProbe;

    if (beginOrEndEndMarker == mdMemberRefNil)
    {
        unsigned callTargetStateBuffer;
        const auto callTargetStateSize = CorSigCompressToken(stateTypeRef, &callTargetStateBuffer);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x01; // arguments count
        signature[offset++] = ELEMENT_TYPE_VOID;

        if (isAsyncMethod)
        {
            // The injected field is of type System.Object
            signature[offset++] = ELEMENT_TYPE_BYREF;
            signature[offset++] = ELEMENT_TYPE_OBJECT;
        }
        else
        {
            // DebuggerState
            signature[offset++] = ELEMENT_TYPE_BYREF;
            if (isMultiProbe)
            {
                signature[offset++] = ELEMENT_TYPE_SZARRAY;
            }

            signature[offset++] = ELEMENT_TYPE_VALUETYPE;
            memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
            offset += callTargetStateSize;
        }

        auto hr = module_metadata->metadata_emit->DefineMemberRef(invokerTypeRef, beginOrEndMethodName.c_str(), signature,
                                                                  offset, &beginOrEndEndMarker);
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

HRESULT DebuggerTokens::WriteBeginLine(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction, ProbeType probeType)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    mdMemberRef beginLineRef = GetBeginMethodStartMarker(probeType);

    if (beginLineRef == mdMemberRefNil)
    {
        mdTypeRef stateTypeRef = GetDebuggerState(probeType);
        mdTypeRef lineTypeRef = GetDebuggerInvoker(probeType);

        unsigned callTargetStateBuffer;
        auto lineStateSize = CorSigCompressToken(stateTypeRef, &callTargetStateBuffer);

        unsigned runtimeMethodHandleBuffer;
        auto runtimeMethodHandleSize = CorSigCompressToken(runtimeMethodHandleRef, &runtimeMethodHandleBuffer);

        unsigned runtimeTypeHandleBuffer;
        auto runtimeTypeHandleSize = CorSigCompressToken(runtimeTypeHandleRef, &runtimeTypeHandleBuffer);

        unsigned long signatureLength = 13 + lineStateSize + runtimeMethodHandleSize + runtimeTypeHandleSize;

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x01; // generic arguments count
        signature[offset++] = 0x08; // arguments count

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, lineStateSize);
        offset += lineStateSize;

        signature[offset++] = ELEMENT_TYPE_STRING;

        signature[offset++] = ELEMENT_TYPE_I4; // probeMetadataIndex

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
            lineTypeRef, 
            managed_profiler_debugger_beginline_name.data(), 
            signature, 
            signatureLength, 
            &beginLineRef);

        if (FAILED(hr))
        {
            Logger::Warn("Wrapper beginMethod could not be defined.");
            return hr;
        }

        SetBeginMethodStartMarker(probeType, beginLineRef);
    }

    mdMethodSpec beginMethodSpec = mdMethodSpecNil;

    bool isValueType = currentType->valueType;
    mdToken currentTypeRef = GetCurrentTypeRef(currentType, isValueType);
    if (currentTypeRef == mdTokenNil)
    {
        isValueType = false;
        currentTypeRef = GetObjectTypeRef();
    }

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

HRESULT DebuggerTokens::WriteEndLine(void* rewriterWrapperPtr, ILInstr** instruction, ProbeType probeType)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    mdMemberRef endLineRef = GetEndMethodStartMarker(probeType, false /* isVoid, non-relevant for Line Probes */);

    if (endLineRef == mdMemberRefNil)
    {
        mdTypeRef stateTypeRef = GetDebuggerState(probeType);
        mdTypeRef lineTypeRef = GetDebuggerInvoker(probeType);

        unsigned callTargetStateBuffer;
        auto callTargetStateSize = CorSigCompressToken(stateTypeRef, &callTargetStateBuffer);

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
            lineTypeRef, managed_profiler_debugger_endline_name.data(), signature, signatureLength, &endLineRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper beginMethod could not be defined.");
            return hr;
        }

        SetEndMethodStartMarker(probeType, false /* isVoid, non-relevant for Line Probes */, endLineRef);
    }

    *instruction = rewriterWrapper->CallMember(endLineRef, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteBeginSpan(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction, bool isAsyncMethod)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    const auto probeType = isAsyncMethod ? AsyncMethodSpanProbe : NonAsyncMethodSpanProbe;

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    bool isMultiProbe = probeType == NonAsyncMethodMultiProbe;

    mdMemberRef beginLineRef = GetBeginMethodStartMarker(probeType);

    if (beginLineRef == mdMemberRefNil)
    {
        mdTypeRef stateTypeRef = GetDebuggerState(probeType);
        mdTypeRef lineTypeRef = GetDebuggerInvoker(probeType);

        unsigned callTargetStateBuffer;
        auto lineStateSize = CorSigCompressToken(stateTypeRef, &callTargetStateBuffer);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = isAsyncMethod ? 0x04 : 0x03; // arguments count

        if (isAsyncMethod)
        {
            signature[offset++] = ELEMENT_TYPE_VOID; // return type is void for async method probe (the state is a field)
        }
        else
        {
            signature[offset++] = ELEMENT_TYPE_VALUETYPE;
            memcpy(&signature[offset], &callTargetStateBuffer, lineStateSize);
            offset += lineStateSize;
        }

        signature[offset++] = ELEMENT_TYPE_STRING; // probeId
        signature[offset++] = ELEMENT_TYPE_STRING; // resourceName
        signature[offset++] = ELEMENT_TYPE_STRING; // operationName

        if (isAsyncMethod)
        {
            // The injected field is of type System.Object
            signature[offset++] = ELEMENT_TYPE_BYREF;
            signature[offset++] = ELEMENT_TYPE_OBJECT;
        }

        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            lineTypeRef, managed_profiler_debugger_begin_span_name.data(), signature, offset, &beginLineRef);

        if (FAILED(hr))
        {
            Logger::Warn("Wrapper beginMethod could not be defined.");
            return hr;
        }

        SetBeginMethodStartMarker(probeType, beginLineRef);
    }

    *instruction = rewriterWrapper->CallMember(beginLineRef, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteEndSpan(void* rewriterWrapperPtr, ILInstr** instruction, bool isAsyncMethod)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    const auto probeType = isAsyncMethod ? AsyncMethodSpanProbe : NonAsyncMethodSpanProbe;

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    mdMemberRef endSpanRef = GetEndMethodStartMarker(probeType, false /* isVoid, non-relevant for Line Probes */);

    if (endSpanRef == mdMemberRefNil)
    {
        mdTypeRef stateTypeRef = GetDebuggerState(probeType);
        mdTypeRef lineTypeRef = GetDebuggerInvoker(probeType);

        unsigned callTargetStateBuffer;
        auto callTargetStateSize = CorSigCompressToken(stateTypeRef, &callTargetStateBuffer);

        unsigned exTypeRefBuffer;
        const auto exTypeRefSize = CorSigCompressToken(exTypeRef, &exTypeRefBuffer);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x02; // arguments count
        signature[offset++] = ELEMENT_TYPE_VOID;

        signature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&signature[offset], &exTypeRefBuffer, exTypeRefSize);
        offset += exTypeRefSize;

        if (isAsyncMethod)
        {
            // The injected field is of type System.Object
            signature[offset++] = ELEMENT_TYPE_BYREF;
            signature[offset++] = ELEMENT_TYPE_OBJECT;
        }
        else
        {
            // DebuggerState
            signature[offset++] = ELEMENT_TYPE_BYREF;
            signature[offset++] = ELEMENT_TYPE_VALUETYPE;
            memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
            offset += callTargetStateSize;
        }

        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            lineTypeRef, managed_profiler_debugger_end_span_name.data(), signature, offset, &endSpanRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper beginMethod could not be defined.");
            return hr;
        }

        SetEndMethodStartMarker(probeType, false /* isVoid, non-relevant for Line Probes */, endSpanRef);
    }

    *instruction = rewriterWrapper->CallMember(endSpanRef, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteShouldUpdateProbeInfo(void* rewriterWrapperPtr, ILInstr** instruction, ProbeType probeType)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    bool isAsyncMethod = probeType == AsyncMethodProbe;

    mdMemberRef shouldUpdateProbeInfoRef =
        isAsyncMethod ? asyncShouldUpdateProbeInfoRef : nonAsyncShouldUpdateProbeInfoRef;

    if (shouldUpdateProbeInfoRef == mdMemberRefNil)
    {
        auto [invokerTypeRef, stateTypeRef] = GetDebuggerInvokerAndState(isAsyncMethod ? AsyncMethodProbe : probeType);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = isAsyncMethod ? 0x03 : 0x02; // arguments count
        signature[offset++] = ELEMENT_TYPE_BOOLEAN;

        signature[offset++] = ELEMENT_TYPE_I4;
        signature[offset++] = ELEMENT_TYPE_I4;

        if (isAsyncMethod)
        {
            // The injected field is of type System.Object
            signature[offset++] = ELEMENT_TYPE_BYREF;
            signature[offset++] = ELEMENT_TYPE_OBJECT;
        }

        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            invokerTypeRef, managed_profiler_debugger_should_update_probe_info_name.data(), signature, offset,
            &shouldUpdateProbeInfoRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper beginMethod could not be defined.");
            return hr;
        }

        if (isAsyncMethod)
        {
            asyncShouldUpdateProbeInfoRef = shouldUpdateProbeInfoRef;
        }
        else
        {
            nonAsyncShouldUpdateProbeInfoRef = shouldUpdateProbeInfoRef;
        }
    }

    *instruction = rewriterWrapper->CallMember(shouldUpdateProbeInfoRef, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteUpdateProbeInfo(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction,
                                             ProbeType probeType)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    bool isAsyncMethod = probeType == AsyncMethodProbe;

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    mdMemberRef updateProbeInfoRef =
        isAsyncMethod ? asyncUpdateProbeInfoRef : nonAsyncUpdateProbeInfoRef;

    if (updateProbeInfoRef == mdMemberRefNil)
    {
        mdTypeRef typeRef = GetDebuggerInvoker(probeType);

        unsigned runtimeMethodHandleBuffer;
        auto runtimeMethodHandleSize = CorSigCompressToken(runtimeMethodHandleRef, &runtimeMethodHandleBuffer);

        unsigned runtimeTypeHandleBuffer;
        auto runtimeTypeHandleSize = CorSigCompressToken(runtimeTypeHandleRef, &runtimeTypeHandleBuffer);

        unsigned long signatureLength = (isAsyncMethod ? 14 : 11) + runtimeMethodHandleSize + runtimeTypeHandleSize;

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        if (isAsyncMethod)
        {
            signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
            signature[offset++] = 0x01; // one generic
        }
        else
        {
            signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        }
        signature[offset++] = 0x06 + (isAsyncMethod ? 1 : 0); // arguments count
        signature[offset++] = ELEMENT_TYPE_VOID;

        // probeIds
        signature[offset++] = ELEMENT_TYPE_SZARRAY;
        signature[offset++] = ELEMENT_TYPE_STRING;

        // probeMetadataIndices
        signature[offset++] = ELEMENT_TYPE_SZARRAY;
        signature[offset++] = ELEMENT_TYPE_I4;

        if (isAsyncMethod)
        {
            signature[offset++] = ELEMENT_TYPE_MVAR;
            signature[offset++] = 0x00;
        }

        signature[offset++] = ELEMENT_TYPE_I4;
        signature[offset++] = ELEMENT_TYPE_I4;

         // RuntimeMethodHandle
         signature[offset++] = ELEMENT_TYPE_VALUETYPE;
         memcpy(&signature[offset], &runtimeMethodHandleBuffer, runtimeMethodHandleSize);
         offset += runtimeMethodHandleSize;

         // RuntimeTypeHandle
         signature[offset++] = ELEMENT_TYPE_VALUETYPE;
         memcpy(&signature[offset], &runtimeTypeHandleBuffer, runtimeTypeHandleSize);
         offset += runtimeTypeHandleSize;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            typeRef, managed_profiler_debugger_update_probe_info_name.data(), signature, signatureLength,
            &updateProbeInfoRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper beginMethod could not be defined.");
            return hr;
        }

        if (isAsyncMethod)
        {
            asyncUpdateProbeInfoRef = updateProbeInfoRef;
        }
        else
        {
            nonAsyncUpdateProbeInfoRef = updateProbeInfoRef;
        }
    }

    mdMemberRef updateProbeInfoMemberRef;

    if (isAsyncMethod)
    {
        bool isValueType = currentType->valueType;
        mdToken currentTypeRef = GetCurrentTypeRef(currentType, isValueType);
        if (currentTypeRef == mdTokenNil)
        {
            isValueType = false;
            currentTypeRef = GetObjectTypeRef();
        }

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

        hr = module_metadata->metadata_emit->DefineMethodSpec(updateProbeInfoRef, signature, signatureLength,
                                                              &updateProbeInfoMemberRef);
        if (FAILED(hr))
        {
            Logger::Warn("Error creating begin method spec.");
            return hr;
        }
    }
    else
    {
        updateProbeInfoMemberRef = updateProbeInfoRef;
    }

    *instruction = rewriterWrapper->CallMember(updateProbeInfoMemberRef, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteRentArray(void* rewriterWrapperPtr, const TypeSignature& type, ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (rentArrayRef == mdMemberRefNil)
    {
        unsigned long signatureLength = 7;

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x01; // one generic
        signature[offset++] = 0x01; // (size)

        signature[offset++] = ELEMENT_TYPE_SZARRAY;
        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x00;
        
        // size
        signature[offset++] = ELEMENT_TYPE_I4;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(rentArrayTypeRef,
                                                                  managed_profiler_debugger_rent_array_name.data(),
                                                                  signature,
                                                                  signatureLength, &rentArrayRef);
        if (FAILED(hr))
        {
            return hr;
        }
    }

    mdMethodSpec logArgMethodSpec = mdMethodSpecNil;

    auto signatureLength = 2;

    PCCOR_SIGNATURE argumentSignatureBuffer;
    ULONG argumentSignatureSize;
    const auto [elementType, argTypeFlags] = type.GetElementTypeAndFlags();
    if (argTypeFlags & TypeFlagByRef)
    {
        PCCOR_SIGNATURE argSigBuff;
        auto signatureSize = type.GetSignature(argSigBuff);
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
        auto signatureSize = type.GetSignature(argumentSignatureBuffer);
        argumentSignatureSize = signatureSize;
        signatureLength += signatureSize;
    }

    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;
    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
    signature[offset++] = 0x01;

    memcpy(&signature[offset], argumentSignatureBuffer, argumentSignatureSize);
    offset += argumentSignatureSize;

    hr = module_metadata->metadata_emit->DefineMethodSpec(rentArrayRef, signature, signatureLength,
                                                          &logArgMethodSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating RentArray method spec.");
        return hr;
    }

    *instruction = rewriterWrapper->CallMember(logArgMethodSpec, false);
    return S_OK;
}


HRESULT DebuggerTokens::GetIsFirstEntryToMoveNextFieldToken(const mdToken type, mdFieldDef& token)
{
    const ModuleMetadata* module_metadata = GetMetadata();
    ULONG cTokens;
    HCORENUM henum = nullptr;
    const HRESULT hr = module_metadata->metadata_import->EnumFieldsWithName(
        &henum, type, managed_profiler_debugger_is_first_entry_field_name.c_str(), &token, 1, &cTokens);
    module_metadata->metadata_import->CloseEnum(henum);
    return hr;
}

HRESULT DebuggerTokens::GetFlowRecorderAsyncOperationIdFieldToken(const mdToken type, mdFieldDef& token)
{
    const ModuleMetadata* module_metadata = GetMetadata();
    ULONG cTokens;
    HCORENUM henum = nullptr;
    const HRESULT hr = module_metadata->metadata_import->EnumFieldsWithName(
        &henum, type, managed_profiler_debugger_flow_recorder_async_operation_id_field_name.c_str(), &token, 1,
        &cTokens);
    module_metadata->metadata_import->CloseEnum(henum);
    return hr;
}

HRESULT DebuggerTokens::GetFlowRecorderAsyncGenerationFieldToken(const mdToken type, mdFieldDef& token)
{
    const ModuleMetadata* module_metadata = GetMetadata();
    ULONG cTokens;
    HCORENUM henum = nullptr;
    const HRESULT hr = module_metadata->metadata_import->EnumFieldsWithName(
        &henum, type, managed_profiler_debugger_flow_recorder_async_generation_field_name.c_str(), &token, 1,
        &cTokens);
    module_metadata->metadata_import->CloseEnum(henum);
    return hr;
}

HRESULT DebuggerTokens::WriteDispose(void* rewriterWrapperPtr, ILInstr** instruction, ProbeType probeType)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    auto [invokerTypeRef, stateTypeRef] = GetDebuggerInvokerAndState(probeType);

    auto disposeRef = GetDisposeMemberRef(probeType);
    if (disposeRef == mdMemberRefNil)
    {
        mdTypeRef typeRef = GetDebuggerInvoker(probeType);

        unsigned callTargetStateBuffer;
        auto callTargetStateSize = CorSigCompressToken(stateTypeRef, &callTargetStateBuffer);

        const auto isMultiProbe = probeType == NonAsyncMethodMultiProbe;
        
        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x03; // arguments count
        signature[offset++] = ELEMENT_TYPE_VOID;

        signature[offset++] = ELEMENT_TYPE_I4;
        signature[offset++] = ELEMENT_TYPE_I4;

        // DebuggerState
        signature[offset++] = ELEMENT_TYPE_BYREF;
        if (isMultiProbe)
        {
            signature[offset++] = ELEMENT_TYPE_SZARRAY;
        }
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            typeRef, managed_profiler_debugger_dispose_name.data(), signature, offset,
            &disposeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper beginMethod could not be defined.");
            return hr;
        }

        SetDisposeMemberRef(probeType, disposeRef);
    }

    *instruction = rewriterWrapper->CallMember(disposeRef, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteFlowRecorderEnter(void* rewriterWrapperPtr, ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*)rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (flowRecorderEnterRef == mdMemberRefNil)
    {
        unsigned flowRecorderStateTypeRefBuffer;
        const auto flowRecorderStateTypeRefSize =
            CorSigCompressToken(flowRecorderStateTypeRef, &flowRecorderStateTypeRefBuffer);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x01; // arguments count

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &flowRecorderStateTypeRefBuffer, flowRecorderStateTypeRefSize);
        offset += flowRecorderStateTypeRefSize;

        signature[offset++] = ELEMENT_TYPE_I4; // methodMetadataIndex

        hr = module_metadata->metadata_emit->DefineMemberRef(
            flowRecorderTypeRef, managed_profiler_debugger_flow_recorder_enter_name.data(), signature, offset,
            &flowRecorderEnterRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper flowRecorderEnterRef could not be defined.");
            return hr;
        }
    }

    *instruction = rewriterWrapper->CallMember(flowRecorderEnterRef, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteFlowRecorderEnterFast(void* rewriterWrapperPtr, ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*)rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (flowRecorderEnterFastRef == mdMemberRefNil)
    {
        unsigned flowRecorderStateTypeRefBuffer;
        const auto flowRecorderStateTypeRefSize =
            CorSigCompressToken(flowRecorderStateTypeRef, &flowRecorderStateTypeRefBuffer);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x01; // arguments count

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &flowRecorderStateTypeRefBuffer, flowRecorderStateTypeRefSize);
        offset += flowRecorderStateTypeRefSize;

        signature[offset++] = ELEMENT_TYPE_I4; // methodMetadataIndex

        hr = module_metadata->metadata_emit->DefineMemberRef(
            flowRecorderTypeRef, managed_profiler_debugger_flow_recorder_enter_fast_name.data(), signature, offset,
            &flowRecorderEnterFastRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper flowRecorderEnterFastRef could not be defined.");
            return hr;
        }
    }

    *instruction = rewriterWrapper->CallMember(flowRecorderEnterFastRef, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteFlowRecorderEnterAsyncStep(void* rewriterWrapperPtr, ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*)rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (flowRecorderEnterAsyncStepRef == mdMemberRefNil)
    {
        unsigned flowRecorderStateTypeRefBuffer;
        const auto flowRecorderStateTypeRefSize =
            CorSigCompressToken(flowRecorderStateTypeRef, &flowRecorderStateTypeRefBuffer);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x03; // arguments count

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &flowRecorderStateTypeRefBuffer, flowRecorderStateTypeRefSize);
        offset += flowRecorderStateTypeRefSize;

        signature[offset++] = ELEMENT_TYPE_I4; // methodMetadataIndex
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_I8; // operationId
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_I8; // generation

        hr = module_metadata->metadata_emit->DefineMemberRef(
            flowRecorderTypeRef, managed_profiler_debugger_flow_recorder_enter_async_step_name.data(), signature, offset,
            &flowRecorderEnterAsyncStepRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper flowRecorderEnterAsyncStepRef could not be defined.");
            return hr;
        }
    }

    *instruction = rewriterWrapper->CallMember(flowRecorderEnterAsyncStepRef, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteFlowRecorderEnterAsyncStepFast(void* rewriterWrapperPtr, ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*)rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (flowRecorderEnterAsyncStepFastRef == mdMemberRefNil)
    {
        unsigned flowRecorderStateTypeRefBuffer;
        const auto flowRecorderStateTypeRefSize =
            CorSigCompressToken(flowRecorderStateTypeRef, &flowRecorderStateTypeRefBuffer);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x03; // arguments count

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &flowRecorderStateTypeRefBuffer, flowRecorderStateTypeRefSize);
        offset += flowRecorderStateTypeRefSize;

        signature[offset++] = ELEMENT_TYPE_I4; // methodMetadataIndex
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_I8; // operationId
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_I8; // generation

        hr = module_metadata->metadata_emit->DefineMemberRef(
            flowRecorderTypeRef, managed_profiler_debugger_flow_recorder_enter_async_step_fast_name.data(), signature,
            offset, &flowRecorderEnterAsyncStepFastRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper flowRecorderEnterAsyncStepFastRef could not be defined.");
            return hr;
        }
    }

    *instruction = rewriterWrapper->CallMember(flowRecorderEnterAsyncStepFastRef, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteFlowRecorderEnterDetached(void* rewriterWrapperPtr, ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*)rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (flowRecorderEnterDetachedRef == mdMemberRefNil)
    {
        unsigned flowRecorderStateTypeRefBuffer;
        const auto flowRecorderStateTypeRefSize =
            CorSigCompressToken(flowRecorderStateTypeRef, &flowRecorderStateTypeRefBuffer);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x01; // arguments count

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &flowRecorderStateTypeRefBuffer, flowRecorderStateTypeRefSize);
        offset += flowRecorderStateTypeRefSize;

        signature[offset++] = ELEMENT_TYPE_I4; // methodMetadataIndex

        hr = module_metadata->metadata_emit->DefineMemberRef(
            flowRecorderTypeRef, managed_profiler_debugger_flow_recorder_enter_detached_name.data(), signature, offset,
            &flowRecorderEnterDetachedRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper flowRecorderEnterDetachedRef could not be defined.");
            return hr;
        }
    }

    *instruction = rewriterWrapper->CallMember(flowRecorderEnterDetachedRef, false);
    return S_OK;
}

mdTypeRef DebuggerTokens::GetFlowRecorderStateTypeRef()
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return mdTypeRefNil;
    }

    return flowRecorderStateTypeRef;
}

HRESULT DebuggerTokens::WriteFlowRecorderExit(void* rewriterWrapperPtr, ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*)rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (flowRecorderExitRef == mdMemberRefNil)
    {
        unsigned flowRecorderStateTypeRefBuffer;
        const auto flowRecorderStateTypeRefSize =
            CorSigCompressToken(flowRecorderStateTypeRef, &flowRecorderStateTypeRefBuffer);

        unsigned exTypeRefBuffer;
        const auto exTypeRefSize = CorSigCompressToken(exTypeRef, &exTypeRefBuffer);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x02; // arguments count
        signature[offset++] = ELEMENT_TYPE_VOID;

        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &flowRecorderStateTypeRefBuffer, flowRecorderStateTypeRefSize);
        offset += flowRecorderStateTypeRefSize;

        signature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&signature[offset], &exTypeRefBuffer, exTypeRefSize);
        offset += exTypeRefSize;

        hr = module_metadata->metadata_emit->DefineMemberRef(
            flowRecorderTypeRef, managed_profiler_debugger_flow_recorder_exit_name.data(), signature, offset,
            &flowRecorderExitRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper flowRecorderExitRef could not be defined.");
            return hr;
        }
    }

    *instruction = rewriterWrapper->CallMember(flowRecorderExitRef, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteFlowRecorderExitFast(void* rewriterWrapperPtr, ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*)rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (flowRecorderExitFastRef == mdMemberRefNil)
    {
        unsigned flowRecorderStateTypeRefBuffer;
        const auto flowRecorderStateTypeRefSize =
            CorSigCompressToken(flowRecorderStateTypeRef, &flowRecorderStateTypeRefBuffer);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x01; // arguments count
        signature[offset++] = ELEMENT_TYPE_VOID;

        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &flowRecorderStateTypeRefBuffer, flowRecorderStateTypeRefSize);
        offset += flowRecorderStateTypeRefSize;

        hr = module_metadata->metadata_emit->DefineMemberRef(
            flowRecorderTypeRef, managed_profiler_debugger_flow_recorder_exit_fast_name.data(), signature, offset,
            &flowRecorderExitFastRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper flowRecorderExitFastRef could not be defined.");
            return hr;
        }
    }

    *instruction = rewriterWrapper->CallMember(flowRecorderExitFastRef, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteFlowRecorderShouldCaptureValues(void* rewriterWrapperPtr, ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*)rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (flowRecorderShouldCaptureValuesRef == mdMemberRefNil)
    {
        unsigned flowRecorderStateTypeRefBuffer;
        const auto flowRecorderStateTypeRefSize =
            CorSigCompressToken(flowRecorderStateTypeRef, &flowRecorderStateTypeRefBuffer);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;
        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x02; // state, phase
        signature[offset++] = ELEMENT_TYPE_BOOLEAN;
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &flowRecorderStateTypeRefBuffer, flowRecorderStateTypeRefSize);
        offset += flowRecorderStateTypeRefSize;
        signature[offset++] = ELEMENT_TYPE_I4;

        hr = module_metadata->metadata_emit->DefineMemberRef(
            flowRecorderTypeRef, managed_profiler_debugger_flow_recorder_should_capture_values_name.data(),
            signature, offset, &flowRecorderShouldCaptureValuesRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper flowRecorderShouldCaptureValuesRef could not be defined.");
            return hr;
        }
    }

    *instruction = rewriterWrapper->CallMember(flowRecorderShouldCaptureValuesRef, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteFlowRecorderLogArg(void* rewriterWrapperPtr, const TypeSignature& argument, ILInstr** instruction)
{
    return WriteFlowRecorderLogArgOrLocal(rewriterWrapperPtr, argument, instruction, true);
}

HRESULT DebuggerTokens::WriteFlowRecorderLogLocal(void* rewriterWrapperPtr, const TypeSignature& local, ILInstr** instruction)
{
    return WriteFlowRecorderLogArgOrLocal(rewriterWrapperPtr, local, instruction, false);
}

HRESULT DebuggerTokens::WriteFlowRecorderLogNamedField(
    void* rewriterWrapperPtr,
    const TypeSignature& field,
    ILInstr** instruction,
    mdMemberRef& fieldRef,
    const WSTRING& methodName)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*)rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (fieldRef == mdMemberRefNil)
    {
        unsigned flowRecorderStateTypeRefBuffer;
        const auto flowRecorderStateTypeRefSize =
            CorSigCompressToken(flowRecorderStateTypeRef, &flowRecorderStateTypeRefBuffer);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;
        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x01;
        signature[offset++] = 0x03; // field, name, state
        signature[offset++] = ELEMENT_TYPE_VOID;
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x00;
        signature[offset++] = ELEMENT_TYPE_STRING;
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &flowRecorderStateTypeRefBuffer, flowRecorderStateTypeRefSize);
        offset += flowRecorderStateTypeRefSize;

        hr = module_metadata->metadata_emit->DefineMemberRef(
            flowRecorderTypeRef, methodName.data(),
            signature, offset, &fieldRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper FlowRecorder named field member ref could not be defined.");
            return hr;
        }
    }

    PCCOR_SIGNATURE fieldSignatureBuffer;
    auto fieldSignatureSize = field.GetSignature(fieldSignatureBuffer);
    COR_SIGNATURE methodSpecSignature[signatureBufferSize];
    unsigned offset = 0;
    methodSpecSignature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
    methodSpecSignature[offset++] = 0x01;
    memcpy(&methodSpecSignature[offset], fieldSignatureBuffer, fieldSignatureSize);
    offset += fieldSignatureSize;

    mdMethodSpec methodSpec = mdMethodSpecNil;
    hr = module_metadata->metadata_emit->DefineMethodSpec(fieldRef, methodSpecSignature, offset,
                                                          &methodSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating FlowRecorder named field method spec.");
        return hr;
    }

    *instruction = rewriterWrapper->CallMember(methodSpec, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteFlowRecorderLogField(void* rewriterWrapperPtr, const TypeSignature& field, ILInstr** instruction)
{
    return WriteFlowRecorderLogNamedField(
        rewriterWrapperPtr,
        field,
        instruction,
        flowRecorderLogFieldRef,
        managed_profiler_debugger_flow_recorder_log_field_name);
}

HRESULT DebuggerTokens::WriteFlowRecorderLogFieldArgument(void* rewriterWrapperPtr, const TypeSignature& field, ILInstr** instruction)
{
    return WriteFlowRecorderLogNamedField(
        rewriterWrapperPtr,
        field,
        instruction,
        flowRecorderLogFieldArgumentRef,
        managed_profiler_debugger_flow_recorder_log_field_argument_name);
}

HRESULT DebuggerTokens::WriteFlowRecorderLogReturnValue(
    void* rewriterWrapperPtr,
    TypeSignature* returnArgument,
    ILInstr** instruction,
    mdMemberRef& returnRef,
    const WSTRING& methodName)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*)rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (returnRef == mdMemberRefNil)
    {
        unsigned flowRecorderStateTypeRefBuffer;
        const auto flowRecorderStateTypeRefSize =
            CorSigCompressToken(flowRecorderStateTypeRef, &flowRecorderStateTypeRefBuffer);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;
        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x01;
        signature[offset++] = 0x02; // value, state
        signature[offset++] = ELEMENT_TYPE_VOID;
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x00;
        signature[offset++] = ELEMENT_TYPE_BYREF;
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &flowRecorderStateTypeRefBuffer, flowRecorderStateTypeRefSize);
        offset += flowRecorderStateTypeRefSize;

        hr = module_metadata->metadata_emit->DefineMemberRef(
            flowRecorderTypeRef, methodName.data(),
            signature, offset, &returnRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper FlowRecorder return member ref could not be defined.");
            return hr;
        }
    }

    PCCOR_SIGNATURE returnSignatureBuffer;
    auto returnSignatureSize = returnArgument->GetSignature(returnSignatureBuffer);
    COR_SIGNATURE methodSpecSignature[signatureBufferSize];
    unsigned offset = 0;
    methodSpecSignature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
    methodSpecSignature[offset++] = 0x01;
    memcpy(&methodSpecSignature[offset], returnSignatureBuffer, returnSignatureSize);
    offset += returnSignatureSize;

    mdMethodSpec methodSpec = mdMethodSpecNil;
    hr = module_metadata->metadata_emit->DefineMethodSpec(returnRef, methodSpecSignature, offset,
                                                          &methodSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating FlowRecorder return method spec.");
        return hr;
    }

    *instruction = rewriterWrapper->CallMember(methodSpec, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteFlowRecorderLogReturn(void* rewriterWrapperPtr, TypeSignature* returnArgument, ILInstr** instruction)
{
    return WriteFlowRecorderLogReturnValue(
        rewriterWrapperPtr,
        returnArgument,
        instruction,
        flowRecorderLogReturnRef,
        managed_profiler_debugger_flow_recorder_log_return_name);
}

HRESULT DebuggerTokens::WriteFlowRecorderLogAsyncReturn(void* rewriterWrapperPtr, TypeSignature* returnArgument, ILInstr** instruction)
{
    return WriteFlowRecorderLogReturnValue(
        rewriterWrapperPtr,
        returnArgument,
        instruction,
        flowRecorderLogAsyncReturnRef,
        managed_profiler_debugger_flow_recorder_log_async_return_name);
}

} // namespace debugger