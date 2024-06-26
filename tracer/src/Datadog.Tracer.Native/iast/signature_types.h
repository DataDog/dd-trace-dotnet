#pragma once
#include "../../../../shared/src/native-src/pal.h"
using namespace shared;

namespace iast
{
    class ModuleInfo;

    const WSTRING GetNameFromCorType(CorElementType corType);
    CorElementType GetCorTypeFromName(WSTRING name);
    bool IsSimpleType(CorElementType corType);

    class ISignatureBuilder
    {
    public:
        virtual HRESULT AddElementType(CorElementType corType) = 0;
        virtual HRESULT AddToken(mdToken token) = 0;
        virtual HRESULT AddData(const BYTE* data, ULONG size) = 0;
        virtual HRESULT Add(DWORD data) = 0;
    };

    class SignatureType
    {
        friend class SignatureInfo;

    protected:
        SignatureType(CorElementType type);
        virtual ~SignatureType();
    protected:
        CorElementType _type;
        bool _isSentinel;
        bool _isPinned;
        std::vector<SignatureType*> _modifiers;
    public:
        // IType
        virtual CorElementType GetCorElementType();
        virtual HRESULT AddToSignature(ISignatureBuilder* pSignatureBuilder);
        virtual bool IsPrimitive();
        virtual bool IsArray();
        virtual bool IsClass();
        virtual bool IsValueType();
        virtual bool IsByRef();
        virtual WSTRING GetName();
        virtual mdToken GetToken();
    public:
        //TODO Scoping these at Type is probably too broad. We should consider moving modifiers/pinned/sentienl to IMethodParameter instead
        //of the type itself. There is also a difference between things like [Mod]PTR[Type] and PTR[Mod][Type]. Both can exist, apparantly due to
        //a c++ compiler bug.
        HRESULT SetIsPinned(bool isPinned);
        HRESULT SetIsSentinel(bool isSentinel);
        HRESULT SetModifers(const std::vector<SignatureType*>& modifiers);
    };

    class SignatureSimpleType : public SignatureType
    {
    public:
        friend class SignatureInfo;
    protected:
        SignatureSimpleType(CorElementType corType);
        ~SignatureSimpleType() override;
    };

    class SignatureTokenType : public SignatureType //, public ITokenType
    {
        friend class SignatureInfo;
    protected:
        SignatureTokenType(ModuleInfo* module, mdToken token, CorElementType type);
        ~SignatureTokenType() override;
    private:
        mdToken _token;
        ModuleInfo* _pOwningModule;
        WSTRING _name;

    public:
        // ITokenType methods.
        virtual HRESULT GetToken(mdToken* token);
        virtual HRESULT GetOwningModule(ModuleInfo** ppOwningModule);

    public:
        WSTRING GetName() override;
        HRESULT AddToSignature(ISignatureBuilder* pSignatureBuilder) override;
        mdToken GetToken() override;
    };

    class SignatureCompositeType : public SignatureType//, public ICompositeType
    {
        friend class SignatureInfo;
    protected:
        SignatureCompositeType(CorElementType type, SignatureType* relatedType);
        ~SignatureCompositeType() override;
    private:
        SignatureType* _relatedType;
    public:
        // ICompositeType
        virtual HRESULT GetRelatedType(SignatureType** type);
    public:
        // IType
        HRESULT AddToSignature(ISignatureBuilder* pSignatureBuilder) override;
        WSTRING GetName() override;
    };

    class SignatureFunctionType : public SignatureType
    {
        friend class SignatureInfo;
    protected:
        SignatureFunctionType(CorCallingConvention callingConvention, SignatureType* pReturnType, const std::vector<SignatureType*>& parameters, DWORD dwGenericParameterCount);
        ~SignatureFunctionType() override;
    private:
        CorCallingConvention _callingConvention;
        SignatureType* _pReturnType;
        std::vector<SignatureType*> _parameters;
        DWORD _genericParameterCount;
        // IType
    public:
        HRESULT AddToSignature(ISignatureBuilder* pSignatureBuilder) override;
    };

    class SignatureArrayType : public SignatureCompositeType
    {
        friend class SignatureInfo;
    protected:
        SignatureArrayType(SignatureType* relatedType, ULONG rank, const std::vector<ULONG>& counts, const std::vector<ULONG>& bounds);
        ~SignatureArrayType() override;
    private:
        ULONG _rank;
        std::vector<ULONG> _counts;
        std::vector<ULONG> _bounds;
    public:
        // IType
        HRESULT AddToSignature(ISignatureBuilder* pSignatureBuilder) override;

    };

    class SignatureGenericParameterType : public SignatureType//, public IGenericParameterType
    {
        friend class SignatureInfo;
    protected:
        SignatureGenericParameterType(CorElementType type, ULONG position);
        ~SignatureGenericParameterType() override;
    private:
        ULONG _position;
    public:
        // IGenericParameterType
        virtual HRESULT GetPosition(ULONG* pPosition);
    public:
        // IType
        HRESULT AddToSignature(ISignatureBuilder* pSignatureBuilder) override;
        WSTRING GetName() override;
    };

    class SignatureGenericInstance : public SignatureCompositeType
    {
        friend class SignatureInfo;
    protected:
        SignatureGenericInstance(SignatureType* typeDefinition, const std::vector<SignatureType*>& genericParameters);
        ~SignatureGenericInstance() override;
    private:
        std::vector<SignatureType*> _genericParameters;
    public:
        // IType
        HRESULT AddToSignature(ISignatureBuilder* pSignatureBuilder) override;
        WSTRING GetName() override;
    };

    class SignatureModifierType : public SignatureType
    {
        friend class SignatureInfo;
    protected:
        SignatureModifierType(CorElementType type, mdToken token);
        ~SignatureModifierType() override;
    private:
        mdToken _token;
    public:
        // IType
        HRESULT AddToSignature(ISignatureBuilder* pSignatureBuilder) override;
    };
}