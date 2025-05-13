#pragma once
#include "../../../../shared/src/native-src/pal.h"
#include "signature_types.h"
using namespace shared;

namespace iast
{

    class ModuleInfo;

    enum class SignatureTypes
    {
        Unknown,
        Method,
        Field,
        TypeSpec,
        LocalsSignature,
        Property
    };

    class SignatureInfo : public ISignatureBuilder
    {
        friend class ModuleInfo;
    public:
        SignatureInfo(ModuleInfo* moduleInfo, PCCOR_SIGNATURE pSig, DWORD nSig);
        virtual ~SignatureInfo();

        WSTRING& GetReturnTypeString();
        WSTRING& GetParamsRepresentation();
    protected:
        ModuleInfo* _module = nullptr;

        std::vector<BYTE> _dynamicSig;
        PCCOR_SIGNATURE _pSig = nullptr;
        DWORD _nSig = 0;
        SignatureTypes _signatureType;

        HRESULT FromSignature(DWORD cbBuffer, const BYTE* pCorSignature, SignatureType** ppType, DWORD* pdwSigSize);
        HRESULT FromToken(CorElementType type, mdToken token, SignatureType** ppType);
        HRESULT IsValueType(mdTypeDef mdTypeDefToken, BOOL* pIsValueType);
        HRESULT ParseTypeSequence(const BYTE* pBuffer, ULONG cbBuffer, ULONG cTypes, std::vector<SignatureType*>* ppEnumTypes, ULONG* pcbRead);

        HRESULT ParseMethodSignature(const BYTE* pSignature, DWORD cbSignature, ULONG* pCallingConvention, SignatureType** ppReturnType, std::vector<SignatureType*>* ppEnumParameterTypes, ULONG* pcGenericTypeParameters, ULONG* pcbRead);
        HRESULT ParseFieldSignature(const BYTE* pSignature, DWORD cbSignature, ULONG* pcbRead);
        HRESULT ParseGenericInstSignature(const BYTE* pSignature, DWORD cbSignature, ULONG* pcbRead);
        HRESULT ParseLocalsSignature(const BYTE* pSignature, DWORD cbSignature, ULONG* pcbRead);
        HRESULT ParsePropertySignature(const BYTE* pSignature, DWORD cbSignature, ULONG* pcbRead);

    public:
        CorCallingConvention _callingConvention = IMAGE_CEE_CS_CALLCONV_DEFAULT;
        SignatureType* _returnType = nullptr;
        ULONG _genericParamCount = 0;
        std::vector<SignatureType*> _params;

        WSTRING _returnTypeString;
        WSTRING _paramsString;

        SignatureTypes GetType();
        PCCOR_SIGNATURE GetSignature(DWORD* sigSize = nullptr);
        bool HasThis();
        int GetEffectiveParamCount();
        WSTRING CharacterizeMember(WSTRING memberName);

    public:
        // Inherited via ISignatureBuilder
        HRESULT AddElementType(CorElementType corType) override;
        HRESULT AddToken(mdToken token) override;
        HRESULT AddData(const BYTE* data, ULONG size) override;
        HRESULT Add(DWORD data) override;
    };
}