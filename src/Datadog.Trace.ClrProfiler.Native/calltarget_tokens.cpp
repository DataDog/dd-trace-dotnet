#include "calltarget_tokens.h"

#include "dd_profiler_constants.h"
#include "il_rewriter_wrapper.h"
#include "logging.h"
#include "module_metadata.h"

namespace trace {

HRESULT CallTargetTokens::EnsureCorLibTokens() {
  ModuleMetadata* module_metadata = GetMetadata();
  AssemblyProperty corAssemblyProperty = *module_metadata->corAssemblyProperty;

  // *** Ensure corlib assembly ref
  if (corLibAssemblyRef == mdAssemblyRefNil) {
    auto hr = module_metadata->assembly_emit->DefineAssemblyRef(
        corAssemblyProperty.ppbPublicKey, corAssemblyProperty.pcbPublicKey,
        corAssemblyProperty.szName.data(), &corAssemblyProperty.pMetaData,
        &corAssemblyProperty.pulHashAlgId,
        sizeof(corAssemblyProperty.pulHashAlgId),
        corAssemblyProperty.assemblyFlags, &corLibAssemblyRef);
    if (corLibAssemblyRef == mdAssemblyRefNil) {
      Warn("Wrapper corLibAssemblyRef could not be defined.");
      return hr;
    }
  }

  // *** Ensure System.Object type ref
  if (objectTypeRef == mdTypeRefNil) {
    auto hr = module_metadata->metadata_emit->DefineTypeRefByName(
        corLibAssemblyRef, SystemObject.data(), &objectTypeRef);
    if (FAILED(hr)) {
      Warn("Wrapper objectTypeRef could not be defined.");
      return hr;
    }
  }

  // *** Ensure System.Exception type ref
  if (exTypeRef == mdTypeRefNil) {
    auto hr = module_metadata->metadata_emit->DefineTypeRefByName(
        corLibAssemblyRef, SystemException.data(), &exTypeRef);
    if (FAILED(hr)) {
      Warn("Wrapper exTypeRef could not be defined.");
      return hr;
    }
  }

  // *** Ensure System.Type type ref
  if (typeRef == mdTypeRefNil) {
    auto hr = module_metadata->metadata_emit->DefineTypeRefByName(
        corLibAssemblyRef, SystemTypeName.data(), &typeRef);
    if (FAILED(hr)) {
      Warn("Wrapper typeRef could not be defined.");
      return hr;
    }
  }

  // *** Ensure System.RuntimeTypeHandle type ref
  if (runtimeTypeHandleRef == mdTypeRefNil) {
    auto hr = module_metadata->metadata_emit->DefineTypeRefByName(
        corLibAssemblyRef, RuntimeTypeHandleTypeName.data(),
        &runtimeTypeHandleRef);
    if (FAILED(hr)) {
      Warn("Wrapper runtimeTypeHandleRef could not be defined.");
      return hr;
    }
  }

  // *** Ensure Type.GetTypeFromHandle token
  if (getTypeFromHandleToken == mdTokenNil) {
    unsigned runtimeTypeHandle_buffer;
    auto runtimeTypeHandle_size =
        CorSigCompressToken(runtimeTypeHandleRef, &runtimeTypeHandle_buffer);

    unsigned type_buffer;
    auto type_size = CorSigCompressToken(typeRef, &type_buffer);

    auto* signature = new COR_SIGNATURE[runtimeTypeHandle_size + type_size + 4];
    unsigned offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
    signature[offset++] = 0x01;
    signature[offset++] = ELEMENT_TYPE_CLASS;
    memcpy(&signature[offset], &type_buffer, type_size);
    offset += type_size;
    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &runtimeTypeHandle_buffer,
           runtimeTypeHandle_size);
    offset += runtimeTypeHandle_size;

    auto hr = module_metadata->metadata_emit->DefineMemberRef(
        typeRef, GetTypeFromHandleMethodName.data(), signature,
        sizeof(signature), &getTypeFromHandleToken);
    if (FAILED(hr)) {
      Warn("Wrapper getTypeFromHandleToken could not be defined.");
      return hr;
    }
  }

