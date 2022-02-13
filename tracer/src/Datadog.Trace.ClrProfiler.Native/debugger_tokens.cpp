#include "debugger_tokens.h"

#include "dd_profiler_constants.h"
#include "il_rewriter_wrapper.h"
#include "logger.h"
#include "module_metadata.h"

namespace debugger
{

const int signatureBufferSize = 500;

/**
 * CALLTARGET CONSTANTS
 **/

static const WSTRING managed_profiler_debugger_type = WStr("Datadog.Trace.Debugger.DebuggerInvoker");
static const WSTRING managed_profiler_debugger_beginmethod_startmarker_name = WStr("BeginMethod_StartMarker");
static const WSTRING managed_profiler_debugger_beginmethod_endmarker_name = WStr("BeginMethod_EndMarker");
static const WSTRING managed_profiler_debugger_endmethod_startmarker_name = WStr("EndMethod_StartMarker");
static const WSTRING managed_profiler_debugger_endmethod_endmarker_name = WStr("EndMethod_EndMarker");
static const WSTRING managed_profiler_debugger_logexception_name = WStr("LogException");
static const WSTRING managed_profiler_debugger_logarg_name = WStr("LogArg");
static const WSTRING managed_profiler_debugger_loglocal_name = WStr("LogLocal");
static const WSTRING managed_profiler_debugger_getdefaultvalue_name = WStr("GetDefaultValue");

static const WSTRING managed_profiler_debugger_statetype =
    WStr("Datadog.Trace.Debugger.DebuggerState");
static const WSTRING managed_profiler_debugger_statetype_getdefault_name = WStr("GetDefault");

static const WSTRING managed_profiler_debugger_returntype =
    WStr("Datadog.Trace.Debugger.DebuggerReturn");
static const WSTRING managed_profiler_debugger_returntype_getdefault_name = WStr("GetDefault");

static const WSTRING managed_profiler_debugger_returntype_generics =
    WStr("Datadog.Trace.Debugger.DebuggerReturn`1");
static const WSTRING managed_profiler_debugger_returntype_getreturnvalue_name = WStr("GetReturnValue");

/**
 * PRIVATE
 **/

ModuleMetadata* DebuggerTokens::GetMetadata()
{
    return module_metadata_ptr;
}

HRESULT DebuggerTokens::EnsureCorLibTokens()
{
    ModuleMetadata* module_metadata = GetMetadata();
    AssemblyProperty corAssemblyProperty = *module_metadata->corAssemblyProperty;

    // *** Ensure corlib assembly ref
    if (corLibAssemblyRef == mdAssemblyRefNil)
    {
        auto hr = module_metadata->assembly_emit->DefineAssemblyRef(
            corAssemblyProperty.ppbPublicKey, corAssemblyProperty.pcbPublicKey, corAssemblyProperty.szName.data(),
            &corAssemblyProperty.pMetaData, &corAssemblyProperty.pulHashAlgId, sizeof(corAssemblyProperty.pulHashAlgId),
            corAssemblyProperty.assemblyFlags, &corLibAssemblyRef);
        if (corLibAssemblyRef == mdAssemblyRefNil)
        {
            Logger::Warn("Wrapper corLibAssemblyRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure System.Object type ref
    if (objectTypeRef == mdTypeRefNil)
    {
        auto hr = module_metadata->metadata_emit->DefineTypeRefByName(corLibAssemblyRef, SystemObject, &objectTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper objectTypeRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure System.Exception type ref
    if (exTypeRef == mdTypeRefNil)
    {
        auto hr = module_metadata->metadata_emit->DefineTypeRefByName(corLibAssemblyRef, SystemException, &exTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper exTypeRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure System.Type type ref
    if (typeRef == mdTypeRefNil)
    {
        auto hr = module_metadata->metadata_emit->DefineTypeRefByName(corLibAssemblyRef, SystemTypeName, &typeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper typeRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure System.RuntimeTypeHandle type ref
    if (runtimeTypeHandleRef == mdTypeRefNil)
    {
        auto hr = module_metadata->metadata_emit->DefineTypeRefByName(corLibAssemblyRef, RuntimeTypeHandleTypeName,
                                                                      &runtimeTypeHandleRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper runtimeTypeHandleRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure Type.GetTypeFromHandle token
    if (getTypeFromHandleToken == mdTokenNil)
    {
        unsigned runtimeTypeHandle_buffer;
        auto runtimeTypeHandle_size = CorSigCompressToken(runtimeTypeHandleRef, &runtimeTypeHandle_buffer);

        unsigned type_buffer;
        auto type_size = CorSigCompressToken(typeRef, &type_buffer);

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x01;
        signature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&signature[offset], &type_buffer, type_size);
        offset += type_size;
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &runtimeTypeHandle_buffer, runtimeTypeHandle_size);
        offset += runtimeTypeHandle_size;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(typeRef, GetTypeFromHandleMethodName, signature,
                                                                  offset, &getTypeFromHandleToken);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper getTypeFromHandleToken could not be defined.");
            return hr;
        }
    }

    // *** Ensure System.RuntimeMethodHandle type ref
    if (runtimeMethodHandleRef == mdTypeRefNil)
    {
        auto hr = module_metadata->metadata_emit->DefineTypeRefByName(corLibAssemblyRef, RuntimeMethodHandleTypeName,
                                                                      &runtimeMethodHandleRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper runtimeMethodHandleRef could not be defined.");
            return hr;
        }
    }

    return S_OK;
}

HRESULT DebuggerTokens::EnsureBaseDebuggerTokens()
{
    auto hr = EnsureCorLibTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ModuleMetadata* module_metadata = GetMetadata();

    // *** Ensure profiler assembly ref
    if (profilerAssemblyRef == mdAssemblyRefNil)
    {
        const AssemblyReference assemblyReference =
            *trace::AssemblyReference::GetFromCache(managed_profiler_full_assembly_version);
        ASSEMBLYMETADATA assembly_metadata{};

        assembly_metadata.usMajorVersion = assemblyReference.version.major;
        assembly_metadata.usMinorVersion = assemblyReference.version.minor;
        assembly_metadata.usBuildNumber = assemblyReference.version.build;
        assembly_metadata.usRevisionNumber = assemblyReference.version.revision;
        if (assemblyReference.locale == WStr("neutral"))
        {
            assembly_metadata.szLocale = const_cast<WCHAR*>(WStr("\0"));
            assembly_metadata.cbLocale = 0;
        }
        else
        {
            assembly_metadata.szLocale = const_cast<WCHAR*>(assemblyReference.locale.c_str());
            assembly_metadata.cbLocale = (DWORD) (assemblyReference.locale.size());
        }

        DWORD public_key_size = 8;
        if (assemblyReference.public_key == trace::PublicKey())
        {
            public_key_size = 0;
        }

        hr = module_metadata->assembly_emit->DefineAssemblyRef(&assemblyReference.public_key.data, public_key_size,
                                                               assemblyReference.name.data(), &assembly_metadata, NULL,
                                                               0, 0, &profilerAssemblyRef);

        if (FAILED(hr))
        {
            Logger::Warn("Wrapper profilerAssemblyRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure debugger type ref
    if (callTargetTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_debugger_type.data(), &callTargetTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper callTargetTypeRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure debuggerstate type ref
    if (callTargetStateTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_debugger_statetype.data(), &callTargetStateTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper callTargetStateTypeRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure CallTargetState.GetDefault() member ref
    if (callTargetStateTypeGetDefault == mdMemberRefNil)
    {
        unsigned callTargetStateTypeBuffer;
        auto callTargetStateTypeSize = CorSigCompressToken(callTargetStateTypeRef, &callTargetStateTypeBuffer);

        const ULONG signatureLength = 3 + callTargetStateTypeSize;
        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x00;

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateTypeBuffer, callTargetStateTypeSize);
        offset += callTargetStateTypeSize;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            callTargetStateTypeRef, managed_profiler_debugger_statetype_getdefault_name.data(), signature,
            signatureLength, &callTargetStateTypeGetDefault);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper callTargetStateTypeGetDefault could not be defined.");
            return hr;
        }
    }

    return S_OK;
}

mdTypeRef DebuggerTokens::GetTargetStateTypeRef()
{
    auto hr = EnsureBaseDebuggerTokens();
    if (FAILED(hr))
    {
        return mdTypeRefNil;
    }
    return callTargetStateTypeRef;
}

mdTypeRef DebuggerTokens::GetTargetVoidReturnTypeRef()
{
    auto hr = EnsureBaseDebuggerTokens();
    if (FAILED(hr))
    {
        return mdTypeRefNil;
    }

    ModuleMetadata* module_metadata = GetMetadata();

    // *** Ensure debuggerreturn void type ref
    if (callTargetReturnVoidTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_debugger_returntype.data(), &callTargetReturnVoidTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper callTargetReturnVoidTypeRef could not be defined.");
            return mdTypeRefNil;
        }
    }

    return callTargetReturnVoidTypeRef;
}

mdTypeSpec DebuggerTokens::GetTargetReturnValueTypeRef(TypeSignature* returnArgument)
{
    auto hr = EnsureBaseDebuggerTokens();
    if (FAILED(hr))
    {
        return mdTypeSpecNil;
    }

    ModuleMetadata* module_metadata = GetMetadata();
    mdTypeSpec returnValueTypeSpec = mdTypeSpecNil;

    // *** Ensure debuggerreturn type ref
    if (callTargetReturnTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_debugger_returntype_generics.data(), &callTargetReturnTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper callTargetReturnTypeRef could not be defined.");
            return mdTypeSpecNil;
        }
    }

    PCCOR_SIGNATURE returnSignatureBuffer;
    auto returnSignatureLength = returnArgument->GetSignature(returnSignatureBuffer);

    // Get The base debuggerReturnTypeRef Buffer and Size
    unsigned callTargetReturnTypeRefBuffer;
    auto callTargetReturnTypeRefSize = CorSigCompressToken(callTargetReturnTypeRef, &callTargetReturnTypeRefBuffer);

    auto signatureLength = 3 + callTargetReturnTypeRefSize + returnSignatureLength;
    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;

    signature[offset++] = ELEMENT_TYPE_GENERICINST;
    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &callTargetReturnTypeRefBuffer, callTargetReturnTypeRefSize);
    offset += callTargetReturnTypeRefSize;
    signature[offset++] = 0x01;
    memcpy(&signature[offset], returnSignatureBuffer, returnSignatureLength);
    offset += returnSignatureLength;

    hr = module_metadata->metadata_emit->GetTokenFromTypeSpec(signature, signatureLength, &returnValueTypeSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating return value type spec");
        return mdTypeSpecNil;
    }

    return returnValueTypeSpec;
}

mdMemberRef DebuggerTokens::GetCallTargetStateDefaultMemberRef()
{
    auto hr = EnsureBaseDebuggerTokens();
    if (FAILED(hr))
    {
        return mdMemberRefNil;
    }
    return callTargetStateTypeGetDefault;
}

mdMemberRef DebuggerTokens::GetCallTargetReturnVoidDefaultMemberRef()
{
    auto hr = EnsureBaseDebuggerTokens();
    if (FAILED(hr))
    {
        return mdMemberRefNil;
    }

    // *** Ensure CallTargetReturn.GetDefault() member ref
    if (callTargetReturnVoidTypeGetDefault == mdMemberRefNil)
    {
        ModuleMetadata* module_metadata = GetMetadata();

        unsigned callTargetReturnVoidTypeBuffer;
        auto callTargetReturnVoidTypeSize =
            CorSigCompressToken(callTargetReturnVoidTypeRef, &callTargetReturnVoidTypeBuffer);

        auto signatureLength = 3 + callTargetReturnVoidTypeSize;
        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x00;

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetReturnVoidTypeBuffer, callTargetReturnVoidTypeSize);
        offset += callTargetReturnVoidTypeSize;

        hr = module_metadata->metadata_emit->DefineMemberRef(
            callTargetReturnVoidTypeRef, managed_profiler_debugger_returntype_getdefault_name.data(), signature,
            signatureLength, &callTargetReturnVoidTypeGetDefault);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper callTargetReturnVoidTypeGetDefault could not be defined.");
            return mdMemberRefNil;
        }
    }

    return callTargetReturnVoidTypeGetDefault;
}

mdMemberRef DebuggerTokens::GetCallTargetReturnValueDefaultMemberRef(mdTypeSpec callTargetReturnTypeSpec)
{
    auto hr = EnsureBaseDebuggerTokens();
    if (FAILED(hr))
    {
        return mdMemberRefNil;
    }
    if (callTargetReturnTypeRef == mdTypeRefNil)
    {
        Logger::Warn(
            "Wrapper callTargetReturnTypeGetDefault could not be defined because callTargetReturnTypeRef is null.");
        return mdMemberRefNil;
    }

    mdMemberRef callTargetReturnTypeGetDefault = mdMemberRefNil;

    // *** Ensure CallTargetReturn<T>.GetDefault() member ref
    ModuleMetadata* module_metadata = GetMetadata();

    unsigned callTargetReturnTypeRefBuffer;
    auto callTargetReturnTypeRefSize = CorSigCompressToken(callTargetReturnTypeRef, &callTargetReturnTypeRefBuffer);

    auto signatureLength = 7 + callTargetReturnTypeRefSize;
    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
    signature[offset++] = 0x00;
    signature[offset++] = ELEMENT_TYPE_GENERICINST;
    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &callTargetReturnTypeRefBuffer, callTargetReturnTypeRefSize);
    offset += callTargetReturnTypeRefSize;
    signature[offset++] = 0x01;
    signature[offset++] = ELEMENT_TYPE_VAR;
    signature[offset++] = 0x00;

    hr = module_metadata->metadata_emit->DefineMemberRef(callTargetReturnTypeSpec,
                                                         managed_profiler_debugger_returntype_getdefault_name.data(),
                                                         signature, signatureLength, &callTargetReturnTypeGetDefault);
    if (FAILED(hr))
    {
        Logger::Warn("Wrapper callTargetReturnTypeGetDefault could not be defined.");
        return mdMemberRefNil;
    }

    return callTargetReturnTypeGetDefault;
}

mdMethodSpec DebuggerTokens::GetCallTargetDefaultValueMethodSpec(TypeSignature* methodArgument)
{
    auto hr = EnsureBaseDebuggerTokens();
    if (FAILED(hr))
    {
        return mdMethodSpecNil;
    }

    mdMethodSpec getDefaultMethodSpec = mdMethodSpecNil;
    ModuleMetadata* module_metadata = GetMetadata();

    // *** Ensure we have the CallTargetInvoker.GetDefaultValue<> memberRef
    if (getDefaultMemberRef == mdMemberRefNil)
    {
        auto signatureLength = 5;
        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x01;
        signature[offset++] = 0x00;

        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x00;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            callTargetTypeRef, managed_profiler_debugger_getdefaultvalue_name.data(), signature, signatureLength,
            &getDefaultMemberRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper getDefaultMemberRef could not be defined.");
            return hr;
        }
    }

    // *** Create de MethodSpec using the TypeSignature

    // Gets the Return type signature
    PCCOR_SIGNATURE methodArgumentSignature = nullptr;
    ULONG methodArgumentSignatureSize;
    methodArgumentSignatureSize = methodArgument->GetSignature(methodArgumentSignature);

    auto signatureLength = 2 + methodArgumentSignatureSize;
    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;
    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
    signature[offset++] = 0x01;

    memcpy(&signature[offset], methodArgumentSignature, methodArgumentSignatureSize);
    offset += methodArgumentSignatureSize;

    hr = module_metadata->metadata_emit->DefineMethodSpec(getDefaultMemberRef, signature, signatureLength,
                                                          &getDefaultMethodSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating getDefaultMethodSpec.");
        return mdMethodSpecNil;
    }

    return getDefaultMethodSpec;
}

mdToken DebuggerTokens::GetCurrentTypeRef(const TypeInfo* currentType, bool& isValueType)
{
    if (currentType->type_spec != mdTypeSpecNil)
    {
        return currentType->type_spec;
    }
    else
    {

        TypeInfo* cType = const_cast<TypeInfo*>(currentType);
        while (!cType->isGeneric)
        {

            if (cType->parent_type == nullptr)
            {
                return cType->id;
            }

            cType = const_cast<TypeInfo*>(cType->parent_type.get());
        }

        isValueType = false;
        return objectTypeRef;
    }
}

HRESULT DebuggerTokens::ModifyLocalSig(ILRewriter* reWriter, TypeSignature* methodReturnValue,
                                         ULONG* callTargetStateIndex, ULONG* exceptionIndex,
                                         ULONG* callTargetReturnIndex, ULONG* returnValueIndex,
                                         mdToken* callTargetStateToken, mdToken* exceptionToken,
                                         mdToken* callTargetReturnToken)
{
    auto hr = EnsureBaseDebuggerTokens();
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
        // should be the callTargetState)
        unsigned temp = 0;
        const auto len = CorSigCompressToken(callTargetStateTypeRef, &temp);
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

    ULONG newLocalsCount = 3;

    // Gets the debugger state type buffer and size
    unsigned callTargetStateTypeRefBuffer;
    auto callTargetStateTypeRefSize = CorSigCompressToken(callTargetStateTypeRef, &callTargetStateTypeRefBuffer);

    // Gets the exception type buffer and size
    unsigned exTypeRefBuffer;
    auto exTypeRefSize = CorSigCompressToken(exTypeRef, &exTypeRefBuffer);

    // Gets the Return type signature
    PCCOR_SIGNATURE returnSignatureType = nullptr;
    ULONG returnSignatureTypeSize = 0;

    // Gets the CallTargetReturn<T> mdTypeSpec
    mdToken callTargetReturn = mdTokenNil;
    PCCOR_SIGNATURE callTargetReturnSignature = nullptr;
    ULONG callTargetReturnSignatureSize;
    unsigned callTargetReturnBuffer;
    ULONG callTargetReturnSize;
    ULONG callTargetReturnSizeForNewSignature = 0;
    unsigned retTypeElementType;
    auto retTypeFlags = methodReturnValue->GetTypeFlags(retTypeElementType);

    if (retTypeFlags != TypeFlagVoid)
    {
        returnSignatureTypeSize = methodReturnValue->GetSignature(returnSignatureType);
        callTargetReturn = GetTargetReturnValueTypeRef(methodReturnValue);

        if (callTargetReturn == mdTypeSpecNil)
        {
            Logger::Warn("Failed to get the return value (GetTargetReturnValueTypeRef).");
            return E_FAIL;
        }

        hr = module_metadata->metadata_import->GetTypeSpecFromToken(callTargetReturn, &callTargetReturnSignature,
                                                                    &callTargetReturnSignatureSize);
        if (FAILED(hr))
        {
            return E_FAIL;
        }

        callTargetReturnSizeForNewSignature = callTargetReturnSignatureSize;

        newLocalsCount++;
    }
    else
    {
        callTargetReturn = GetTargetVoidReturnTypeRef();
        callTargetReturnSize = CorSigCompressToken(callTargetReturn, &callTargetReturnBuffer);
        callTargetReturnSizeForNewSignature = 1 + callTargetReturnSize;
    }

    // New signature size
    ULONG newSignatureSize = originalSignatureSize + returnSignatureTypeSize + (1 + exTypeRefSize) +
                             callTargetReturnSizeForNewSignature + (1 + callTargetStateTypeRefSize);
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

    // Return value local
    if (returnSignatureType != nullptr)
    {
        memcpy(&newSignatureBuffer[newSignatureOffset], returnSignatureType, returnSignatureTypeSize);
        newSignatureOffset += returnSignatureTypeSize;
    }

    // Exception value
    newSignatureBuffer[newSignatureOffset++] = ELEMENT_TYPE_CLASS;
    memcpy(&newSignatureBuffer[newSignatureOffset], &exTypeRefBuffer, exTypeRefSize);
    newSignatureOffset += exTypeRefSize;

    // CallTarget Return value
    if (callTargetReturnSignature != nullptr)
    {
        memcpy(&newSignatureBuffer[newSignatureOffset], callTargetReturnSignature, callTargetReturnSignatureSize);
        newSignatureOffset += callTargetReturnSignatureSize;
    }
    else
    {
        newSignatureBuffer[newSignatureOffset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&newSignatureBuffer[newSignatureOffset], &callTargetReturnBuffer, callTargetReturnSize);
        newSignatureOffset += callTargetReturnSize;
    }

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
    *callTargetStateToken = callTargetStateTypeRef;
    *exceptionToken = exTypeRef;
    *callTargetReturnToken = callTargetReturn;
    if (returnSignatureType != nullptr)
    {
        *returnValueIndex = newLocalsCount - 4;
    }
    else
    {
        *returnValueIndex = static_cast<ULONG>(ULONG_MAX);
    }
    *exceptionIndex = newLocalsCount - 3;
    *callTargetReturnIndex = newLocalsCount - 2;
    *callTargetStateIndex = newLocalsCount - 1;
    return hr;
}

HRESULT DebuggerTokens::WriteLogArgOrLocal(void* rewriterWrapperPtr, const TypeSignature& argOrLocal,
    ILInstr** instruction, bool isArg)
{
    auto hr = EnsureBaseDebuggerTokens();
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

        unsigned long signatureLength;
        if (enable_by_ref_instrumentation)
        {
            signatureLength = 10 + +callTargetStateSize;
        }
        else
        {
            signatureLength = 8 + callTargetStateSize;
        }

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x01; // one generic argOrLocal (of the argOrLocal)
        signature[offset++] = 0x03; // (argumentIndex, argOrLocal, DebuggerState)

        signature[offset++] = ELEMENT_TYPE_VOID;

        // the argOrLocal
        if (enable_by_ref_instrumentation)
        {
            signature[offset++] = ELEMENT_TYPE_BYREF;
        }
        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x00;

        // argumentIndex
        signature[offset++] = ELEMENT_TYPE_I4;

        // DebuggerState
        if (enable_debugger_state_by_ref)
        {
            signature[offset++] = ELEMENT_TYPE_BYREF;
        }
        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(callTargetTypeRef, targetMemberName, signature, signatureLength, &logArgOrLocalRef);
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
    unsigned elementType;
    const auto& argTypeFlags = argOrLocal.GetTypeFlags(elementType);
    if (enable_by_ref_instrumentation && (argTypeFlags & TypeFlagByRef))
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

    hr = module_metadata->metadata_emit->DefineMethodSpec(logArgOrLocalRef, signature, signatureLength, &logArgMethodSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating log exception method spec.");
        return hr;
    }

    *instruction = rewriterWrapper->CallMember(logArgMethodSpec, false);
    return S_OK;
}

/**
 * PUBLIC
 **/

DebuggerTokens::DebuggerTokens(ModuleMetadata* module_metadata_ptr, const bool enableByRefInstrumentation,
                                   const bool enableCallTargetStateByRef) :
    enable_by_ref_instrumentation(enableByRefInstrumentation),
    enable_debugger_state_by_ref(enableCallTargetStateByRef)
{
    this->module_metadata_ptr = module_metadata_ptr;
    beginMethodStartMarkerRef = mdMemberRefNil;
}

mdTypeRef DebuggerTokens::GetObjectTypeRef()
{
    return objectTypeRef;
}
mdTypeRef DebuggerTokens::GetExceptionTypeRef()
{
    return exTypeRef;
}
mdAssemblyRef DebuggerTokens::GetCorLibAssemblyRef()
{
    return corLibAssemblyRef;
}

HRESULT DebuggerTokens::ModifyLocalSigAndInitialize(void* rewriterWrapperPtr, FunctionInfo* functionInfo,
                                                      ULONG* callTargetStateIndex, ULONG* exceptionIndex,
                                                      ULONG* callTargetReturnIndex, ULONG* returnValueIndex,
                                                      mdToken* callTargetStateToken, mdToken* exceptionToken,
                                                      mdToken* callTargetReturnToken, ILInstr** firstInstruction)
{
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;

    // Modify the Local Var Signature of the method
    auto returnFunctionMethod = functionInfo->method_signature.GetRet();

    auto hr = ModifyLocalSig(rewriterWrapper->GetILRewriter(), &returnFunctionMethod, callTargetStateIndex,
                             exceptionIndex, callTargetReturnIndex, returnValueIndex, callTargetStateToken,
                             exceptionToken, callTargetReturnToken);

    if (FAILED(hr))
    {
        Logger::Warn("ModifyLocalSig() failed.");
        return hr;
    }

    // Init locals
    if (*returnValueIndex != static_cast<ULONG>(ULONG_MAX))
    {
        *firstInstruction =
            rewriterWrapper->CallMember(GetCallTargetDefaultValueMethodSpec(&returnFunctionMethod), false);
        rewriterWrapper->StLocal(*returnValueIndex);

        rewriterWrapper->CallMember(GetCallTargetReturnValueDefaultMemberRef(*callTargetReturnToken), false);
        rewriterWrapper->StLocal(*callTargetReturnIndex);
    }
    else
    {
        *firstInstruction = rewriterWrapper->CallMember(GetCallTargetReturnVoidDefaultMemberRef(), false);
        rewriterWrapper->StLocal(*callTargetReturnIndex);
    }
    rewriterWrapper->LoadNull();
    rewriterWrapper->StLocal(*exceptionIndex);
    // We don't need to initialize debugger state because is going to be initialized right after this method call.
    // So we can save 2 instructions.
    /*rewriterWrapper->CallMember(GetCallTargetStateDefaultMemberRef(), false);
    rewriterWrapper->StLocal(*callTargetStateIndex);*/
    return S_OK;
}

HRESULT DebuggerTokens::WriteBeginMethod_StartMarker(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction)
{
    auto hr = EnsureBaseDebuggerTokens();
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

        unsigned long signatureLength = 6 + callTargetStateSize;

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x01; // generic arguments count
        signature[offset++] = 0x01; // arguments count

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;

        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x00;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            callTargetTypeRef, managed_profiler_debugger_beginmethod_startmarker_name.data(), signature, signatureLength, &beginMethodStartMarkerRef);
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

    hr = module_metadata->metadata_emit->DefineMethodSpec(beginMethodStartMarkerRef, signature, signatureLength, &beginMethodSpec);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating begin method spec.");
        return hr;
    }

    *instruction = rewriterWrapper->CallMember(beginMethodSpec, false);
    return S_OK;
}

// endmethod with void return
HRESULT DebuggerTokens::WriteEndVoidReturnMemberRef(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction)
{
    auto hr = EnsureBaseDebuggerTokens();
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
        if (enable_debugger_state_by_ref)
        {
            signatureLength++;
        }

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

        if (enable_debugger_state_by_ref)
        {
            signature[offset++] = ELEMENT_TYPE_BYREF;
        }

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
        offset += callTargetStateSize;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(callTargetTypeRef,
                                                                  managed_profiler_debugger_endmethod_startmarker_name.data(),
                                                                  signature, signatureLength, &endVoidMemberRef);
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
HRESULT DebuggerTokens::WriteEndReturnMemberRef(void* rewriterWrapperPtr, const TypeInfo* currentType, TypeSignature* returnArgument,
                                                  ILInstr** instruction)
{
    auto hr = EnsureBaseDebuggerTokens();
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
    if (enable_debugger_state_by_ref)
    {
        signatureLength++;
    }

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

    if (enable_debugger_state_by_ref)
    {
        signature[offset++] = ELEMENT_TYPE_BYREF;
    }
    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &callTargetStateBuffer, callTargetStateSize);
    offset += callTargetStateSize;

    hr = module_metadata->metadata_emit->DefineMemberRef(callTargetTypeRef,
                                                         managed_profiler_debugger_endmethod_startmarker_name.data(), signature,
                                                         signatureLength, &endMethodMemberRef);
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
HRESULT DebuggerTokens::WriteLogException(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction)
{
    auto hr = EnsureBaseDebuggerTokens();
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
        signature[offset++] = 0x01;
        signature[offset++] = 0x01;

        signature[offset++] = ELEMENT_TYPE_VOID;
        signature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&signature[offset], &exTypeRefBuffer, exTypeRefSize);
        offset += exTypeRefSize;

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

    *instruction = rewriterWrapper->CallMember(logExceptionMethodSpec, false);
    return S_OK;
}

HRESULT DebuggerTokens::WriteCallTargetReturnGetReturnValue(void* rewriterWrapperPtr,
                                                              mdTypeSpec callTargetReturnTypeSpec,
                                                              ILInstr** instruction)
{
    auto hr = EnsureBaseDebuggerTokens();
    if (FAILED(hr))
    {
        return mdMemberRefNil;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    // Ensure T CallTargetReturn<T>.GetReturnValue() member ref
    mdMemberRef callTargetReturnGetValueMemberRef = mdMemberRefNil;

    auto signatureLength = 4;
    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT | IMAGE_CEE_CS_CALLCONV_HASTHIS;
    signature[offset++] = 0x00;
    signature[offset++] = ELEMENT_TYPE_VAR;
    signature[offset++] = 0x00;
    hr = module_metadata->metadata_emit->DefineMemberRef(
        callTargetReturnTypeSpec, managed_profiler_debugger_returntype_getreturnvalue_name.data(), signature,
        signatureLength, &callTargetReturnGetValueMemberRef);
    if (FAILED(hr))
    {
        Logger::Warn("Wrapper callTargetReturnGetValueMemberRef could not be defined.");
        return mdMemberRefNil;
    }

    *instruction = rewriterWrapper->CallMember(callTargetReturnGetValueMemberRef, false);
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

HRESULT DebuggerTokens::WriteBeginOrEndMethod_EndMarker(void* rewriterWrapperPtr, bool isBeginMethod, ILInstr** instruction)
{
    auto hr = EnsureBaseDebuggerTokens();
    if (FAILED(hr))
    {
        return hr;
    }

    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    mdMemberRef beginOrEndMethodRef = isBeginMethod ? beginMethodEndMarkerRef : endMethodEndMarkerRef;
    const auto beginOrEndMethodName = isBeginMethod ? 
                                          managed_profiler_debugger_beginmethod_endmarker_name.data() :
                                          managed_profiler_debugger_endmethod_endmarker_name.data();

    if (beginOrEndMethodRef == mdMemberRefNil)
    {
        unsigned callTargetStateBuffer;
        auto callTargetStateSize = CorSigCompressToken(callTargetStateTypeRef, &callTargetStateBuffer);
        
        unsigned long signatureLength = 4 + callTargetStateSize;
        
        if (enable_debugger_state_by_ref)
        {
            signatureLength += 1; // accommodate for ELEMENT_TYPE_BYREF.
        }

        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x01; // arguments count
        signature[offset++] = ELEMENT_TYPE_VOID;
        
        // DebuggerState
        if (enable_debugger_state_by_ref)
        {
            signature[offset++] = ELEMENT_TYPE_BYREF;
        }
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
} // namespace trace
