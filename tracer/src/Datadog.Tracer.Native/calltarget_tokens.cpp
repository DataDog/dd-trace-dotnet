#include "calltarget_tokens.h"

#include "dd_profiler_constants.h"
#include "il_rewriter_wrapper.h"
#include "logger.h"
#include "module_metadata.h"

namespace trace
{
const int signatureBufferSize = 500;

/**
 * CALLTARGET CONSTANTS
 **/

static const shared::WSTRING managed_profiler_calltarget_getdefaultvalue_name = WStr("GetDefaultValue");
static const shared::WSTRING managed_profiler_calltarget_statetype_getdefault_name = WStr("GetDefault");
static const shared::WSTRING managed_profiler_calltarget_returntype_getdefault_name = WStr("GetDefault");
static const shared::WSTRING managed_profiler_calltarget_returntype_getreturnvalue_name = WStr("GetReturnValue");

/**
 * PRIVATE
 **/

HRESULT CallTargetTokens::EnsureCorLibTokens()
{
    ModuleMetadata* module_metadata = GetMetadata();
    AssemblyProperty corAssemblyProperty = *module_metadata->corAssemblyProperty;

    // *** Ensure corlib assembly ref
    if (corLibAssemblyRef == mdAssemblyRefNil)
    {
        mdAssemblyRef newCorLibAssemblyRef;
        auto hr = module_metadata->assembly_emit->DefineAssemblyRef(
            corAssemblyProperty.ppbPublicKey, corAssemblyProperty.pcbPublicKey, corAssemblyProperty.szName.data(),
            &corAssemblyProperty.pMetaData, &corAssemblyProperty.pulHashAlgId, sizeof(corAssemblyProperty.pulHashAlgId),
            corAssemblyProperty.assemblyFlags, &newCorLibAssemblyRef);
        if (newCorLibAssemblyRef == mdAssemblyRefNil)
        {
            Logger::Warn("Wrapper corLibAssemblyRef could not be defined.");
            return hr;
        }
        corLibAssemblyRef = newCorLibAssemblyRef;
    }

    // *** Ensure System.Object type ref
    if (objectTypeRef == mdTypeRefNil)
    {
        mdTypeRef newObjectTypeRef;
        auto hr = module_metadata->metadata_emit->DefineTypeRefByName(corLibAssemblyRef, SystemObject, &newObjectTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper objectTypeRef could not be defined.");
            return hr;
        }
        objectTypeRef = newObjectTypeRef;
    }

    // *** Ensure System.Exception type ref
    if (exTypeRef == mdTypeRefNil)
    {
        mdTypeRef newExTypeRef;
        auto hr = module_metadata->metadata_emit->DefineTypeRefByName(corLibAssemblyRef, SystemException, &newExTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper exTypeRef could not be defined.");
            return hr;
        }
        exTypeRef = newExTypeRef;
    }

    // *** Ensure System.Type type ref
    if (typeRef == mdTypeRefNil)
    {
        mdTypeRef newTypeRef;
        auto hr = module_metadata->metadata_emit->DefineTypeRefByName(corLibAssemblyRef, SystemTypeName, &newTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper typeRef could not be defined.");
            return hr;
        }
        typeRef = newTypeRef;
    }

    // *** Ensure System.RuntimeTypeHandle type ref
    if (runtimeTypeHandleRef == mdTypeRefNil)
    {
        mdTypeRef newRuntimeTypeHandleRef;
        auto hr = module_metadata->metadata_emit->DefineTypeRefByName(corLibAssemblyRef, RuntimeTypeHandleTypeName,
                                                                      &newRuntimeTypeHandleRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper runtimeTypeHandleRef could not be defined.");
            return hr;
        }
        runtimeTypeHandleRef = newRuntimeTypeHandleRef;
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

        mdMemberRef newGetTypeFromHandleToken;
        auto hr = module_metadata->metadata_emit->DefineMemberRef(typeRef, GetTypeFromHandleMethodName, signature,
                                                                  offset, &newGetTypeFromHandleToken);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper getTypeFromHandleToken could not be defined.");
            return hr;
        }
        getTypeFromHandleToken = newGetTypeFromHandleToken;
    }

