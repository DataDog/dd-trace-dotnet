#include <fstream>
#include <utility>
#include "ModuleMetadata.h"
#include "Macros.h"
#include "IntegrationBase.h"

ModuleMetadata::ModuleMetadata(std::wstring assembly_name,
                               std::vector<const IntegrationBase*> integrations,
                               const mdModule module,
                               IMetaDataImport* const metadata_import,
                               IMetaDataEmit* const metadata_emit,
                               IMetaDataAssemblyImport* const assembly_import,
                               IMetaDataAssemblyEmit* const assembly_emit) :
    module(module),
    metadataImport(metadata_import),
    metadataEmit(metadata_emit),
    assemblyImport(assembly_import),
    assemblyEmit(assembly_emit),
    assemblyName(std::move(assembly_name)),
    m_Integrations(std::move(integrations))
{
    metadataImport->AddRef();
    metadataEmit->AddRef();
    assemblyImport->AddRef();
    assemblyEmit->AddRef();
}

bool ModuleMetadata::TryGetRef(const TypeReference& keyIn, mdTypeRef& valueOut) const
{
    const auto search = m_TypeMap.find(keyIn);

    if (search != m_TypeMap.end())
    {
        valueOut = search->second;
        return true;
    }

    return false;
}

bool ModuleMetadata::TryGetRef(const MemberReference& keyIn, mdMemberRef& valueOut) const
{
    const auto search = m_MemberMap.find(keyIn);

    if (search != m_MemberMap.end())
    {
        valueOut = search->second;
        return true;
    }

    return false;
}

void ModuleMetadata::SetRef(const TypeReference& keyIn, const mdTypeRef& valueIn)
{
    m_TypeMap[keyIn] = valueIn;
}

void ModuleMetadata::SetRef(const MemberReference& keyIn, const mdMemberRef& valueIn)
{
    m_MemberMap[keyIn] = valueIn;
}

HRESULT ModuleMetadata::ResolveType(const TypeReference& type)
{
    mdTypeRef typeRef = mdTypeRefNil;
    return ResolveType(type, typeRef);
}

HRESULT ModuleMetadata::ResolveType(const TypeReference& type, mdTypeRef& typeRefOut)
{
    HRESULT hr;

    if (TryGetRef(type, typeRefOut))
    {
        // this type was already resolved
        return S_OK;
    }

    if (assemblyName == type.AssemblyName)
    {
        // type is defined in this assembly
        mdTypeRef typeRef = mdTypeRefNil;
        hr = metadataEmit->DefineTypeRefByName(module, type.TypeName.c_str(), &typeRef);

        if (SUCCEEDED(hr))
        {
            SetRef(type, typeRef);
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
                SetRef(type, typeRef);
                typeRefOut = typeRef;
            }
        }
    }

    return S_OK;
}

HRESULT ModuleMetadata::ResolveMember(const MemberReference& member)
{
    mdMemberRef memberRef = mdMemberRefNil;
    return ResolveMember(member, memberRef);
}

HRESULT ModuleMetadata::ResolveMember(const MemberReference& member, mdMemberRef& memberRefOut)
{
    if (TryGetRef(member, memberRefOut))
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
        SetRef(member, memberRef);
        memberRefOut = memberRef;
    }

    return S_OK;
}

HRESULT ModuleMetadata::EmitAssemblyRef(const std::wstring& assemblyName,
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

HRESULT ModuleMetadata::FindAssemblyRef(const std::wstring& assemblyName,
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

HRESULT ModuleMetadata::CreateSignature(const MemberReference& member,
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

HRESULT ModuleMetadata::AddElementTypeToSignature(PCOR_SIGNATURE pSignature,
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

HRESULT ModuleMetadata::FindAssemblyRefIterator(const std::wstring& assemblyName,
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

void ModuleMetadata::GetClassAndFunctionNamesFromMethodDef(mdMethodDef methodDef,
                                                           LPWSTR wszTypeDefName,
                                                           ULONG cchTypeDefName,
                                                           LPWSTR wszMethodDefName,
                                                           ULONG cchMethodDefName) const
{
    mdTypeDef typeDef;
    ULONG cchMethodDefActual;
    DWORD dwMethodAttr;
    ULONG cchTypeDefActual;
    DWORD dwTypeDefFlags;
    mdTypeDef typeDefBase;

    HRESULT hr = metadataImport->GetMethodProps(methodDef,
                                                &typeDef,
                                                wszMethodDefName,
                                                cchMethodDefName,
                                                &cchMethodDefActual,
                                                &dwMethodAttr,
                                                // [OUT] point to the blob value of meta data
                                                nullptr,
                                                // [OUT] actual size of signature blob
                                                nullptr,
                                                // [OUT] codeRVA
                                                nullptr,
                                                // [OUT] Impl. Flags
                                                nullptr);

    if (FAILED(hr))
    {
        LOG_APPEND(L"GetMethodProps failed for methodDef = " << HEX(methodDef) << L", hr = " << HEX(hr));
    }

    hr = metadataImport->GetTypeDefProps(typeDef,
                                         wszTypeDefName,
                                         cchTypeDefName,
                                         &cchTypeDefActual,
                                         &dwTypeDefFlags,
                                         &typeDefBase);

    if (FAILED(hr))
    {
        LOG_APPEND(L"GetTypeDefProps failed for typeDef = " << HEX(typeDef) << L", hr = " << HEX(hr));
    }
}