  // *** Ensure System.RuntimeMethodHandle type ref
  if (runtimeMethodHandleRef == mdTypeRefNil) {
    auto hr = module_metadata->metadata_emit->DefineTypeRefByName(
        corLibAssemblyRef, RuntimeMethodHandleTypeName.data(),
        &runtimeMethodHandleRef);
    if (FAILED(hr)) {
      Warn("Wrapper runtimeMethodHandleRef could not be defined.");
      return hr;
    }
  }

  return S_OK;
}

HRESULT CallTargetTokens::EnsureBaseCalltargetTokens() {
  auto hr = EnsureCorLibTokens();
  if (FAILED(hr)) {
    return hr;
  }

  ModuleMetadata* module_metadata = GetMetadata();

  // *** Ensure profiler assembly ref
  if (profilerAssemblyRef == mdAssemblyRefNil) {
    const AssemblyReference assemblyReference =
        managed_profiler_full_assembly_version;
    ASSEMBLYMETADATA assembly_metadata{};

    assembly_metadata.usMajorVersion = assemblyReference.version.major;
    assembly_metadata.usMinorVersion = assemblyReference.version.minor;
    assembly_metadata.usBuildNumber = assemblyReference.version.build;
    assembly_metadata.usRevisionNumber = assemblyReference.version.revision;
    if (assemblyReference.locale == "neutral"_W) {
      assembly_metadata.szLocale = const_cast<WCHAR*>("\0"_W.c_str());
      assembly_metadata.cbLocale = 0;
    } else {
      assembly_metadata.szLocale =
          const_cast<WCHAR*>(assemblyReference.locale.c_str());
      assembly_metadata.cbLocale = (DWORD)(assemblyReference.locale.size());
    }

    DWORD public_key_size = 8;
    if (assemblyReference.public_key == trace::PublicKey()) {
      public_key_size = 0;
    }

    hr = module_metadata->assembly_emit->DefineAssemblyRef(
        &assemblyReference.public_key.data, public_key_size,
        assemblyReference.name.data(), &assembly_metadata, NULL, NULL, 0,
        &profilerAssemblyRef);

    if (FAILED(hr)) {
      Warn("Wrapper profilerAssemblyRef could not be defined.");
      return hr;
    }
  }

  // *** Ensure calltarget type ref
  if (callTargetTypeRef == mdTypeRefNil) {
    hr = module_metadata->metadata_emit->DefineTypeRefByName(
        profilerAssemblyRef, managed_profiler_calltarget_type.data(),
        &callTargetTypeRef);
    if (FAILED(hr)) {
      Warn("Wrapper callTargetTypeRef could not be defined.");
      return hr;
    }
  }

  // *** Ensure calltargetstate type ref
  if (callTargetStateTypeRef == mdTypeRefNil) {
    hr = module_metadata->metadata_emit->DefineTypeRefByName(
        profilerAssemblyRef, managed_profiler_calltarget_statetype.data(),
        &callTargetStateTypeRef);
    if (FAILED(hr)) {
      Warn("Wrapper callTargetStateTypeRef could not be defined.");
      return hr;
    }
  }

  // *** Ensure CallTargetState.GetDefault() member ref
  if (callTargetStateTypeGetDefault == mdMemberRefNil) {
    unsigned callTargetStateTypeBuffer;
    auto callTargetStateTypeSize =
        CorSigCompressToken(callTargetStateTypeRef, &callTargetStateTypeBuffer);

    auto signatureLength = 3 + callTargetStateTypeSize;
    auto* signature = new COR_SIGNATURE[signatureLength];
    unsigned offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
    signature[offset++] = 0x00;

    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &callTargetStateTypeBuffer,
           callTargetStateTypeSize);
    offset += callTargetStateTypeSize;

    auto hr = module_metadata->metadata_emit->DefineMemberRef(
        callTargetStateTypeRef,
        managed_profiler_calltarget_statetype_getdefault_name.data(), signature,
        signatureLength, &callTargetStateTypeGetDefault);
    if (FAILED(hr)) {
      Warn("Wrapper callTargetStateTypeGetDefault could not be defined.");
      return hr;
    }

    Info("callTargetStateTypeGetDefault: ", HexStr(signature, signatureLength));
  }

  return S_OK;
}

