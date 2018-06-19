#include <fstream>
#include "MetadataBuilder.h"
#include "Macros.h"

HRESULT MetadataBuilder::ResolveType(const TypeReference& type)
{
    mdTypeRef typeRef = mdTypeRefNil;
    return ResolveType(type, typeRef);
}

HRESULT MetadataBuilder::ResolveType(const TypeReference& type, mdTypeRef& typeRefOut)
{
    HRESULT hr;

    if (metadata.TryGetRef(type, typeRefOut))
    {
        // this type was already resolved
        return S_OK;
    }

    if (metadata.assemblyName == type.AssemblyName)
    {
        // type is defined in this assembly
        mdTypeRef typeRef = mdTypeRefNil;
        hr = metadataEmit->DefineTypeRefByName(module, type.TypeName.c_str(), &typeRef);

        if (SUCCEEDED(hr))
        {
            metadata.SetRef(type, typeRef);
        }
    }
    else
    {
        // type is defined in another assembly,
        // find a reference to the assembly where type lives
        mdAssemblyRef assemblyRef = mdAssemblyRefNil;
        hr = FindAssemblyRef(type.AssemblyName, &assemblyRef);

        // TODO: emit assembly reference if not found

        if (SUCCEEDED(hr))
        {
            // search for an existing reference to the type
            mdTypeRef typeRef = mdTypeRefNil;
            hr = metadataImport->FindTypeRef(assemblyRef, type.TypeName.c_str(), &typeRef);

            if (hr == HRESULT(0x80131130) /* record not found on lookup */)
            {
                // if typeRef not found, create a new one by emiting a metadata token
                hr = metadataEmit->DefineTypeRefByName(assemblyRef, type.TypeName.c_str(), &typeRef);
            }

            if (SUCCEEDED(hr))
            {
                metadata.SetRef(type, typeRef);
                typeRefOut = typeRef;
            }
        }
    }

    return S_OK;
}

HRESULT MetadataBuilder::ResolveMember(const MemberReference& member)
{
    mdMemberRef memberRef = mdMemberRefNil;
    return ResolveMember(member, memberRef);
}

HRESULT MetadataBuilder::ResolveMember(const MemberReference& member, mdMemberRef& memberRefOut)
{
    if (metadata.TryGetRef(member, memberRefOut))
    {
        // this member was already resolved
        return S_OK;
    }

    mdTypeRef containingTypeRef;
    HRESULT hr = ResolveType(member.ContainingType, containingTypeRef);
    RETURN_IF_FAILED(hr);

    hr = ResolveType(member.ReturnType);
    RETURN_IF_FAILED(hr);

    for (const TypeReference& argumentType : member.ArgumentTypes)
    {
        hr = ResolveType(argumentType);
        RETURN_IF_FAILED(hr);
    }

    COR_SIGNATURE pSignature[128]{};
    ULONG signatureLength;
    hr = CreateSignature(member, pSignature, _countof(pSignature), signatureLength);
    RETURN_IF_FAILED(hr);

    mdMemberRef memberRef = mdMemberRefNil;
    hr = metadataImport->FindMemberRef(containingTypeRef, member.MethodName.c_str(), pSignature, signatureLength, &memberRef);

    if (hr == HRESULT(0x80131130) /* record not found on lookup */)
    {
        // if memberRef not found, create it by emiting a metadata token
        hr = metadataEmit->DefineMemberRef(containingTypeRef, member.MethodName.c_str(), pSignature, signatureLength, &memberRef);
    }

    if (SUCCEEDED(hr))
    {
        metadata.SetRef(member, memberRef);
        memberRefOut = memberRef;
    }

    return S_OK;
}

HRESULT MetadataBuilder::EmitAssemblyRef(const std::wstring& assemblyName,
                                        const ASSEMBLYMETADATA& assemblyMetadata,
                                        BYTE publicKeyToken[],
                                        ULONG publicKeyTokenLength,
                                        mdAssemblyRef& assemblyRef) const
{
    const HRESULT hr = assemblyEmit->DefineAssemblyRef(static_cast<void *>(publicKeyToken),
                                                       publicKeyTokenLength,
                                                       assemblyName.c_str(),
                                                       &assemblyMetadata,
                                                       // hash blob
                                                       nullptr,
                                                       // cb of hash blob
                                                       0,
                                                       // flags
                                                       0,
                                                       &assemblyRef);

    LOG_IFFAILEDRET(hr, L"DefineAssemblyRef failed");
    return S_OK;
}

