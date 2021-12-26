#include "livedebugger_tokens.h"

#include "dd_profiler_constants.h"
#include "il_rewriter_wrapper.h"
#include "logger.h"
#include "module_metadata.h"

namespace trace
{

const int signatureBufferSize = 500;

/**
 * LIVEDEBUGGER CONSTANTS
 **/

static const WSTRING managed_profiler_livedebugger_type = WStr("Datadog.Trace.LiveDebugger.LiveDebuggerInvoker");
static const WSTRING managed_profiler_livedebugger_beginmethod_name = WStr("BeginMethod");
static const WSTRING managed_profiler_livedebugger_endmethod_name = WStr("EndMethod");
static const WSTRING managed_profiler_livedebugger_logexception_name = WStr("LogException");
static const WSTRING managed_profiler_livedebugger_getdefaultvalue_name = WStr("GetDefaultValue");

static const WSTRING managed_profiler_livedebugger_statetype =
    WStr("Datadog.Trace.ClrProfiler.LiveDebugger.LiveDebuggerState");
static const WSTRING managed_profiler_livedebugger_statetype_getdefault_name = WStr("GetDefault");

static const WSTRING managed_profiler_livedebugger_returntype =
    WStr("Datadog.Trace.ClrProfiler.LiveDebugger.LiveDebuggerReturn");
static const WSTRING managed_profiler_livedebugger_returntype_getdefault_name = WStr("GetDefault");

static const WSTRING managed_profiler_livedebugger_returntype_generics =
    WStr("Datadog.Trace.ClrProfiler.LiveDebugger.LiveDebuggerReturn`1");
static const WSTRING managed_profiler_livedebugger_returntype_getreturnvalue_name = WStr("GetReturnValue");

/**
 * PRIVATE
 **/

ModuleMetadata* LiveDebuggerTokens::GetMetadata()
{
    return module_metadata_ptr;
}

HRESULT LiveDebuggerTokens::EnsureCorLibTokens()
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

HRESULT LiveDebuggerTokens::EnsureBaseLivedebuggerTokens()
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

    // *** Ensure livedebugger type ref
    if (liveDebuggerTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_livedebugger_type.data(), &liveDebuggerTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper liveDebuggerTypeRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure livedebuggerstate type ref
    if (liveDebuggerStateTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_livedebugger_statetype.data(), &liveDebuggerStateTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper liveDebuggerStateTypeRef could not be defined.");
            return hr;
        }
    }

    // *** Ensure LiveDebuggerState.GetDefault() member ref
    if (liveDebuggerStateTypeGetDefault == mdMemberRefNil)
    {
        unsigned liveDebuggerStateTypeBuffer;
        auto liveDebuggerStateTypeSize = CorSigCompressToken(liveDebuggerStateTypeRef, &liveDebuggerStateTypeBuffer);

        const ULONG signatureLength = 3 + liveDebuggerStateTypeSize;
        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x00;

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &liveDebuggerStateTypeBuffer, liveDebuggerStateTypeSize);
        offset += liveDebuggerStateTypeSize;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            liveDebuggerStateTypeRef, managed_profiler_livedebugger_statetype_getdefault_name.data(), signature,
            signatureLength, &liveDebuggerStateTypeGetDefault);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper liveDebuggerStateTypeGetDefault could not be defined.");
            return hr;
        }
    }

    return S_OK;
}

mdTypeRef LiveDebuggerTokens::GetTargetStateTypeRef()
{
    auto hr = EnsureBaseLivedebuggerTokens();
    if (FAILED(hr))
    {
        return mdTypeRefNil;
    }
    return liveDebuggerStateTypeRef;
}

mdTypeRef LiveDebuggerTokens::GetTargetVoidReturnTypeRef()
{
    auto hr = EnsureBaseLivedebuggerTokens();
    if (FAILED(hr))
    {
        return mdTypeRefNil;
    }

    ModuleMetadata* module_metadata = GetMetadata();

    // *** Ensure livedebuggerreturn void type ref
    if (liveDebuggerReturnVoidTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_livedebugger_returntype.data(), &liveDebuggerReturnVoidTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper liveDebuggerReturnVoidTypeRef could not be defined.");
            return mdTypeRefNil;
        }
    }

    return liveDebuggerReturnVoidTypeRef;
}