mdTypeRef CallTargetTokens::GetTargetStateTypeRef() {
  auto hr = EnsureBaseCalltargetTokens();
  if (FAILED(hr)) {
    return mdTypeRefNil;
  }
  return callTargetStateTypeRef;
}

mdTypeRef CallTargetTokens::GetTargetVoidReturnTypeRef() {
  auto hr = EnsureBaseCalltargetTokens();
  if (FAILED(hr)) {
    return mdTypeRefNil;
  }

  ModuleMetadata* module_metadata = GetMetadata();

  // *** Ensure calltargetreturn void type ref
  if (callTargetReturnVoidTypeRef == mdTypeRefNil) {
    hr = module_metadata->metadata_emit->DefineTypeRefByName(
        profilerAssemblyRef, managed_profiler_calltarget_returntype.data(),
        &callTargetReturnVoidTypeRef);
    if (FAILED(hr)) {
      Warn("Wrapper callTargetReturnVoidTypeRef could not be defined.");
      return mdTypeRefNil;
    }
  }

  return callTargetReturnVoidTypeRef;
}

mdTypeSpec CallTargetTokens::GetTargetReturnValueTypeRef(
    FunctionMethodArgument* returnArgument) {
  {
    std::lock_guard<std::mutex> guard(mdTypeSpecMap_lock);
    if (mdTypeSpecMap.count(returnArgument) > 0) {
      return mdTypeSpecMap[returnArgument];
    }
  }

  auto hr = EnsureBaseCalltargetTokens();
  if (FAILED(hr)) {
    return mdTypeSpecNil;
  }

  ModuleMetadata* module_metadata = GetMetadata();
  mdTypeSpec returnValueTypeSpec = mdTypeSpecNil;

  // *** Ensure calltargetreturn type ref
  if (callTargetReturnTypeRef == mdTypeRefNil) {
    hr = module_metadata->metadata_emit->DefineTypeRefByName(
        profilerAssemblyRef,
        managed_profiler_calltarget_returntype_generics.data(),
        &callTargetReturnTypeRef);
    if (FAILED(hr)) {
      Warn("Wrapper callTargetReturnTypeRef could not be defined.");
      return mdTypeSpecNil;
    }
  }

  PCCOR_SIGNATURE returnSignatureBuffer;
  auto returnSignatureLength =
      returnArgument->GetSignature(returnSignatureBuffer);

  // Get The base calltargetReturnTypeRef Buffer and Size
  unsigned callTargetReturnTypeRefBuffer;
  auto callTargetReturnTypeRefSize = CorSigCompressToken(
      callTargetReturnTypeRef, &callTargetReturnTypeRefBuffer);

  auto signatureLength =
      3 + callTargetReturnTypeRefSize + returnSignatureLength;
  auto* signature = new COR_SIGNATURE[signatureLength];
  unsigned offset = 0;

  signature[offset++] = ELEMENT_TYPE_GENERICINST;
  signature[offset++] = ELEMENT_TYPE_VALUETYPE;
  memcpy(&signature[offset], &callTargetReturnTypeRefBuffer,
         callTargetReturnTypeRefSize);
  offset += callTargetReturnTypeRefSize;
  signature[offset++] = 0x01;
  memcpy(&signature[offset], returnSignatureBuffer, returnSignatureLength);
  offset += returnSignatureLength;

  hr = module_metadata->metadata_emit->GetTokenFromTypeSpec(
      signature, signatureLength, &returnValueTypeSpec);
  if (FAILED(hr)) {
    Warn("Error creating return value type spec");
    return mdTypeSpecNil;
  }

  Info("GetTargetReturnValueTypeRef: ", HexStr(signature, signatureLength));

  {
    std::lock_guard<std::mutex> guard(mdTypeSpecMap_lock);
    mdTypeSpecMap[returnArgument] = returnValueTypeSpec;
  }

  return returnValueTypeSpec;
}

