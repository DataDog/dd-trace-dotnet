#pragma once
#include "../util.h"
#include "../../../../shared/src/native-src/com_ptr.h"

namespace instrumented_assembly_generator
{
HRESULT GetTypeName(const ComPtr<IMetaDataImport>& metadataImport, const mdToken& token, shared::WSTRING& name);

class MemberSignature
{
private:
    const PCCOR_SIGNATURE _sig;
    const ULONG _sigLength;
    const ULONG _offset;

public:
    MemberSignature(const PCCOR_SIGNATURE pSig, const ULONG length, const ULONG offset) :
        _sig(pSig), _sigLength(length), _offset(offset)
    {
    }

    [[nodiscard]] ::shared::WSTRING TypeSigToString(const ComPtr<IMetaDataImport>& metadataImport) const
    {
        PCCOR_SIGNATURE typeSig = &_sig[_offset];
        return GetTypeSigName(typeSig, metadataImport);
    }

    [[nodiscard]] shared::WSTRING MethodSigToString(const ComPtr<IMetaDataImport>& metadataImport) const
    {
        PCCOR_SIGNATURE methodSig = &_sig[_offset];
        return GetMethodSigName(methodSig, metadataImport);
    }

    [[nodiscard]] shared::WSTRING LocalsToString(const ComPtr<IMetaDataImport>& metadataImport) const
    {
        shared::WSTRING name;
        PCCOR_SIGNATURE localsSig = &_sig[_offset];
        localsSig++;
        auto const numOfLocals = *localsSig;
        localsSig++;
        for (auto i = 0; i < numOfLocals; i++)
        {
            if (i > 0 && i < numOfLocals)
            {
                name += WStr(",");
            }
            name += GetTypeSigName(localsSig, metadataImport);
        }
        return name;
    }

    static shared::WSTRING GetTypeSigName(PCCOR_SIGNATURE& pSig, const ComPtr<IMetaDataImport>& metadataImport);
    static shared::WSTRING GetMethodSigName(PCCOR_SIGNATURE& pSig, const ComPtr<IMetaDataImport>& metadataImport);
    static HRESULT GetGenericsMemberFullName(mdMethodSpec methodSpecToken, mdTypeDef parentToken, LPCWSTR memberName,
                                             const ComPtr<IMetaDataImport>& metadataImport, shared::WSTRING& fullName);
    static HRESULT GetMemberFullName(mdTypeDef token, LPCWSTR memberName, const ComPtr<IMetaDataImport>& metadataImport,
                                     shared::WSTRING& fullName);
};
}