    // *** Ensure System.RuntimeMethodHandle type ref
    if (runtimeMethodHandleRef == mdTypeRefNil)
    {
        mdTypeRef newRuntimeMethodHandleRef;
        auto hr = module_metadata->metadata_emit->DefineTypeRefByName(corLibAssemblyRef, RuntimeMethodHandleTypeName,
                                                                      &newRuntimeMethodHandleRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper runtimeMethodHandleRef could not be defined.");
            return hr;
        }
        runtimeMethodHandleRef = newRuntimeMethodHandleRef;
    }

    return S_OK;
}

mdTypeRef CallTargetTokens::GetTargetStateTypeRef()
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return mdTypeRefNil;
    }
    return callTargetStateTypeRef;
}

mdTypeRef CallTargetTokens::GetTargetVoidReturnTypeRef()
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return mdTypeRefNil;
    }

    ModuleMetadata* module_metadata = GetMetadata();

    // *** Ensure calltargetreturn void type ref
    if (callTargetReturnVoidTypeRef == mdTypeRefNil)
    {
        mdTypeRef newCallTargetReturnVoidTypeRef;
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, GetCallTargetReturnType().data(), &newCallTargetReturnVoidTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper callTargetReturnVoidTypeRef could not be defined.");
            return mdTypeRefNil;
        }
        callTargetReturnVoidTypeRef = newCallTargetReturnVoidTypeRef;
    }

    return callTargetReturnVoidTypeRef;
}

mdMemberRef CallTargetTokens::GetCallTargetStateDefaultMemberRef()
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return mdMemberRefNil;
    }
    return callTargetStateTypeGetDefault;
}

mdMemberRef CallTargetTokens::GetCallTargetReturnVoidDefaultMemberRef()
{
    auto hr = EnsureBaseCalltargetTokens();
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

        mdMemberRef newCallTargetReturnVoidTypeGetDefault;
        hr = module_metadata->metadata_emit->DefineMemberRef(
            callTargetReturnVoidTypeRef, managed_profiler_calltarget_returntype_getdefault_name.data(), signature,
            signatureLength, &newCallTargetReturnVoidTypeGetDefault);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper callTargetReturnVoidTypeGetDefault could not be defined.");
            return mdMemberRefNil;
        }
        callTargetReturnVoidTypeGetDefault = newCallTargetReturnVoidTypeGetDefault;
    }

    return callTargetReturnVoidTypeGetDefault;
}

mdMemberRef CallTargetTokens::GetCallTargetReturnValueDefaultMemberRef(mdTypeSpec callTargetReturnTypeSpec)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return mdMemberRefNil;
    }
    if (callTargetReturnTypeRef == mdTypeRefNil)
    {
        Logger::Warn("Wrapper callTargetReturnTypeGetDefault could not be defined because callTargetReturnTypeRef is null.");
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
                                                         managed_profiler_calltarget_returntype_getdefault_name.data(),
                                                         signature, signatureLength, &callTargetReturnTypeGetDefault);
    if (FAILED(hr))
    {
        Logger::Warn("Wrapper callTargetReturnTypeGetDefault could not be defined.");
        return mdMemberRefNil;
    }

    return callTargetReturnTypeGetDefault;
}

mdMethodSpec CallTargetTokens::GetCallTargetDefaultValueMethodSpec(const TypeSignature* methodArgument)
{
    auto hr = EnsureBaseCalltargetTokens();
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

        mdMemberRef newGetDefaultMemberRef;
        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            callTargetTypeRef, managed_profiler_calltarget_getdefaultvalue_name.data(), signature, signatureLength,
            &newGetDefaultMemberRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper getDefaultMemberRef could not be defined.");
            return hr;
        }
        getDefaultMemberRef = newGetDefaultMemberRef;
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