mdMethodSpec CallTargetTokens::GetBeginMethodWithArgumentsArrayMemberRef(
    mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef) {
  return mdMethodSpecNil;
}

mdMethodSpec CallTargetTokens::GetBeginMethodWithoutArgumentsMemberRef(
    mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef) {
  return mdMethodSpecNil;
}

mdMethodSpec CallTargetTokens::GetBeginMethodWith1ArgumentsMemberRef(
    mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
    mdTypeRef arg1TypeRef) {
  return mdMethodSpecNil;
}

mdMethodSpec CallTargetTokens::GetBeginMethodWith2ArgumentsMemberRef(
    mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
    mdTypeRef arg1TypeRef, mdTypeRef arg2TypeRef) {
  return mdMethodSpecNil;
}

mdMethodSpec CallTargetTokens::GetBeginMethodWith3ArgumentsMemberRef(
    mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
    mdTypeRef arg1TypeRef, mdTypeRef arg2TypeRef, mdTypeRef arg3TypeRef) {
  return mdMethodSpecNil;
}

mdMethodSpec CallTargetTokens::GetBeginMethodWith4ArgumentsMemberRef(
    mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
    mdTypeRef arg1TypeRef, mdTypeRef arg2TypeRef, mdTypeRef arg3TypeRef,
    mdTypeRef arg4TypeRef) {
  return mdMethodSpecNil;
}

mdMethodSpec CallTargetTokens::GetBeginMethodWith5ArgumentsMemberRef(
    mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
    mdTypeRef arg1TypeRef, mdTypeRef arg2TypeRef, mdTypeRef arg3TypeRef,
    mdTypeRef arg4TypeRef, mdTypeRef arg5TypeRef) {
  return mdMethodSpecNil;
}

mdMethodSpec CallTargetTokens::GetBeginMethodWith6ArgumentsMemberRef(
    mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
    mdTypeRef arg1TypeRef, mdTypeRef arg2TypeRef, mdTypeRef arg3TypeRef,
    mdTypeRef arg4TypeRef, mdTypeRef arg5TypeRef, mdTypeRef arg6TypeRef) {
  return mdMethodSpecNil;
}

mdMethodSpec CallTargetTokens::GetEndVoidReturnMemberRef(
    mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef) {
  return mdMethodSpecNil;
}

mdMethodSpec CallTargetTokens::GetEndReturnMemberRef(
    mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
    mdTypeRef returnTypeRef) {
  return mdMethodSpecNil;
}

mdMethodSpec CallTargetTokens::GetLogExceptionMemberRef(
    mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef) {
  return mdMethodSpecNil;
}

