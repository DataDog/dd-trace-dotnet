#include "calltarget_tokens.h"

#include "dd_profiler_constants.h"
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

  return S_OK;
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

mdTypeRef CallTargetTokens::GetTargetStateTypeRef() {
  auto hr = EnsureBaseCalltargetTokens();
  if (FAILED(hr)) {
    return mdTypeRefNil;
  }

  ModuleMetadata* module_metadata = GetMetadata();

  // *** Ensure calltargetstate type ref
  if (callTargetStateTypeRef == mdTypeRefNil) {
    hr = module_metadata->metadata_emit->DefineTypeRefByName(
        profilerAssemblyRef, managed_profiler_calltarget_statetype.data(),
        &callTargetStateTypeRef);
    if (FAILED(hr)) {
      Warn("Wrapper callTargetStateTypeRef could not be defined.");
      return mdTypeRefNil;
    }
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
      4 + callTargetReturnTypeRefSize + returnSignatureLength;
  auto* signature = new COR_SIGNATURE[signatureLength];
  unsigned offset = 0;

  signature[offset++] = ELEMENT_TYPE_GENERICINST;
  signature[offset++] = ELEMENT_TYPE_VALUETYPE;
  memcpy(&signature[offset], &callTargetReturnTypeRefBuffer,
         callTargetReturnTypeRefSize);
  offset += callTargetReturnTypeRefSize;
  signature[offset++] = 0x01;
  memcpy(&signature[offset], &returnSignatureBuffer, returnSignatureLength);
  offset += returnSignatureLength;

  hr = module_metadata->metadata_emit->GetTokenFromTypeSpec(
      signature, signatureLength, &returnValueTypeSpec);
  if (FAILED(hr)) {
    Warn("Error creating return value type spec");
    return mdTypeSpecNil;
  }

  {
    std::lock_guard<std::mutex> guard(mdTypeSpecMap_lock);
    mdTypeSpecMap[returnArgument] = returnValueTypeSpec;
  }

  return returnValueTypeSpec;
}

HRESULT CallTargetTokens::ModifyLocalSig(
    ILRewriter& reWriter, FunctionMethodArgument* methodReturnValue) {
  auto hr = EnsureBaseCalltargetTokens();
  if (FAILED(hr)) {
    return mdTypeSpecNil;
  }

  ModuleMetadata* module_metadata = GetMetadata();

  PCCOR_SIGNATURE originalSignature = NULL;
  ULONG originalSignatureSize = 0;
  mdToken localVarSig = reWriter.GetTkLocalVarSig();

  if (localVarSig != mdTokenNil) {
    IfFailRet(module_metadata->metadata_import->GetSigFromToken(
        localVarSig, &originalSignature, &originalSignatureSize));

    // Check if the localvarsig has been already rewritten
    unsigned temp = 0;
    const auto len = CorSigCompressToken(callTargetStateTypeRef, &temp);
    if (originalSignatureSize - len > 0) {
      if (originalSignature[originalSignatureSize - len - 1] ==
          ELEMENT_TYPE_CLASS) {
        if (memcmp(&originalSignature[originalSignatureSize - len], &temp,
                   len) == 0)
          return E_FAIL;
      }
    }
  }

  // Gets the calltarget state type buffer and size
  unsigned callTargetStateTypeRefBuffer;
  auto callTargetStateTypeRefSize = CorSigCompressToken(
      callTargetStateTypeRef, &callTargetStateTypeRefBuffer);

  // Gets the exception type buffer and size
  unsigned exTypeRefBuffer;
  auto exTypeRefSize = CorSigCompressToken(exTypeRef, &exTypeRefBuffer);

  // Gets the Return type signature
  PCCOR_SIGNATURE returnSignatureType = NULL;
  ULONG returnSignatureTypeSize;

  // Gets the CallTargetReturn<T> mdTypeSpec
  mdToken callTargetReturn = mdTokenNil;

  unsigned retTypeElementType;
  auto retTypeFlags = methodReturnValue->GetTypeFlags(retTypeElementType);
  if (retTypeFlags != TypeFlagVoid) {
    returnSignatureTypeSize =
        methodReturnValue->GetSignature(returnSignatureType);
    callTargetReturn = GetTargetReturnValueTypeRef(methodReturnValue);
  } else {
    callTargetReturn = GetTargetVoidReturnTypeRef();
  }

  return S_OK;
}

}  // namespace trace