HRESULT CallTargetTokens::ModifyLocalSig(ILRewriter* reWriter, TypeSignature* methodReturnValue,
                                         std::vector<TypeSignature>* methodTypeArguments,
                                         ULONG* callTargetStateIndex, ULONG* exceptionIndex,
                                         ULONG* callTargetReturnIndex, ULONG* returnValueIndex,
                                         mdToken* callTargetStateToken, mdToken* exceptionToken,
                                         mdToken* callTargetReturnToken, std::vector<ULONG>& additionalLocalIndices, bool isAsyncMethod)
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
        // should be the callTargetState)
        unsigned temp = 0;
        const auto len = CorSigCompressToken(callTargetStateTypeRef, &temp);
        if (originalSignatureSize > len)
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
    constexpr int variableNumber = 3;
    const auto additionalLocalsCount = GetAdditionalLocalsCount(*methodTypeArguments);
    ULONG newLocalsCount = variableNumber + additionalLocalsCount;

    // Gets the calltarget state type buffer and size
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
    const auto [retTypeElementType, retTypeFlags] = methodReturnValue->GetElementTypeAndFlags();

    if (retTypeFlags != TypeFlagVoid)
    {
        returnSignatureTypeSize = methodReturnValue->GetSignature(returnSignatureType);
        callTargetReturn = GetTargetReturnValueTypeRef(methodReturnValue);

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
    COR_SIGNATURE newSignatureBuffer[BUFFER_SIZE];
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
    
    // Add custom locals
    AddAdditionalLocals(methodReturnValue, methodTypeArguments, newSignatureBuffer, newSignatureOffset, newSignatureSize, isAsyncMethod);

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

    const auto sizeOfOtherIndexes = additionalLocalIndices.size();
    auto indexStart = variableNumber + additionalLocalsCount;

    if (returnSignatureType != nullptr)
    {
        *returnValueIndex = newLocalsCount - (indexStart + 1); // index+1 because return type adds an additional index comparing to void
    }
    else
    {
        *returnValueIndex = static_cast<ULONG>(ULONG_MAX);
    }

    *exceptionIndex = newLocalsCount - indexStart--;
    *callTargetReturnIndex = newLocalsCount - indexStart--;

    for (unsigned int i = 0; i < sizeOfOtherIndexes; i++)
    {
        additionalLocalIndices[i] = newLocalsCount - indexStart--;
    }

    *callTargetStateIndex = newLocalsCount - 1; // Must be the last local.
    return hr;
}

/**
 * PROTECTED
 **/

ModuleMetadata* CallTargetTokens::GetMetadata()
{
    return module_metadata_ptr;
}

HRESULT CallTargetTokens::EnsureBaseCalltargetTokens()
{
    std::lock_guard<std::recursive_mutex> guard(metadata_mutex);

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
            
        mdAssemblyRef newProfilerAssemblyRef;
        hr = module_metadata->assembly_emit->DefineAssemblyRef(&assemblyReference.public_key.data, public_key_size,
            assemblyReference.name.data(), &assembly_metadata, nullptr,
            0, 0, &newProfilerAssemblyRef);

        if (FAILED(hr))
        {
            Logger::Warn("Wrapper profilerAssemblyRef could not be defined.");
            return hr;
        }
        profilerAssemblyRef = newProfilerAssemblyRef;
    }

    // *** Ensure calltarget type ref
    if (callTargetTypeRef == mdTypeRefNil)
    {
        mdTypeRef newCallTargetTypeRef;
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, GetCallTargetType().data(), &newCallTargetTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper callTargetTypeRef could not be defined.");
            return hr;
        }
        callTargetTypeRef = newCallTargetTypeRef;
    }

    // *** Ensure calltargetstate type ref
    if (callTargetStateTypeRef == mdTypeRefNil)
    {
        mdTypeRef newCallTargetStateTypeRef;
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, GetCallTargetStateType().data(), &newCallTargetStateTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper callTargetStateTypeRef could not be defined.");
            return hr;
        }
        callTargetStateTypeRef = newCallTargetStateTypeRef;
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

        mdMemberRef newCallTargetStateTypeGetDefault;
        auto hr = module_metadata->metadata_emit->DefineMemberRef(
            callTargetStateTypeRef, managed_profiler_calltarget_statetype_getdefault_name.data(), signature,
            signatureLength, &newCallTargetStateTypeGetDefault);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper callTargetStateTypeGetDefault could not be defined.");
            return hr;
        }
        callTargetStateTypeGetDefault = newCallTargetStateTypeGetDefault;
    }

    // *** Ensure calltargetrefstruct type ref
    if (callTargetRefStructTypeRef == mdTypeRefNil) {
        if (GetCallTargetRefStructType() != EmptyWStr) {
            mdTypeRef newCallTargetRefStructTypeRef;
            hr = module_metadata->metadata_emit->DefineTypeRefByName(
                profilerAssemblyRef, GetCallTargetRefStructType().data(), &newCallTargetRefStructTypeRef);
            if (FAILED(hr)) {
                Logger::Warn("Wrapper callTargetRefStructTypeRef could not be defined.");
                return hr;
            }
            callTargetRefStructTypeRef = newCallTargetRefStructTypeRef;
        }
    }

    return S_OK;
}