HRESULT CallTargetTokens::ModifyLocalSig(
    ILRewriter* reWriter, FunctionMethodArgument* methodReturnValue,
    ULONG* callTargetStateIndex, ULONG* exceptionIndex,
    ULONG* callTargetReturnIndex, ULONG* returnValueIndex,
    mdToken* callTargetStateToken, mdToken* exceptionToken,
    mdToken* callTargetReturnToken) {
  auto hr = EnsureBaseCalltargetTokens();
  if (FAILED(hr)) {
    return hr;
  }

  ModuleMetadata* module_metadata = GetMetadata();

  PCCOR_SIGNATURE originalSignature = NULL;
  ULONG originalSignatureSize = 0;
  mdToken localVarSig = reWriter->GetTkLocalVarSig();

  if (localVarSig != mdTokenNil) {
    IfFailRet(module_metadata->metadata_import->GetSigFromToken(
        localVarSig, &originalSignature, &originalSignatureSize));

    // Check if the localvarsig has been already rewritten (the last local
    // should be the callTargetState)
    unsigned temp = 0;
    const auto len = CorSigCompressToken(callTargetStateTypeRef, &temp);
    if (originalSignatureSize - len > 0) {
      if (originalSignature[originalSignatureSize - len - 1] ==
          ELEMENT_TYPE_VALUETYPE) {
        if (memcmp(&originalSignature[originalSignatureSize - len], &temp,
                   len) == 0) {
          Warn("The signature for this method has been already modified.");
          return E_FAIL;
        }
      }
    }
  }

  ULONG newLocalsCount = 3;

  // Gets the calltarget state type buffer and size
  unsigned callTargetStateTypeRefBuffer;
  auto callTargetStateTypeRefSize = CorSigCompressToken(
      callTargetStateTypeRef, &callTargetStateTypeRefBuffer);

  Info("CallTargetState: ",
       HexStr(&callTargetStateTypeRefBuffer, callTargetStateTypeRefSize));

  // Gets the exception type buffer and size
  unsigned exTypeRefBuffer;
  auto exTypeRefSize = CorSigCompressToken(exTypeRef, &exTypeRefBuffer);

  Info("Exception: ", HexStr(&exTypeRefBuffer, exTypeRefSize));

  // Gets the Return type signature
  PCCOR_SIGNATURE returnSignatureType = NULL;
  ULONG returnSignatureTypeSize = 0;

  // Gets the CallTargetReturn<T> mdTypeSpec
  mdToken callTargetReturn = mdTokenNil;
  PCCOR_SIGNATURE callTargetReturnSignature = NULL;
  ULONG callTargetReturnSignatureSize;
  unsigned callTargetReturnBuffer;
  ULONG callTargetReturnSize;
  ULONG callTargetReturnSizeForNewSignature = 0;
  unsigned retTypeElementType;
  auto retTypeFlags = methodReturnValue->GetTypeFlags(retTypeElementType);

  if (retTypeFlags != TypeFlagVoid) {
    returnSignatureTypeSize = methodReturnValue->GetSignature(returnSignatureType);
    callTargetReturn = GetTargetReturnValueTypeRef(methodReturnValue);

    hr = module_metadata->metadata_import->GetTypeSpecFromToken(
        callTargetReturn, &callTargetReturnSignature,
        &callTargetReturnSignatureSize);
    if (FAILED(hr)) {
      return E_FAIL;
    }

    callTargetReturnSizeForNewSignature = callTargetReturnSignatureSize;

    Info("CallTargetReturn SIGNATURE: ",
         HexStr(callTargetReturnSignature, callTargetReturnSignatureSize));
    Info("ReturnValue: ", HexStr(returnSignatureType, returnSignatureTypeSize));

    newLocalsCount++;
  } else {
    callTargetReturn = GetTargetVoidReturnTypeRef();
    callTargetReturnSize = CorSigCompressToken(callTargetReturn, &callTargetReturnBuffer);
    callTargetReturnSizeForNewSignature = 1 + callTargetReturnSize;

    Info("CallTargetReturn: ", HexStr(&callTargetReturnBuffer, callTargetReturnSize));
  }

  // New signature size
  ULONG newSignatureSize =
      originalSignatureSize + returnSignatureTypeSize + (1 + exTypeRefSize) +
      callTargetReturnSizeForNewSignature + (1 + callTargetStateTypeRefSize);
  ULONG newSignatureOffset = 0;
  Info("New LocalVars Signature length: ", newSignatureSize);

  ULONG oldLocalsBuffer;
  ULONG oldLocalsLen = 0;
  unsigned newLocalsBuffer;
  ULONG newLocalsLen;

  // Calculate the new locals count
  if (originalSignatureSize == 0) {
    newSignatureSize += 2;
    newLocalsLen = CorSigCompressData(newLocalsCount, &newLocalsBuffer);
  } else {
    oldLocalsLen =
        CorSigUncompressData(originalSignature + 1, &oldLocalsBuffer);
    newLocalsCount += oldLocalsBuffer;
    newLocalsLen = CorSigCompressData(newLocalsCount, &newLocalsBuffer);
    newSignatureSize += newLocalsLen - oldLocalsLen;
  }

  // New signature declaration
  auto* newSignatureBuffer = new COR_SIGNATURE[newSignatureSize];
  newSignatureBuffer[newSignatureOffset++] = IMAGE_CEE_CS_CALLCONV_LOCAL_SIG;

  // Set the locals count
  memcpy(&newSignatureBuffer[newSignatureOffset], &newLocalsBuffer,
         newLocalsLen);
  newSignatureOffset += newLocalsLen;

  // Copy previous locals to the signature
  if (originalSignatureSize > 0) {
    const auto copyLength = originalSignatureSize - 1 - oldLocalsLen;
    memcpy(&newSignatureBuffer[newSignatureOffset],
           originalSignature + 1 + oldLocalsLen, copyLength);
    newSignatureOffset += copyLength;
  }

  // Add new locals

  // Return value local
  if (returnSignatureType != NULL) {
    memcpy(&newSignatureBuffer[newSignatureOffset], returnSignatureType,
           returnSignatureTypeSize);
    newSignatureOffset += returnSignatureTypeSize;
  }

  // Exception value
  newSignatureBuffer[newSignatureOffset++] = ELEMENT_TYPE_CLASS;
  memcpy(&newSignatureBuffer[newSignatureOffset], &exTypeRefBuffer,
         exTypeRefSize);
  newSignatureOffset += exTypeRefSize;

  // CallTarget Return value
  if (callTargetReturnSignature != NULL) {
    memcpy(&newSignatureBuffer[newSignatureOffset], callTargetReturnSignature,
           callTargetReturnSignatureSize);
    newSignatureOffset += callTargetReturnSignatureSize;
  } else {
    newSignatureBuffer[newSignatureOffset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&newSignatureBuffer[newSignatureOffset], &callTargetReturnBuffer,
           callTargetReturnSize);
    newSignatureOffset += callTargetReturnSize;
  }

  // CallTarget state value
  newSignatureBuffer[newSignatureOffset++] = ELEMENT_TYPE_VALUETYPE;
  memcpy(&newSignatureBuffer[newSignatureOffset], &callTargetStateTypeRefBuffer,
         callTargetStateTypeRefSize);
  newSignatureOffset += callTargetStateTypeRefSize;

  // Get new locals token
  mdToken newLocalVarSig;
  hr = module_metadata->metadata_emit->GetTokenFromSig(
      newSignatureBuffer, newSignatureSize, &newLocalVarSig);
  if (FAILED(hr)) {
    Warn("Error creating new locals var signature.");
    return hr;
  }

  Info("LocalVars Signature: ", HexStr(newSignatureBuffer, newSignatureSize));

  reWriter->SetTkLocalVarSig(newLocalVarSig);
  *callTargetStateToken = callTargetStateTypeRef;
  *exceptionToken = exTypeRef;
  *callTargetReturnToken = callTargetReturn;
  if (returnSignatureType != NULL) {
    *returnValueIndex = newLocalsCount - 4;
  } else {
    *returnValueIndex = ULONG_MAX;
  }
  *exceptionIndex = newLocalsCount - 3;
  *callTargetReturnIndex = newLocalsCount - 2;
  *callTargetStateIndex = newLocalsCount - 1;
  return hr;
}