mdTypeSpec LiveDebuggerTokens::GetTargetReturnValueTypeRef(FunctionMethodArgument* returnArgument)
{
    auto hr = EnsureBaseLivedebuggerTokens();
    if (FAILED(hr))
    {
        return mdTypeSpecNil;
    }

    ModuleMetadata* module_metadata = GetMetadata();
    mdTypeSpec returnValueTypeSpec = mdTypeSpecNil;

    // *** Ensure livedebuggerreturn type ref
    if (liveDebuggerReturnTypeRef == mdTypeRefNil)
    {
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, managed_profiler_livedebugger_returntype_generics.data(), &liveDebuggerReturnTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper liveDebuggerReturnTypeRef could not be defined.");
            return mdTypeSpecNil;
        }
    }

    PCCOR_SIGNATURE returnSignatureBuffer;
    auto returnSignatureLength = returnArgument->GetSignature(returnSignatureBuffer);

    // Get The base livedebuggerReturnTypeRef Buffer and Size
    unsigned liveDebuggerReturnTypeRefBuffer;
    auto liveDebuggerReturnTypeRefSize = CorSigCompressToken(liveDebuggerReturnTypeRef, &liveDebuggerReturnTypeRefBuffer);

    auto signatureLength = 3 + liveDebuggerReturnTypeRefSize + returnSignatureLength;
    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;

    signature[offset++] = ELEMENT_TYPE_GENERICINST;
    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &liveDebuggerReturnTypeRefBuffer, liveDebuggerReturnTypeRefSize);
    offset += liveDebuggerReturnTypeRefSize;
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

mdMemberRef LiveDebuggerTokens::GetLiveDebuggerStateDefaultMemberRef()
{
    auto hr = EnsureBaseLivedebuggerTokens();
    if (FAILED(hr))
    {
        return mdMemberRefNil;
    }
    return liveDebuggerStateTypeGetDefault;
}

mdMemberRef LiveDebuggerTokens::GetLiveDebuggerReturnVoidDefaultMemberRef()
{
    auto hr = EnsureBaseLivedebuggerTokens();
    if (FAILED(hr))
    {
        return mdMemberRefNil;
    }

    // *** Ensure LiveDebuggerReturn.GetDefault() member ref
    if (liveDebuggerReturnVoidTypeGetDefault == mdMemberRefNil)
    {
        ModuleMetadata* module_metadata = GetMetadata();

        unsigned liveDebuggerReturnVoidTypeBuffer;
        auto liveDebuggerReturnVoidTypeSize =
            CorSigCompressToken(liveDebuggerReturnVoidTypeRef, &liveDebuggerReturnVoidTypeBuffer);

        auto signatureLength = 3 + liveDebuggerReturnVoidTypeSize;
        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        signature[offset++] = 0x00;

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &liveDebuggerReturnVoidTypeBuffer, liveDebuggerReturnVoidTypeSize);
        offset += liveDebuggerReturnVoidTypeSize;

        hr = module_metadata->metadata_emit->DefineMemberRef(
            liveDebuggerReturnVoidTypeRef, managed_profiler_livedebugger_returntype_getdefault_name.data(), signature,
            signatureLength, &liveDebuggerReturnVoidTypeGetDefault);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper liveDebuggerReturnVoidTypeGetDefault could not be defined.");
            return mdMemberRefNil;
        }
    }

    return liveDebuggerReturnVoidTypeGetDefault;
}