mdTypeSpec CallTargetTokens::GetTargetReturnValueTypeRef(TypeSignature* returnArgument)
{
    auto hr = EnsureBaseCalltargetTokens();
    if (FAILED(hr))
    {
        return mdTypeSpecNil;
    }

    ModuleMetadata* module_metadata = GetMetadata();
    mdTypeSpec returnValueTypeSpec = mdTypeSpecNil;

    // *** Ensure calltargetreturn type ref
    if (callTargetReturnTypeRef == mdTypeRefNil)
    {
        mdTypeRef newCallTargetReturnTypeRef;
        hr = module_metadata->metadata_emit->DefineTypeRefByName(
            profilerAssemblyRef, GetCallTargetReturnGenericType().data(), &newCallTargetReturnTypeRef);
        if (FAILED(hr))
        {
            Logger::Warn("Wrapper callTargetReturnTypeRef could not be defined.");
            return mdTypeSpecNil;
        }
        callTargetReturnTypeRef = newCallTargetReturnTypeRef;
    }

    PCCOR_SIGNATURE returnSignatureBuffer;
    auto returnSignatureLength = returnArgument->GetSignature(returnSignatureBuffer);

    // Get The base calltargetReturnTypeRef Buffer and Size
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

mdToken CallTargetTokens::GetCurrentTypeRef(const TypeInfo* currentType, bool& isValueType)
{
    isValueType = currentType->valueType;

    if (currentType->type_spec != mdTypeSpecNil)
    {
        return currentType->type_spec;
    }

    // If the parent type is a generic type we cannot use the nested type Id.
    // So we check the parent type.
    if (currentType->parent_type != nullptr)
    {
        bool hasGenericParentType = false;
        TypeInfo* parentType = const_cast<TypeInfo*>(currentType->parent_type.get());
        while (true)
        {
            if (parentType->isGeneric)
            {
                hasGenericParentType = true;
                break;
            }

            if (parentType->parent_type == nullptr)
            {
                break;
            }

            parentType = const_cast<TypeInfo*>(parentType->parent_type.get());
        }

        // If the type has a generic parent type we need to check
        // if the current type is an object or an struct
        if (hasGenericParentType)
        {
            if (currentType->valueType)
            {
                // In case the current type is a struct we cannot do anything
                // So we return mdTokenNil
                return mdTokenNil;
            }
            else
            {
                // In case the current type is an object we can just use
                // the object type to do the rewrite
                return objectTypeRef;
            }
        }
    }

    // If the current type is a generic type we need to transverse the type.
    // to look for a non generic one that we can use.
    TypeInfo* cType = const_cast<TypeInfo*>(currentType);
    while (cType != nullptr && cType->isGeneric)
    {
        cType = const_cast<TypeInfo*>(cType->extend_from.get());
    }

    if (cType == nullptr)
    {
        return objectTypeRef;
    }

    isValueType = cType->valueType;
    return cType->id;
}


int CallTargetTokens::GetAdditionalLocalsCount(const std::vector<TypeSignature>& methodTypeArguments)
{
    return 0;
}

void CallTargetTokens::AddAdditionalLocals(TypeSignature* methodReturnValue, std::vector<TypeSignature>* methodTypeArguments,
                                           COR_SIGNATURE (&signatureBuffer)[BUFFER_SIZE], ULONG& signatureOffset,
                                           ULONG& signatureSize, bool isAsyncMethod)
{
}

CallTargetTokens::CallTargetTokens(ModuleMetadata* moduleMetadataPtr, const bool enableByRefInstrumentation,
                                   const bool enableCallTargetStateByRef) :
    module_metadata_ptr(moduleMetadataPtr),
    enable_by_ref_instrumentation(enableByRefInstrumentation),
    enable_calltarget_state_by_ref(enableCallTargetStateByRef)
{
}

/**
 * PUBLIC
 **/

mdTypeRef CallTargetTokens::GetObjectTypeRef()
{
    return objectTypeRef;
}

mdTypeRef CallTargetTokens::GetExceptionTypeRef()
{
    return exTypeRef;
}

mdTypeRef CallTargetTokens::GetRuntimeTypeHandleTypeRef()
{
    return runtimeTypeHandleRef;
}

mdTypeRef CallTargetTokens::GetRuntimeMethodHandleTypeRef()
{
    return runtimeMethodHandleRef;
}

mdAssemblyRef CallTargetTokens::GetCorLibAssemblyRef()
{
    return corLibAssemblyRef;
}

HRESULT CallTargetTokens::ModifyLocalSigAndInitialize(void* rewriterWrapperPtr, TypeSignature* methodReturnType,
                                                      std::vector<TypeSignature>* methodTypeArguments,
                                                      ULONG* callTargetStateIndex, ULONG* exceptionIndex,
                                                      ULONG* callTargetReturnIndex, ULONG* returnValueIndex,
                                                      mdToken* callTargetStateToken, mdToken* exceptionToken,
                                                      mdToken* callTargetReturnToken, ILInstr** firstInstruction,
                                                      std::vector<ULONG>& additionalLocalIndices, bool isAsyncMethod)
{
    ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*) rewriterWrapperPtr;

    // Modify the Local Var Signature of the method

    auto hr = ModifyLocalSig(rewriterWrapper->GetILRewriter(), methodReturnType, methodTypeArguments,
                             callTargetStateIndex, exceptionIndex, callTargetReturnIndex,
                             returnValueIndex, callTargetStateToken, exceptionToken, callTargetReturnToken,
                             additionalLocalIndices, isAsyncMethod);

    if (FAILED(hr))
    {
        Logger::Warn("ModifyLocalSig() failed.");
        return hr;
    }

    // Init locals
    if (*returnValueIndex != static_cast<ULONG>(ULONG_MAX))
    {
        *firstInstruction =
            rewriterWrapper->CallMember(GetCallTargetDefaultValueMethodSpec(methodReturnType), false);
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
    // We don't need to initialize calltarget state because is going to be initialized right after this method call.
    // So we can save 2 instructions.
    /*rewriterWrapper->CallMember(GetCallTargetStateDefaultMemberRef(), false);
    rewriterWrapper->StLocal(*callTargetStateIndex);*/
    return S_OK;
}

HRESULT CallTargetTokens::WriteCallTargetReturnGetReturnValue(void* rewriterWrapperPtr,
                                                              mdTypeSpec callTargetReturnTypeSpec,
                                                              ILInstr** instruction)
{
    auto hr = EnsureBaseCalltargetTokens();
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
        callTargetReturnTypeSpec, managed_profiler_calltarget_returntype_getreturnvalue_name.data(), signature,
        signatureLength, &callTargetReturnGetValueMemberRef);
    if (FAILED(hr))
    {
        Logger::Warn("Wrapper callTargetReturnGetValueMemberRef could not be defined.");
        return mdMemberRefNil;
    }

    *instruction = rewriterWrapper->CallMember(callTargetReturnGetValueMemberRef, false);
    return S_OK;
}
} // namespace trace