mdMemberRef CallTargetTokens::GetCallTargetStateDefaultMemberRef() {
  auto hr = EnsureBaseCalltargetTokens();
  if (FAILED(hr)) {
    return mdMemberRefNil;
  }
  return callTargetStateTypeGetDefault;
}

mdMemberRef CallTargetTokens::GetCallTargetReturnVoidDefaultMemberRef() {
  auto hr = EnsureBaseCalltargetTokens();
  if (FAILED(hr)) {
    return mdMemberRefNil;
  }

  // *** Ensure CallTargetReturn.GetDefault() member ref
  if (callTargetReturnVoidTypeGetDefault == mdMemberRefNil) {
    ModuleMetadata* module_metadata = GetMetadata();

    unsigned callTargetReturnVoidTypeBuffer;
    auto callTargetReturnVoidTypeSize = CorSigCompressToken(
        callTargetReturnVoidTypeRef, &callTargetReturnVoidTypeBuffer);

    auto signatureLength = 3 + callTargetReturnVoidTypeSize;
    auto* signature = new COR_SIGNATURE[signatureLength];
    unsigned offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
    signature[offset++] = 0x00;

    signature[offset++] = ELEMENT_TYPE_VALUETYPE;
    memcpy(&signature[offset], &callTargetReturnVoidTypeBuffer,
           callTargetReturnVoidTypeSize);
    offset += callTargetReturnVoidTypeSize;

    hr = module_metadata->metadata_emit->DefineMemberRef(
        callTargetReturnVoidTypeRef,
        managed_profiler_calltarget_returntype_getdefault_name.data(),
        signature, signatureLength, &callTargetReturnVoidTypeGetDefault);
    if (FAILED(hr)) {
      Warn("Wrapper callTargetReturnVoidTypeGetDefault could not be defined.");
      return mdMemberRefNil;
    }

    Info("callTargetReturnVoidTypeGetDefault signature: ",
         HexStr(signature, signatureLength));
  }

  return callTargetReturnVoidTypeGetDefault;
}

