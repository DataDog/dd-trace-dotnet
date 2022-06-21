#include "member_signature.h"
#include "instrumented_assembly_generator_helper.h"

namespace instrumented_assembly_generator
{
HRESULT GetTypeRefName(const ComPtr<IMetaDataImport>& metadataImport, const mdToken& token, shared::WSTRING& name)
{
    HRESULT hr;
    WCHAR szTypeName[name_length_limit]{};
    mdModule scope;
    ULONG cchTypeDefActualSize;
    IfFailRet(metadataImport->GetModuleFromScope(&scope));
    IfFailRet(metadataImport->GetTypeRefProps(
        token, &scope, szTypeName, instrumented_assembly_generator::name_length_limit, &cchTypeDefActualSize));
    name = shared::WSTRING(szTypeName);
    return hr;
}

HRESULT GetTypeSpecName(const ComPtr<IMetaDataImport>& metadataImport, const mdToken& token, shared::WSTRING& name)
{
    HRESULT hr;
    PCCOR_SIGNATURE signature;
    ULONG signature_length;
    IfFailRet(metadataImport->GetTypeSpecFromToken(token, &signature, &signature_length));
    const auto typeSpecSig = MemberSignature(signature, signature_length, 0);
    name = typeSpecSig.TypeSigToString(metadataImport);
    return hr;
}

HRESULT GetMethodSpecName(const ComPtr<IMetaDataImport2>& metadataImport2, const mdToken& token, shared::WSTRING& name)
{
    HRESULT hr;
    PCCOR_SIGNATURE signature;
    ULONG signature_length;
    mdToken parent;
    IfFailRet(metadataImport2->GetMethodSpecProps(token, &parent, &signature, &signature_length));
    const auto methodSpecSig = MemberSignature(signature, signature_length, 0);
    name = methodSpecSig.MethodSigToString(metadataImport2);
    return hr;
}

shared::WSTRING GetMethodSpecSigName(PCCOR_SIGNATURE& pSig, const ComPtr<IMetaDataImport>& metadataImport)
{
    shared::WSTRING name;
    auto const numOfParams = *pSig;
    pSig++;
    for (auto i = 0; i < numOfParams; i++)
    {
        if (i > 0)
        {
            name += WStr(",");
        }
        name += MemberSignature::GetTypeSigName(pSig, metadataImport);
    }
    return name;
}

HRESULT GetTypeName(const ComPtr<IMetaDataImport>& metadataImport, const mdToken& token, shared::WSTRING& name)
{
    HRESULT hr;
    WCHAR szTypeName[name_length_limit]{};
    ULONG cchTypeDefActualSize;
    switch (TypeFromToken(token))
    {
        case mdtTypeDef:
        {
            IfFailRet(metadataImport->GetTypeDefProps(token, szTypeName, name_length_limit, &cchTypeDefActualSize,
                                                      nullptr, nullptr));
            name = shared::WSTRING(szTypeName);
            return hr;
        }
        case mdtTypeRef:
        {
            return GetTypeRefName(metadataImport, token, name);
        }
        case mdtTypeSpec:
        {
            return GetTypeSpecName(metadataImport, token, name);
        }
        case mdtMemberRef:
        {
            mdToken ptk;
            IfFailRet(metadataImport->GetMemberRefProps(token, &ptk, szTypeName, name_length_limit,
                                                        &cchTypeDefActualSize, nullptr, nullptr));
            return GetTypeRefName(metadataImport, ptk, name);
        }
        case mdtMethodSpec:
        {
            return GetMethodSpecName(metadataImport.As<IMetaDataImport2>(IID_IMetaDataImport2), token, name);
        }
        default:
            return E_FAIL;
    }
}

shared::WSTRING MemberSignature::GetTypeSigName(PCCOR_SIGNATURE& pSig, const ComPtr<IMetaDataImport>& metadataImport)
{
    shared::WSTRING tokenName;
    if (*pSig == ELEMENT_TYPE_BYREF)
    {
        tokenName += IntToHex(static_cast<int>(*pSig)) + WStr("?");
        pSig++;
    }
    mdToken token;
    shared::WSTRING typeName;
    tokenName += IntToHex(static_cast<int>(*pSig));
    switch (*pSig)
    {
        case ELEMENT_TYPE_VOID:
        case ELEMENT_TYPE_BOOLEAN:
        case ELEMENT_TYPE_CHAR:
        case ELEMENT_TYPE_I1:
        case ELEMENT_TYPE_U1:
        case ELEMENT_TYPE_U2:
        case ELEMENT_TYPE_I2:
        case ELEMENT_TYPE_I4:
        case ELEMENT_TYPE_U4:
        case ELEMENT_TYPE_I8:
        case ELEMENT_TYPE_U8:
        case ELEMENT_TYPE_R4:
        case ELEMENT_TYPE_R8:
        case ELEMENT_TYPE_I:
        case ELEMENT_TYPE_U:
        case ELEMENT_TYPE_STRING:
        case ELEMENT_TYPE_OBJECT:
            pSig++;
            break;
        case ELEMENT_TYPE_CLASS:
            pSig++;
            pSig += CorSigUncompressToken(pSig, &token);
            GetTypeName(metadataImport, token, typeName);

            tokenName += WStr("?") + IntToHex(token) + WStr("?") + typeName;
            break;
        case ELEMENT_TYPE_VALUETYPE:
        {
            pSig++;
            pSig += CorSigUncompressToken(pSig, &token);
            GetTypeName(metadataImport, token, typeName);
            tokenName += WStr("?") + IntToHex(token) + WStr("?") + typeName;
            break;
        }
        case ELEMENT_TYPE_SZARRAY:
        {
            pSig++;
            tokenName += WStr("?") + GetTypeSigName(pSig, metadataImport);
            break;
        }
        case ELEMENT_TYPE_GENERICINST:
        {
            pSig++;
            tokenName += WStr("?") + GetTypeSigName(pSig, metadataImport);
            tokenName += WStr("<");
            ULONG num = 0;
            pSig += CorSigUncompressData(pSig, &num);
            for (ULONG i = 0; i < num; i++)
            {
                tokenName += GetTypeSigName(pSig, metadataImport);
                if (i != num - 1)
                {
                    tokenName += WStr(",");
                }
            }
            tokenName += WStr(">");
            break;
        }
        case ELEMENT_TYPE_MVAR:
        {
            pSig++;
            ULONG num = 0;
            pSig += CorSigUncompressData(pSig, &num);
            tokenName += WStr("?") + shared::ToWSTRING(std::to_string(num));
            break;
        }
        case ELEMENT_TYPE_VAR:
        {
            pSig++;
            ULONG num = 0;
            pSig += CorSigUncompressData(pSig, &num);
            tokenName += WStr("?") + shared::ToWSTRING(std::to_string(num));
            break;
        }
        default:
            break;
    }

    return tokenName;
}

shared::WSTRING MemberSignature::GetMethodSigName(PCCOR_SIGNATURE& pSig, const ComPtr<IMetaDataImport>& metadataImport)
{
    shared::WSTRING tokenName;
    if (*pSig == ELEMENT_TYPE_BYREF)
    {
        pSig++;
    }

    if (*pSig == IMAGE_CEE_CS_CALLCONV_GENERICINST)
    {
        pSig++;
        return GetMethodSpecSigName(pSig, metadataImport);
    }
    return WStr("Not Implemented");
}

HRESULT MemberSignature::GetGenericsMemberFullName(mdMethodSpec methodSpecToken, mdTypeDef parentToken,
                                                   LPCWSTR memberName, const ComPtr<IMetaDataImport>& metadataImport,
                                                   shared::WSTRING& fullName)
{
    HRESULT hr;
    shared::WSTRING genericTypeArguments;
    IfFailRet(GetMemberFullName(parentToken, memberName, metadataImport, fullName));
    IfFailRet(GetTypeName(metadataImport, methodSpecToken, genericTypeArguments));
    fullName = fullName + WStr("<") + genericTypeArguments + WStr(">");
    return hr;
}

HRESULT MemberSignature::GetMemberFullName(mdTypeDef token, LPCWSTR memberName,
                                           const ComPtr<IMetaDataImport>& metadataImport, shared::WSTRING& fullName)
{
    shared::WSTRING typeName;
    HRESULT hr;
    IfFailRet(GetTypeName(metadataImport, token, typeName));

    const auto methodName = shared::WSTRING(memberName);
    fullName = typeName + WStr(".") + methodName;
    return hr;
}
} // namespace instrumented_assembly_generator