mdMemberRef LiveDebuggerTokens::GetLiveDebuggerReturnValueDefaultMemberRef(mdTypeSpec liveDebuggerReturnTypeSpec)
{
    auto hr = EnsureBaseLivedebuggerTokens();
    if (FAILED(hr))
    {
        return mdMemberRefNil;
    }
    if (liveDebuggerReturnTypeRef == mdTypeRefNil)
    {
        Logger::Warn(
            "Wrapper liveDebuggerReturnTypeGetDefault could not be defined because liveDebuggerReturnTypeRef is null.");
        return mdMemberRefNil;
    }

    mdMemberRef liveDebuggerReturnTypeGetDefault = mdMemberRefNil;

    // *** Ensure LiveDebuggerReturn<T>.GetDefault() member ref
    ModuleMetadata* module_metadata = GetMetadata();

    unsigned liveDebuggerReturnTypeRefBuffer;
    auto liveDebuggerReturnTypeRefSize = CorSigCompressToken(liveDebuggerReturnTypeRef, &liveDebuggerReturnTypeRefBuffer);

    auto signatureLength = 7 + liveDebuggerReturnTypeRefSize;
    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
    signature[offset++] = 0x00;
    signature[offset++] = ELEMENT_TYPE_GENERICINST;
    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &liveDebuggerReturnTypeRefBuffer, liveDebuggerReturnTypeRefSize);
    offset += liveDebuggerReturnTypeRefSize;
    signature[offset++] = 0x01;
    signature[offset++] = ELEMENT_TYPE_VAR;
    signature[offset++] = 0x00;

    hr = module_metadata->metadata_emit->DefineMemberRef(liveDebuggerReturnTypeSpec,
                                                         managed_profiler_livedebugger_returntype_getdefault_name.data(),
                                                         signature, signatureLength, &liveDebuggerReturnTypeGetDefault);
    if (FAILED(hr))
    {
        Logger::Warn("Wrapper liveDebuggerReturnTypeGetDefault could not be defined.");
        return mdMemberRefNil;
    }

    return liveDebuggerReturnTypeGetDefault;
}

mdMethodSpec LiveDebuggerTokens::GetLiveDebuggerDefaultValueMethodSpec(FunctionMethodArgument* methodArgument)
{
    auto hr = EnsureBaseLivedebuggerTokens();
    if (FAILED(hr))
    {
        return mdMethodSpecNil;
    }

    mdMethodSpec getDefaultMethodSpec = mdMethodSpecNil;
    ModuleMetadata* module_metadata = GetMetadata();

    // *** Ensure we have the LiveDebuggerInvoker.GetDefaultValue<> memberRef
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
            liveDebuggerTypeRef, managed_profiler_livedebugger_getdefaultvalue_name.data(), signature, signatureLength,
            &getDefaultMemberRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper getDefaultMemberRef could not be defined.");
            return hr;
        }
    }

    // *** Create de MethodSpec using the FunctionMethodArgument

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

mdToken LiveDebuggerTokens::GetCurrentTypeRef(const TypeInfo* currentType, bool& isValueType)
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