mdMemberRef CallTargetTokens::GetCallTargetReturnValueDefaultMemberRef(
    mdTypeSpec callTargetReturnTypeSpec) {
  auto hr = EnsureBaseCalltargetTokens();
  if (FAILED(hr)) {
    return mdMemberRefNil;
  }
  if (callTargetReturnTypeRef == mdTypeRefNil) {
    return mdMemberRefNil;
  }

  mdMemberRef callTargetReturnTypeGetDefault = mdMemberRefNil;
  mdMethodSpec callTargetReturnTypeGetDefaultSpec = mdMethodSpecNil;

  // *** Ensure CallTargetReturn<T>.GetDefault() member ref
  ModuleMetadata* module_metadata = GetMetadata();

  unsigned callTargetReturnTypeRefBuffer;
  auto callTargetReturnTypeRefSize = CorSigCompressToken(
      callTargetReturnTypeRef, &callTargetReturnTypeRefBuffer);

  auto signatureLength = 7 + callTargetReturnTypeRefSize;
  auto* signature = new COR_SIGNATURE[signatureLength];
  unsigned offset = 0;

  signature[offset++] = IMAGE_CEE_CS_CALLCONV_DEFAULT;
  signature[offset++] = 0x00;
  signature[offset++] = ELEMENT_TYPE_GENERICINST;
  signature[offset++] = ELEMENT_TYPE_VALUETYPE;
  memcpy(&signature[offset], &callTargetReturnTypeRefBuffer,
         callTargetReturnTypeRefSize);
  offset += callTargetReturnTypeRefSize;
  signature[offset++] = 0x01;
  signature[offset++] = ELEMENT_TYPE_VAR;
  signature[offset++] = 0x00;

  hr = module_metadata->metadata_emit->DefineMemberRef(
      callTargetReturnTypeSpec,
      managed_profiler_calltarget_returntype_getdefault_name.data(), signature,
      signatureLength, &callTargetReturnTypeGetDefault);
  if (FAILED(hr)) {
    Warn("Wrapper callTargetReturnTypeGetDefault could not be defined.");
    return mdMemberRefNil;
  }
  Info("callTargetReturnTypeGetDefault signature: ",
       HexStr(signature, signatureLength));

  return callTargetReturnTypeGetDefault; 
}

