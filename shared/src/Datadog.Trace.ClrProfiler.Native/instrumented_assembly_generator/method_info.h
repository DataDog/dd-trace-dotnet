#pragma once
#include "method_signature.h"

namespace instrumented_assembly_generator
{
// https://github.com/dotnet/roslyn/blob/ab9a5440f93609954d5fc5dc946df3519cd500d3/src/Compilers/Core/Portable/PEWriter/MetadataWriter.cs#L61
constexpr size_t path_length_limit = 260 - 1;
// https://github.com/dotnet/roslyn/blob/ab9a5440f93609954d5fc5dc946df3519cd500d3/src/Compilers/Core/Portable/PEWriter/MetadataWriter.cs#L50
constexpr size_t name_length_limit = 1024 - 1;

struct MethodInfo
{
    const mdToken token;
    const shared::WSTRING name;
    const mdToken typeToken;
    const shared::WSTRING typeName;
    MethodSignature methodSig;

    MethodInfo() : token(0), typeToken(0)
    {
    }

    MethodInfo(const mdToken token, shared::WSTRING name, const mdToken typeToken, shared::WSTRING typeName,
               MethodSignature methodSig) :
        token(token),
        name(std::move(name)),
        typeToken(typeToken),
        typeName(std::move(typeName)),
        methodSig(std::move(methodSig))
    {
    }

    [[nodiscard]] bool IsValid() const
    {
        return token > 0;
    }

    static MethodInfo GetMethodInfo(const ComPtr<IMetaDataImport>& metadataImport, mdToken methodToken,
                                    shared::WSTRING functionName, mdToken parentToken, PCCOR_SIGNATURE methodSignature,
                                    ULONG methodSignatureLen)
    {
        shared::WSTRING name;
        auto const hr = GetTypeName(metadataImport, parentToken, name);
        if (FAILED(hr))
        {
            return {};
        }
        return {methodToken, std::move(functionName), parentToken, name,
                MethodSignature(methodSignature, methodSignatureLen)};
    }

    static MethodInfo GetMethodInfo(const ComPtr<IMetaDataImport>& metadataImport, const mdToken& methodToken)
    {
        auto parentToken = mdTokenNil;
        WCHAR functionName[name_length_limit]{};
        DWORD function_name_len = 0;
        PCCOR_SIGNATURE methodSignature{nullptr};
        ULONG methodSignatureLen = 0;
        DWORD pdwCPlusTypeFlag;
        HRESULT hr = E_FAIL;
        ComPtr<IMetaDataImport2> metadataImport2;
        switch (TypeFromToken(methodToken))
        {
            case mdtMethodDef:
                // Will call GetMethodProps
                hr = metadataImport->GetMemberProps(methodToken, &parentToken, functionName, name_length_limit,
                                                    &function_name_len, nullptr, &methodSignature, &methodSignatureLen,
                                                    nullptr, nullptr, &pdwCPlusTypeFlag, nullptr, nullptr);
                break;
            case mdtMemberRef:
                hr = metadataImport->GetMemberRefProps(methodToken, &parentToken, functionName, name_length_limit,
                                                       &function_name_len, &methodSignature, &methodSignatureLen);
                break;
            case mdtMethodSpec:
                metadataImport2 = metadataImport.As<IMetaDataImport2>(IID_IMetaDataImport2);
                if (metadataImport2.Get() == nullptr) break;

                hr = metadataImport2->GetMethodSpecProps(methodToken, &parentToken, &methodSignature,
                                                         &methodSignatureLen);

                if (FAILED(hr)) break;
                return GetMethodInfo(metadataImport, parentToken);

            default:
                hr = E_FAIL;
                break;
        }

        if (FAILED(hr))
        {
            return {};
        }
        return GetMethodInfo(metadataImport, methodToken, functionName, parentToken, methodSignature,
                             methodSignatureLen);
    }
};
} // namespace instrumented_assembly_generator