HRESULT LiveDebuggerTokens::ModifyLocalSig(ILRewriter* reWriter, FunctionMethodArgument* methodReturnValue,
                                         ULONG* liveDebuggerStateIndex, ULONG* exceptionIndex,
                                         ULONG* liveDebuggerReturnIndex, ULONG* returnValueIndex,
                                         mdToken* liveDebuggerStateToken, mdToken* exceptionToken,
                                         mdToken* liveDebuggerReturnToken)
{
    auto hr = EnsureBaseLivedebuggerTokens();
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
        // should be the liveDebuggerState)
        unsigned temp = 0;
        const auto len = CorSigCompressToken(liveDebuggerStateTypeRef, &temp);
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

    // Gets the livedebugger state type buffer and size
    unsigned liveDebuggerStateTypeRefBuffer;
    auto liveDebuggerStateTypeRefSize = CorSigCompressToken(liveDebuggerStateTypeRef, &liveDebuggerStateTypeRefBuffer);

    // Gets the exception type buffer and size
    unsigned exTypeRefBuffer;
    auto exTypeRefSize = CorSigCompressToken(exTypeRef, &exTypeRefBuffer);

    // Gets the Return type signature
    PCCOR_SIGNATURE returnSignatureType = nullptr;
    ULONG returnSignatureTypeSize = 0;

    // Gets the LiveDebuggerReturn<T> mdTypeSpec
    mdToken liveDebuggerReturn = mdTokenNil;
    PCCOR_SIGNATURE liveDebuggerReturnSignature = nullptr;
    ULONG liveDebuggerReturnSignatureSize;
    unsigned liveDebuggerReturnBuffer;
    ULONG liveDebuggerReturnSize;
    ULONG liveDebuggerReturnSizeForNewSignature = 0;
    unsigned retTypeElementType;
    auto retTypeFlags = methodReturnValue->GetTypeFlags(retTypeElementType);

    if (retTypeFlags != TypeFlagVoid)
    {
        returnSignatureTypeSize = methodReturnValue->GetSignature(returnSignatureType);
        liveDebuggerReturn = GetTargetReturnValueTypeRef(methodReturnValue);

        hr = module_metadata->metadata_import->GetTypeSpecFromToken(liveDebuggerReturn, &liveDebuggerReturnSignature,
                                                                    &liveDebuggerReturnSignatureSize);
        if (FAILED(hr))
        {
            return E_FAIL;
        }

        liveDebuggerReturnSizeForNewSignature = liveDebuggerReturnSignatureSize;

        newLocalsCount++;
    }
    else
    {
        liveDebuggerReturn = GetTargetVoidReturnTypeRef();
        liveDebuggerReturnSize = CorSigCompressToken(liveDebuggerReturn, &liveDebuggerReturnBuffer);
        liveDebuggerReturnSizeForNewSignature = 1 + liveDebuggerReturnSize;
    }

    // New signature size
    ULONG newSignatureSize = originalSignatureSize + returnSignatureTypeSize + (1 + exTypeRefSize) +
                             liveDebuggerReturnSizeForNewSignature + (1 + liveDebuggerStateTypeRefSize);
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

    // LiveDebugger Return value
    if (liveDebuggerReturnSignature != nullptr)
    {
        memcpy(&newSignatureBuffer[newSignatureOffset], liveDebuggerReturnSignature, liveDebuggerReturnSignatureSize);
        newSignatureOffset += liveDebuggerReturnSignatureSize;
    }
    else
    {
        newSignatureBuffer[newSignatureOffset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&newSignatureBuffer[newSignatureOffset], &liveDebuggerReturnBuffer, liveDebuggerReturnSize);
        newSignatureOffset += liveDebuggerReturnSize;
    }

    // LiveDebugger state value
    newSignatureBuffer[newSignatureOffset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&newSignatureBuffer[newSignatureOffset], &liveDebuggerStateTypeRefBuffer, liveDebuggerStateTypeRefSize);
    newSignatureOffset += liveDebuggerStateTypeRefSize;

    // Get new locals token
    mdToken newLocalVarSig;
    hr = module_metadata->metadata_emit->GetTokenFromSig(newSignatureBuffer, newSignatureSize, &newLocalVarSig);
    if (FAILED(hr))
    {
        Logger::Warn("Error creating new locals var signature.");
        return hr;
    }

    reWriter->SetTkLocalVarSig(newLocalVarSig);
    *liveDebuggerStateToken = liveDebuggerStateTypeRef;
    *exceptionToken = exTypeRef;
    *liveDebuggerReturnToken = liveDebuggerReturn;
    if (returnSignatureType != nullptr)
    {
        *returnValueIndex = newLocalsCount - 4;
    }
    else
    {
        *returnValueIndex = static_cast<ULONG>(ULONG_MAX);
    }
    *exceptionIndex = newLocalsCount - 3;
    *liveDebuggerReturnIndex = newLocalsCount - 2;
    *liveDebuggerStateIndex = newLocalsCount - 1;
    return hr;
}