mdMethodSpec CallTargetTokens::GetCallTargetDefaultValueMethodSpec(
    FunctionMethodArgument* methodArgument) {
  auto hr = EnsureBaseCalltargetTokens();
  if (FAILED(hr)) {
    return mdMethodSpecNil;
  }

  mdMethodSpec getDefaultMethodSpec = mdMethodSpecNil;
  ModuleMetadata* module_metadata = GetMetadata();

  // *** Ensure we have the CallTargetInvoker.GetDefault<> memberRef
  if (getDefaultMemberRef == mdMemberRefNil) {
    auto signatureLength = 5;
    auto* signature = new COR_SIGNATURE[signatureLength];
    unsigned offset = 0;

    signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERIC;
    signature[offset++] = 0x01;
    signature[offset++] = 0x00;

    signature[offset++] = ELEMENT_TYPE_MVAR;
    signature[offset++] = 0x00;

    auto hr = module_metadata->metadata_emit->DefineMemberRef(
        callTargetTypeRef,
        managed_profiler_calltarget_getdefaultvalue_name.data(), signature,
        signatureLength, &getDefaultMemberRef);
    if (FAILED(hr)) {
      Warn("Wrapper getDefaultMemberRef could not be defined.");
      return hr;
    }

    Info("getDefaultMemberRef signature: ", HexStr(signature, signatureLength));
  }

  // *** Create de MethodSpec using the FunctionMethodArgument

  // Gets the Return type signature
  PCCOR_SIGNATURE methodArgumentSignature = NULL;
  ULONG methodArgumentSignatureSize;
  methodArgumentSignatureSize =
      methodArgument->GetSignature(methodArgumentSignature);

  auto signatureLength = 2 + methodArgumentSignatureSize;
  auto* signature = new COR_SIGNATURE[signatureLength];
  unsigned offset = 0;
  signature[offset++] = IMAGE_CEE_CS_CALLCONV_GENERICINST;
  signature[offset++] = 0x01;

  memcpy(&signature[offset], methodArgumentSignature,
         methodArgumentSignatureSize);
  offset += methodArgumentSignatureSize;

  hr = module_metadata->metadata_emit->DefineMethodSpec(
      getDefaultMemberRef, signature, signatureLength, &getDefaultMethodSpec);

  if (FAILED(hr)) {
    Warn("Error creating getDefaultMethodSpec.");
    return mdMethodSpecNil;
  }

  Info("getDefaultMethodSpec signature: ", HexStr(signature, signatureLength));

  return getDefaultMethodSpec;
}

HRESULT CallTargetTokens::ModifyLocalSigAndInitialize(
    void* rewriterWrapperPtr, FunctionInfo* functionInfo) {
  ILRewriterWrapper* rewriterWrapper = (ILRewriterWrapper*)rewriterWrapperPtr;

  // Modify the Local Var Signature of the method
  auto returnFunctionMethod = functionInfo->method_signature.GetRet();

  ULONG callTargetStateIndex = ULONG_MAX;
  ULONG exceptionIndex = ULONG_MAX;
  ULONG callTargetReturnIndex = ULONG_MAX;
  ULONG returnValueIndex = ULONG_MAX;
  mdToken callTargetStateToken = mdTokenNil;
  mdToken exceptionToken = mdTokenNil;
  mdToken callTargetReturnToken = mdTokenNil;

  Info("ModifyLocalSigAndInitialize: Modifying the locals var signature.");
  auto hr = ModifyLocalSig(rewriterWrapper->GetILRewriter(),
                           &returnFunctionMethod, &callTargetStateIndex,
                           &exceptionIndex,
      &callTargetReturnIndex, &returnValueIndex, &callTargetStateToken,
      &exceptionToken, &callTargetReturnToken);

  if (FAILED(hr)) {
    Warn("ModifyLocalSig() failed.");
    return hr;
  }

   // Init locals
  Info("ModifyLocalSigAndInitialize: Initializing new locals vars.");
  if (returnValueIndex != ULONG_MAX) {
    rewriterWrapper->CallMember(GetCallTargetDefaultValueMethodSpec(&returnFunctionMethod), false);
    rewriterWrapper->StLocal(returnValueIndex);

    rewriterWrapper->CallMember(GetCallTargetReturnValueDefaultMemberRef(callTargetReturnToken), false);
    rewriterWrapper->StLocal(callTargetReturnIndex);
  } else {
    rewriterWrapper->CallMember(GetCallTargetReturnVoidDefaultMemberRef(), false);
    rewriterWrapper->StLocal(callTargetReturnIndex);
  }
  rewriterWrapper->LoadNull();
  rewriterWrapper->StLocal(exceptionIndex);
  rewriterWrapper->CallMember(GetCallTargetStateDefaultMemberRef(), false);
  rewriterWrapper->StLocal(callTargetStateIndex);

  return S_OK;
}

}  // namespace trace