HRESULT MetadataBuilder::FindAssemblyRef(const std::wstring& assemblyName,
                                        mdAssemblyRef* assemblyRef)
{
    HCORENUM hEnum = nullptr;
    mdAssemblyRef rgAssemblyRefs[20];
    ULONG cAssemblyRefsReturned;

    do
    {
        const HRESULT hr = assemblyImport->EnumAssemblyRefs(&hEnum,
                                                            rgAssemblyRefs,
                                                            _countof(rgAssemblyRefs),
                                                            &cAssemblyRefsReturned);

        LOG_IFFAILEDRET(hr, L"EnumAssemblyRefs failed, hr = " << HEX(hr));

        if (cAssemblyRefsReturned == 0)
        {
            assemblyImport->CloseEnum(hEnum);
            LOG_APPEND(L"Could not find an AssemblyRef to " << assemblyName);
            return E_FAIL;
        }
    }
    while (FindAssemblyRefIterator(assemblyName,
                                   rgAssemblyRefs,
                                   cAssemblyRefsReturned,
                                   assemblyRef) < S_OK);

    assemblyImport->CloseEnum(hEnum);
    return S_OK;
}


HRESULT MetadataBuilder::CreateSignature(const MemberReference& member,
                                        PCOR_SIGNATURE const pSignature,
                                        const ULONG maxSignatureLength,
                                        ULONG& signatureLength)
{
    // member signature:
    //   calling convention
    //   argument count
    //   return type
    //   argument types

    // TODO: check bounds limit on pSignature[]
    signatureLength = 0;
    pSignature[signatureLength++] = member.CorCallingConvention;
    pSignature[signatureLength++] = static_cast<COR_SIGNATURE>(member.ArgumentTypes.size());

    // add return type to signature
    HRESULT hr = AddElementTypeToSignature(pSignature, maxSignatureLength, signatureLength, member.ReturnType);
    RETURN_IF_FAILED(hr);

    // add arguments types to signature
    for (const TypeReference& argumentType : member.ArgumentTypes)
    {
        hr = AddElementTypeToSignature(pSignature, maxSignatureLength, signatureLength, argumentType);
        RETURN_IF_FAILED(hr);
    }

    return S_OK;
}

HRESULT MetadataBuilder::AddElementTypeToSignature(PCOR_SIGNATURE pSignature,
                                                  const ULONG maxSignatureLength,
                                                  ULONG& signatureLength,
                                                  const TypeReference& type)
{
    // TODO: check bounds limit on pSignature[]
    pSignature[signatureLength++] = type.CorElementType;

    if (type.CorElementType == ELEMENT_TYPE_SZARRAY)
    {
        // recursive call to add the array type
        return AddElementTypeToSignature(pSignature, maxSignatureLength, signatureLength, *type.ArrayType);
    }

    if (type.CorElementType == ELEMENT_TYPE_CLASS ||
        type.CorElementType == ELEMENT_TYPE_VALUETYPE)
    {
        mdTypeRef typeRef = mdTypeRefNil;
        HRESULT hr = ResolveType(type, typeRef);
        RETURN_IF_FAILED(hr);

        COR_SIGNATURE compressedToken[8];
        const ULONG compressedTokenSize = CorSigCompressToken(typeRef, compressedToken);
        memcpy_s(&pSignature[signatureLength], maxSignatureLength - signatureLength, &compressedToken, _countof(compressedToken));
    }

    return S_OK;
}

HRESULT MetadataBuilder::FindAssemblyRefIterator(const std::wstring& assemblyName,
                                                mdAssemblyRef* rgAssemblyRefs,
                                                ULONG cAssemblyRefs,
                                                mdAssemblyRef* assemblyRef) const
{
    for (ULONG i = 0; i < cAssemblyRefs; i++)
    {
        const void* pvPublicKeyOrToken;
        ULONG cbPublicKeyOrToken;
        WCHAR wszName[512];
        ULONG cchNameReturned;
        ASSEMBLYMETADATA asmMetaData{};
        //ZeroMemory(&asmMetaData, sizeof(asmMetaData));
        const void* pbHashValue;
        ULONG cbHashValue;
        DWORD asmRefFlags;

        const HRESULT hr = assemblyImport->GetAssemblyRefProps(rgAssemblyRefs[i],
                                                               &pvPublicKeyOrToken,
                                                               &cbPublicKeyOrToken,
                                                               wszName,
                                                               _countof(wszName),
                                                               &cchNameReturned,
                                                               &asmMetaData,
                                                               &pbHashValue,
                                                               &cbHashValue,
                                                               &asmRefFlags);

        LOG_IFFAILEDRET(hr,L"GetAssemblyRefProps failed, hr = " << HEX(hr));

        if (assemblyName == wszName)
        {
            *assemblyRef = rgAssemblyRefs[i];
            return S_OK;
        }
    }

    return E_FAIL;
}