// slowpath BeginMethod
HRESULT LiveDebuggerTokens::WriteBeginMethodWithArgumentsArray(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                                             const TypeInfo* currentType, ILInstr** instruction)
{
    auto hr = EnsureBaseLivedebuggerTokens();
    if (FAILED(hr))
    {
        return hr;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (beginArrayMemberRef == mdMemberRefNil)
    {
        unsigned liveDebuggerStateBuffer;
        auto liveDebuggerStateSize = CorSigCompressToken(liveDebuggerStateTypeRef, &liveDebuggerStateBuffer);

        auto signatureLength = 8 + liveDebuggerStateSize;
        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x02;
        signature[offset++] = 0x02;

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &liveDebuggerStateBuffer, liveDebuggerStateSize);
        offset += liveDebuggerStateSize;

        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x01;

        signature[offset++] = ELEMENT_TYPE_SZARRAY;
        signature[offset++] = ELEMENT_TYPE_OBJECT;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(liveDebuggerTypeRef,
                                                                  managed_profiler_livedebugger_beginmethod_name.data(),
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
 * PUBLIC
 **/

LiveDebuggerTokens::LiveDebuggerTokens(ModuleMetadata* module_metadata_ptr, const bool enableByRefInstrumentation) :
    enable_by_ref_instrumentation(enableByRefInstrumentation)
{
    this->module_metadata_ptr = module_metadata_ptr;
    for (int i = 0; i < FASTPATH_COUNT; i++)
    {
        beginMethodFastPathRefs[i] = mdMemberRefNil;
    }
}

mdTypeRef LiveDebuggerTokens::GetObjectTypeRef()
{
    return objectTypeRef;
}
mdTypeRef LiveDebuggerTokens::GetExceptionTypeRef()
{
    return exTypeRef;
}
mdAssemblyRef LiveDebuggerTokens::GetCorLibAssemblyRef()
{
    return corLibAssemblyRef;
}

HRESULT LiveDebuggerTokens::ModifyLocalSigAndInitialize(void* rewriterWrapperPtr, FunctionInfo* functionInfo,
                                                      ULONG* liveDebuggerStateIndex, ULONG* exceptionIndex,
                                                      ULONG* liveDebuggerReturnIndex, ULONG* returnValueIndex,
                                                      mdToken* liveDebuggerStateToken, mdToken* exceptionToken,
                                                      mdToken* liveDebuggerReturnToken, ILInstr** firstInstruction)
{
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;

    // Modify the Local Var Signature of the method
    auto returnFunctionMethod = functionInfo->method_signature.GetRet();

    auto hr = ModifyLocalSig(rewriterWrapper->GetILRewriter(), &returnFunctionMethod, liveDebuggerStateIndex,
                             exceptionIndex, liveDebuggerReturnIndex, returnValueIndex, liveDebuggerStateToken,
                             exceptionToken, liveDebuggerReturnToken);

    if (FAILED(hr))
    {
        Logger::Warn("ModifyLocalSig() failed.");
        return hr;
    }

    // Init locals
    if (*returnValueIndex != static_cast<ULONG>(ULONG_MAX))
    {
        *firstInstruction =
            rewriterWrapper->CallMember(GetLiveDebuggerDefaultValueMethodSpec(&returnFunctionMethod), false);
        rewriterWrapper->StLocal(*returnValueIndex);

        rewriterWrapper->CallMember(GetLiveDebuggerReturnValueDefaultMemberRef(*liveDebuggerReturnToken), false);
        rewriterWrapper->StLocal(*liveDebuggerReturnIndex);
    }
    else
    {
        *firstInstruction = rewriterWrapper->CallMember(GetLiveDebuggerReturnVoidDefaultMemberRef(), false);
        rewriterWrapper->StLocal(*liveDebuggerReturnIndex);
    }
    rewriterWrapper->LoadNull();
    rewriterWrapper->StLocal(*exceptionIndex);
    // We don't need to initialize livedebugger state because is going to be initialized right after this method call.
    // So we can save 2 instructions.
    /*rewriterWrapper->CallMember(GetLiveDebuggerStateDefaultMemberRef(), false);
    rewriterWrapper->StLocal(*liveDebuggerStateIndex);*/
    return S_OK;
}

HRESULT LiveDebuggerTokens::WriteBeginMethod(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                           const TypeInfo* currentType,
                                           const std::vector<FunctionMethodArgument>& methodArguments,
                                           ILInstr** instruction)
{
    auto hr = EnsureBaseLivedebuggerTokens();
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

    if (beginMethodFastPathRefs[numArguments] == mdMemberRefNil)
    {
        unsigned liveDebuggerStateBuffer;
        auto liveDebuggerStateSize = CorSigCompressToken(liveDebuggerStateTypeRef, &liveDebuggerStateBuffer);

        unsigned long signatureLength;
        if (enable_by_ref_instrumentation)
        {
            signatureLength = 6 + (numArguments * 3) + liveDebuggerStateSize;
        }
        else
        {
            signatureLength = 6 + (numArguments * 2) + liveDebuggerStateSize;
        }
        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x02 + numArguments;
        signature[offset++] = 0x01 + numArguments;

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &liveDebuggerStateBuffer, liveDebuggerStateSize);
        offset += liveDebuggerStateSize;

        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x01;

        for (auto i = 0; i < numArguments; i++)
        {
            if (enable_by_ref_instrumentation)
            {
                signature[offset++] = ELEMENT_TYPE_BYREF;
            }
            signature[offset++] = ELEMENT_TYPE_MVAR;
            signature[offset++] = 0x01 + (i + 1);
        }

        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            liveDebuggerTypeRef, managed_profiler_livedebugger_beginmethod_name.data(), signature, signatureLength,
            &beginMethodFastPathRefs[numArguments]);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper beginMethod for ", numArguments, " arguments could not be defined.");
            return hr;
        }
    }

    mdMethodSpec beginMethodSpec = mdMethodSpecNil;

    unsigned integrationTypeBuffer;
    ULONG integrationTypeSize = CorSigCompressToken(integrationTypeRef, &integrationTypeBuffer);

    bool isValueType = currentType->valueType;
    mdToken currentTypeRef = GetCurrentTypeRef(currentType, isValueType);

    unsigned currentTypeBuffer;
    ULONG currentTypeSize = CorSigCompressToken(currentTypeRef, &currentTypeBuffer);

    auto signatureLength = 4 + integrationTypeSize + currentTypeSize;

    PCCOR_SIGNATURE argumentsSignatureBuffer[FASTPATH_COUNT];
    ULONG argumentsSignatureSize[FASTPATH_COUNT];
    unsigned elementType;
    for (auto i = 0; i < numArguments; i++)
    {
        const auto& argTypeFlags = methodArguments[i].GetTypeFlags(elementType);
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

    hr = module_metadata->metadata_emit->DefineMethodSpec(beginMethodFastPathRefs[numArguments], signature,
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
HRESULT LiveDebuggerTokens::WriteEndVoidReturnMemberRef(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                                      const TypeInfo* currentType, ILInstr** instruction)
{
    auto hr = EnsureBaseLivedebuggerTokens();
    if (FAILED(hr))
    {
        return hr;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    if (endVoidMemberRef == mdMemberRefNil)
    {
        unsigned liveDebuggerReturnVoidBuffer;
        auto liveDebuggerReturnVoidSize = CorSigCompressToken(liveDebuggerReturnVoidTypeRef, &liveDebuggerReturnVoidBuffer);

        unsigned exTypeRefBuffer;
        auto exTypeRefSize = CorSigCompressToken(exTypeRef, &exTypeRefBuffer);

        unsigned liveDebuggerStateBuffer;
        auto liveDebuggerStateSize = CorSigCompressToken(liveDebuggerStateTypeRef, &liveDebuggerStateBuffer);

        auto signatureLength = 8 + liveDebuggerReturnVoidSize + exTypeRefSize + liveDebuggerStateSize;
        COR_SIGNATURE signature[signatureBufferSize];
        unsigned offset = 0;

        signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
        signature[offset++] = 0x02;
        signature[offset++] = 0x03;

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &liveDebuggerReturnVoidBuffer, liveDebuggerReturnVoidSize);
        offset += liveDebuggerReturnVoidSize;

        signature[offset++] = ELEMENT_TYPE_MVAR;
        signature[offset++] = 0x01;

        signature[offset++] = ELEMENT_TYPE_CLASS;
        memcpy(&signature[offset], &exTypeRefBuffer, exTypeRefSize);
        offset += exTypeRefSize;

        signature[offset++] = ELEMENT_TYPE_VALUETYPE;
        memcpy(&signature[offset], &liveDebuggerStateBuffer, liveDebuggerStateSize);
        offset += liveDebuggerStateSize;

        auto hr = module_metadata->metadata_emit->DefineMemberRef(liveDebuggerTypeRef,
                                                                  managed_profiler_livedebugger_endmethod_name.data(),
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
HRESULT LiveDebuggerTokens::WriteEndReturnMemberRef(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                                  const TypeInfo* currentType, FunctionMethodArgument* returnArgument,
                                                  ILInstr** instruction)
{
    auto hr = EnsureBaseLivedebuggerTokens();
    if (FAILED(hr))
    {
        return hr;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();
    GetTargetReturnValueTypeRef(returnArgument);

    // *** Define base MethodMemberRef for the type

    mdMemberRef endMethodMemberRef = mdMemberRefNil;

    unsigned liveDebuggerReturnTypeRefBuffer;
    auto liveDebuggerReturnTypeRefSize = CorSigCompressToken(liveDebuggerReturnTypeRef, &liveDebuggerReturnTypeRefBuffer);

    unsigned exTypeRefBuffer;
    auto exTypeRefSize = CorSigCompressToken(exTypeRef, &exTypeRefBuffer);

    unsigned liveDebuggerStateBuffer;
    auto liveDebuggerStateSize = CorSigCompressToken(liveDebuggerStateTypeRef, &liveDebuggerStateBuffer);

    auto signatureLength = 14 + liveDebuggerReturnTypeRefSize + exTypeRefSize + liveDebuggerStateSize;
    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
    signature[offset++] = 0x03;
    signature[offset++] = 0x04;

    signature[offset++] = ELEMENT_TYPE_GENERICINST;
    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &liveDebuggerReturnTypeRefBuffer, liveDebuggerReturnTypeRefSize);
    offset += liveDebuggerReturnTypeRefSize;
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

    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &liveDebuggerStateBuffer, liveDebuggerStateSize);
    offset += liveDebuggerStateSize;

    hr = module_metadata->metadata_emit->DefineMemberRef(liveDebuggerTypeRef,
                                                         managed_profiler_livedebugger_endmethod_name.data(), signature,
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
HRESULT LiveDebuggerTokens::WriteLogException(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                            const TypeInfo* currentType, ILInstr** instruction)
{
    auto hr = EnsureBaseLivedebuggerTokens();
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

        auto hr = module_metadata->metadata_emit->DefineMemberRef(liveDebuggerTypeRef,
                                                                  managed_profiler_livedebugger_logexception_name.data(),
                                                                  signature, signatureLength, &logExceptionRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper logExceptionRef could not be defined.");
            return hr;
        }
    }

    mdMethodSpec logExceptionMethodSpec = mdMethodSpecNil;

    unsigned integrationTypeBuffer;
    ULONG integrationTypeSize = CorSigCompressToken(integrationTypeRef, &integrationTypeBuffer);

    bool isValueType = currentType->valueType;
    mdToken currentTypeRef = GetCurrentTypeRef(currentType, isValueType);

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

HRESULT LiveDebuggerTokens::WriteLiveDebuggerReturnGetReturnValue(void* rewriterWrapperPtr,
                                                              mdTypeSpec liveDebuggerReturnTypeSpec,
                                                              ILInstr** instruction)
{
    auto hr = EnsureBaseLivedebuggerTokens();
    if (FAILED(hr))
    {
        return mdMemberRefNil;
    }
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;
    ModuleMetadata* module_metadata = GetMetadata();

    // Ensure T LiveDebuggerReturn<T>.GetReturnValue() member ref
    mdMemberRef liveDebuggerReturnGetValueMemberRef = mdMemberRefNil;

    auto signatureLength = 4;
    COR_SIGNATURE signature[signatureBufferSize];
    unsigned offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT | IMAGE_CEE_CS_CALLCONV_HASTHIS;
    signature[offset++] = 0x00;
    signature[offset++] = ELEMENT_TYPE_VAR;
    signature[offset++] = 0x00;
    hr = module_metadata->metadata_emit->DefineMemberRef(
        liveDebuggerReturnTypeSpec, managed_profiler_livedebugger_returntype_getreturnvalue_name.data(), signature,
        signatureLength, &liveDebuggerReturnGetValueMemberRef);
    if (FAILED(hr))
    {
        Logger::Warn("Wrapper liveDebuggerReturnGetValueMemberRef could not be defined.");
        return mdMemberRefNil;
    }

    *instruction = rewriterWrapper->CallMember(liveDebuggerReturnGetValueMemberRef, false);
    return S_OK;
}

} // namespace trace
