#include "iast_util.h"
#include "signature_types.h"
#include "module_info.h"

namespace iast
{

    std::map<CorElementType, WSTRING> corElementNames;
    std::map<WSTRING, CorElementType> corElementTypes;
    std::set<CorElementType> simpleTypes;
#define DefCorType(x,y) corElementNames[x] = y; corElementTypes[y] = x;
#define DefCorTypeSimple(x,y) corElementNames[x] = y; corElementTypes[y] = x; simpleTypes.insert(x);
    bool InitCorTypes()
    {
        DefCorType(ELEMENT_TYPE_END, WStr("END"));
        DefCorTypeSimple(ELEMENT_TYPE_VOID, WStr("void"));
        DefCorTypeSimple(ELEMENT_TYPE_BOOLEAN, WStr("System.Boolean"));
        DefCorTypeSimple(ELEMENT_TYPE_CHAR, WStr("System.Char"));
        DefCorTypeSimple(ELEMENT_TYPE_I1, WStr("System.SByte"));
        DefCorTypeSimple(ELEMENT_TYPE_U1, WStr("System.Byte"));
        DefCorTypeSimple(ELEMENT_TYPE_I2, WStr("System.Int16"));
        DefCorTypeSimple(ELEMENT_TYPE_U2, WStr("System.UInt16"));
        DefCorTypeSimple(ELEMENT_TYPE_I4, WStr("System.Int32"));
        DefCorTypeSimple(ELEMENT_TYPE_U4, WStr("System.UInt32"));
        DefCorTypeSimple(ELEMENT_TYPE_I8, WStr("System.Int64"));
        DefCorTypeSimple(ELEMENT_TYPE_U8, WStr("System.UInt64"));
        DefCorTypeSimple(ELEMENT_TYPE_R4, WStr("System.Single"));
        DefCorTypeSimple(ELEMENT_TYPE_R8, WStr("System.Double"));
        DefCorTypeSimple(ELEMENT_TYPE_I, WStr("System.IntPtr"));
        DefCorTypeSimple(ELEMENT_TYPE_U, WStr("System.UIntPtr"));
        DefCorTypeSimple(ELEMENT_TYPE_STRING, WStr("System.String"));
        DefCorTypeSimple(ELEMENT_TYPE_OBJECT, WStr("System.Object"));
        DefCorTypeSimple(ELEMENT_TYPE_TYPEDBYREF, WStr("TYPEDBYREF")); //Don't know what this is
        DefCorType(ELEMENT_TYPE_PTR, WStr("PTR"));
        DefCorType(ELEMENT_TYPE_BYREF, WStr("BYREF"));
        DefCorType(ELEMENT_TYPE_VALUETYPE, WStr("VALUETYPE"));
        DefCorType(ELEMENT_TYPE_CLASS, WStr("CLASS"));
        DefCorType(ELEMENT_TYPE_VAR, WStr("VAR"));
        DefCorType(ELEMENT_TYPE_ARRAY, WStr("ARRAY"));
        DefCorType(ELEMENT_TYPE_GENERICINST, WStr("GENERICINST"));
        DefCorType(ELEMENT_TYPE_FNPTR, WStr("FNPTR"));
        DefCorType(ELEMENT_TYPE_SZARRAY, WStr("SZARRAY"));
        DefCorType(ELEMENT_TYPE_MVAR, WStr("MVAR"));
        DefCorType(ELEMENT_TYPE_CMOD_REQD, WStr("CMOD_REQD"));
        DefCorType(ELEMENT_TYPE_CMOD_OPT, WStr("CMOD_OPT"));
        DefCorType(ELEMENT_TYPE_INTERNAL, WStr("INTERNA"));
        DefCorType(ELEMENT_TYPE_MAX, WStr("MAX"));
        DefCorType(ELEMENT_TYPE_MODIFIER, WStr("MODIFIER"));
        DefCorType(ELEMENT_TYPE_SENTINEL, WStr("SENTINE"));
        DefCorType(ELEMENT_TYPE_PINNED, WStr("PINNED"));
        return true;
    }
    static bool corTypesInitizalized = InitCorTypes();

    //Temporary helper
    const WSTRING GetNameFromCorType(CorElementType corType)
    {
        if (corTypesInitizalized)
        {
            auto it = corElementNames.find(corType);
            if (it != corElementNames.end())
            {
                return it->second;
            }
        }
        return WStr("INVALID");
        //switch (corType)
        //{
        //case ELEMENT_TYPE_END:
        //    return L"END";
        //case ELEMENT_TYPE_VOID:
        //    return L"void";
        //case ELEMENT_TYPE_BOOLEAN:
        //    return L"System.Boolean";
        //case ELEMENT_TYPE_CHAR:
        //    return L"System.Char";
        //case ELEMENT_TYPE_I1:
        //    return L"System.Byte";
        //case ELEMENT_TYPE_U1:
        //    return L"System.Byte";
        //case ELEMENT_TYPE_I2:
        //    return L"System.Int16";
        //case ELEMENT_TYPE_U2:
        //    return L"System.UInt16";
        //case ELEMENT_TYPE_I4:
        //    return L"System.Int32";
        //case ELEMENT_TYPE_U4:
        //    return L"System.UInt32";
        //case ELEMENT_TYPE_I8:
        //    return L"System.Int64";
        //case ELEMENT_TYPE_U8:
        //    return L"System.UInt64";
        //case ELEMENT_TYPE_R4:
        //    return L"System.Float";
        //case ELEMENT_TYPE_R8:
        //    return L"System.Double";
        //case ELEMENT_TYPE_STRING:
        //    return L"System.String";
        //case ELEMENT_TYPE_PTR:
        //    return L"PTR";
        //case ELEMENT_TYPE_BYREF:
        //    return L"BYREF";
        //case ELEMENT_TYPE_VALUETYPE:
        //    return L"VALUETYPE";
        //case ELEMENT_TYPE_CLASS:
        //    return L"CLASS";
        //case ELEMENT_TYPE_VAR:
        //    return L"VAR";
        //case ELEMENT_TYPE_ARRAY:
        //    return L"ARRAY";
        //case ELEMENT_TYPE_GENERICINST:
        //    return L"GENERICINST";
        //case ELEMENT_TYPE_TYPEDBYREF:
        //    return L"TYPEDBYREF";
        //case ELEMENT_TYPE_I:
        //    return L"System.IntPtr";
        //case ELEMENT_TYPE_U:
        //    return L"System.UIntPtr";
        //case ELEMENT_TYPE_FNPTR:
        //    return L"FNPTR";
        //case ELEMENT_TYPE_OBJECT:
        //    return L"System.Object";
        //case ELEMENT_TYPE_SZARRAY:
        //    return L"SZARRAY";
        //case ELEMENT_TYPE_MVAR:
        //    return L"MVAR";
        //case ELEMENT_TYPE_CMOD_REQD:
        //    return L"CMOD_REQD";
        //case ELEMENT_TYPE_CMOD_OPT:
        //    return L"CMOD_OPT";
        //case ELEMENT_TYPE_INTERNAL:
        //    return L"INTERNAL";
        //case ELEMENT_TYPE_MAX:
        //    return L"MAX";
        //case ELEMENT_TYPE_MODIFIER:
        //    return L"MODIFIER";
        //case ELEMENT_TYPE_SENTINEL:
        //    return L"SENTINEL";
        //case ELEMENT_TYPE_PINNED:
        //    return L"PINNED";
        //default:
        //    return L"INVALID";
        //}
    }
    CorElementType GetCorTypeFromName(WSTRING name)
    {
        if (corTypesInitizalized)
        {
            auto it = corElementTypes.find(name);
            if (it != corElementTypes.end())
            {
                return it->second;
            }
        }
        return (CorElementType)0;
    }
    bool IsSimpleType(CorElementType corType)
    {
        if (corTypesInitizalized)
        {
            auto it = simpleTypes.find(corType);
            return it != simpleTypes.end();
        }
        return false;
    }

    SignatureType::SignatureType(CorElementType type) :
        _type(type),
        _isPinned(false),
        _isSentinel(false),
        _modifiers()
    {
    }
    SignatureType::~SignatureType()
    {
    }

    CorElementType SignatureType::GetCorElementType()
    {
        return _type;
    }
    HRESULT SignatureType::AddToSignature(ISignatureBuilder* pSigBuilder)
    {
        IfNullRet(pSigBuilder);
        HRESULT hr = S_OK;

        if (_isSentinel)
        {
            IfFailRet(pSigBuilder->AddElementType(ELEMENT_TYPE_SENTINEL));
        }
        if (_isPinned)
        {
            IfFailRet(pSigBuilder->AddElementType(ELEMENT_TYPE_PINNED));
        }

        for (SignatureType* modifier : _modifiers)
        {
            IfFailRet(modifier->AddToSignature(pSigBuilder));
        }

        IfFailRet(pSigBuilder->AddElementType(_type));

        return hr;
    }
    bool SignatureType::IsPrimitive()
    {
        return (_type <= ELEMENT_TYPE_STRING) || (_type == ELEMENT_TYPE_I) || (_type == ELEMENT_TYPE_U);
    }
    bool SignatureType::IsArray()
    {
        return (_type == ELEMENT_TYPE_ARRAY) || (_type == ELEMENT_TYPE_SZARRAY);
    }
    bool SignatureType::IsClass()
    {
        return (_type == ELEMENT_TYPE_CLASS);
    }
    bool SignatureType::IsValueType()
    {
        return (_type == ELEMENT_TYPE_VALUETYPE);
    }
    bool SignatureType::IsByRef()
    {
        return (_type == ELEMENT_TYPE_BYREF);
    }


    WSTRING SignatureType::GetName()
    {
        return GetNameFromCorType(_type);
    }
    mdToken SignatureType::GetToken()
    {
        return 0;
    }

    HRESULT SignatureType::SetIsPinned(_In_ bool isPinned)
    {
        _isPinned = isPinned;
        return S_OK;
    }
    HRESULT SignatureType::SetIsSentinel(_In_ bool isSentinel)
    {
        _isSentinel = isSentinel;
        return S_OK;
    }
    HRESULT SignatureType::SetModifers(_In_ const std::vector<SignatureType*>& modifiers)
    {
        _modifiers = modifiers;
        return S_OK;
    }

    SignatureSimpleType::SignatureSimpleType(CorElementType corType) : SignatureType(corType)
    {
    }
    SignatureSimpleType::~SignatureSimpleType()
    {
    }

    SignatureTokenType::SignatureTokenType(ModuleInfo* module, mdToken token, CorElementType type) : SignatureType(type), _token(token), _pOwningModule(module)
    {
    }
    SignatureTokenType::~SignatureTokenType()
    {
    }
    HRESULT SignatureTokenType::GetToken(mdToken* token)
    {
        IfNullRet(token);
        *token = _token;
        return S_OK;
    }
    HRESULT SignatureTokenType::GetOwningModule(ModuleInfo** ppOwningModule)
    {
        IfNullRet(ppOwningModule);
        *ppOwningModule = _pOwningModule;
        return S_OK;
    }
    HRESULT SignatureTokenType::AddToSignature(ISignatureBuilder* pSignatureBuilder)
    {
        IfNullRet(pSignatureBuilder);

        HRESULT hr = S_OK;
        IfFailRet(SignatureType::AddToSignature(pSignatureBuilder));
        IfFailRet(pSignatureBuilder->AddToken(_token));
        return hr;
    }
    WSTRING SignatureTokenType::GetName()
    {
        HRESULT hr = S_OK;
        if (_name.length() == 0)
        {
            ULONG cchLength = 0;
            IMetaDataImport* pImport = _pOwningModule->GetMetaDataImport();
            if (TypeFromToken(_token) == mdtTypeDef)
            {
                mdTypeDef tkEnclosing = mdTokenNil;
                mdTypeDef tkCurrent = _token;
                WSTRING fullName;
                std::vector<WCHAR> nameBuffer(100);
                while (!IsNilToken(tkCurrent) && (pImport->GetNestedClassProps(tkCurrent, &tkEnclosing) == S_OK) && (tkEnclosing != mdTokenNil))
                {
                    if (FAILED(pImport->GetTypeDefProps(tkEnclosing, nullptr, 0, &cchLength, nullptr, nullptr)))
                    {
                        return EmptyWStr;
                    }
                    if (nameBuffer.size() < cchLength)
                    {
                        nameBuffer.resize(cchLength);
                    }

                    // Add nested class names separated by a "+".
                    if (FAILED(pImport->GetTypeDefProps(tkEnclosing, nameBuffer.data(), cchLength, &cchLength, nullptr, nullptr)))
                    {
                        return EmptyWStr;
                    }

                    fullName.insert(0, WStr("+"));
                    fullName.insert(0, nameBuffer.data());

                    tkCurrent = tkEnclosing;
                }

                if(FAILED(pImport->GetTypeDefProps(_token, nullptr, 0, &cchLength, nullptr, nullptr)))
                {
                    return EmptyWStr;
                }
                if (nameBuffer.size() < cchLength)
                {
                    nameBuffer.resize(cchLength);
                }

                if(FAILED(pImport->GetTypeDefProps(_token, nameBuffer.data(), cchLength, &cchLength, nullptr, nullptr)))
                {
                    return EmptyWStr;
                }
                fullName += nameBuffer.data();

                _name = fullName;
            }
            else if (TypeFromToken(_token) == mdtTypeRef)
            {
                // Note: there are no public apis to get the outer classes for a nested
                // type ref. We don't want to attempt to resolve the type ref though, because
                // that could cause a module load at a time when such an operation is not
                // allowed.
                WCHAR nameBuffer[1024];
                if (FAILED(pImport->GetTypeRefProps(_token, nullptr, nameBuffer, 1024, &cchLength)))
                {
                    return EmptyWStr;
                }
                _name = nameBuffer;
            }
        }
        return _name;
    }
    mdToken SignatureTokenType::GetToken()
    {
        return this->_token;
    }

    SignatureCompositeType::SignatureCompositeType(CorElementType type, SignatureType* relatedType) : SignatureType(type)
    {
        _relatedType = relatedType;
    }
    SignatureCompositeType::~SignatureCompositeType()
    {
    }
    HRESULT SignatureCompositeType::AddToSignature(ISignatureBuilder* pSignatureBuilder)
    {
        HRESULT hr = S_OK;
        IfFailRet(SignatureType::AddToSignature(pSignatureBuilder));
        IfFailRet(_relatedType->AddToSignature(pSignatureBuilder));
        return hr;
    }
    HRESULT SignatureCompositeType::GetRelatedType(SignatureType** type)
    {
        IfNullRet(type);
        if (type) { *type = _relatedType; }
        return S_OK;
    }
    WSTRING SignatureCompositeType::GetName()
    {
        WSTRING res = EmptyWStr;
        if (_relatedType)
        {
            res = _relatedType->GetName();
        }
        if (this->IsArray()) { res += WStr("[]"); } //TODO: Determine array rank
        return res;
    }

    SignatureFunctionType::SignatureFunctionType(CorCallingConvention callingConvention, SignatureType* pReturnType, const std::vector<SignatureType*>& parameters, DWORD dwGenericParameterCount)
        : SignatureType(ELEMENT_TYPE_FNPTR), _callingConvention(callingConvention), _pReturnType(pReturnType), _parameters(parameters), _genericParameterCount(dwGenericParameterCount)
    {
    }
    SignatureFunctionType::~SignatureFunctionType()
    {
    }
    HRESULT SignatureFunctionType::AddToSignature(ISignatureBuilder* pSignatureBuilder)
    {
        HRESULT hr = S_OK;
        IfFailRet(SignatureType::AddToSignature(pSignatureBuilder));

        IfFailRet(pSignatureBuilder->AddData((const BYTE*)&_callingConvention, 1));
        if (_callingConvention & IMAGE_CEE_CS_CALLCONV_GENERIC)
        {
            IfFailRet(pSignatureBuilder->Add(_genericParameterCount));
        }
        IfFailRet(pSignatureBuilder->Add((DWORD)(_parameters.size())));
        IfFailRet(_pReturnType->AddToSignature(pSignatureBuilder));
        for (SignatureType* parameter : _parameters)
        {
            IfFailRet(parameter->AddToSignature(pSignatureBuilder));
        }
        return hr;
    }

    SignatureArrayType::SignatureArrayType(SignatureType* relatedType, ULONG rank, const std::vector<ULONG>& counts, const std::vector<ULONG>& bounds)
        : SignatureCompositeType(ELEMENT_TYPE_ARRAY, relatedType), _rank(rank), _counts(counts), _bounds(bounds)
    {
    }
    SignatureArrayType::~SignatureArrayType()
    {
    }
    HRESULT SignatureArrayType::AddToSignature(ISignatureBuilder* pSignatureBuilder)
    {
        HRESULT hr = S_OK;
        IfFailRet(SignatureCompositeType::AddToSignature(pSignatureBuilder));

        IfFailRet(pSignatureBuilder->Add(_rank));
        IfFailRet(pSignatureBuilder->Add((DWORD)(_counts.size())));
        for (ULONG count : _counts)
        {
            IfFailRet(pSignatureBuilder->Add(count));
        }
        IfFailRet(pSignatureBuilder->Add((DWORD)(_bounds.size())));
        for (ULONG bound : _bounds)
        {
            IfFailRet(pSignatureBuilder->Add(bound));
        }
        return hr;
    }

    SignatureGenericParameterType::SignatureGenericParameterType(CorElementType type, ULONG position) : SignatureType(type), _position(position)
    {
    }
    SignatureGenericParameterType::~SignatureGenericParameterType()
    {
    }
    HRESULT SignatureGenericParameterType::GetPosition(ULONG* pPosition)
    {
        IfNullRet(pPosition);
        *pPosition = _position;
        return S_OK;
    }
    HRESULT SignatureGenericParameterType::AddToSignature(ISignatureBuilder* pSignatureBuilder)
    {
        HRESULT hr = S_OK;
        IfFailRet(SignatureType::AddToSignature(pSignatureBuilder));
        IfFailRet(pSignatureBuilder->Add(_position));
        return hr;
    }
    WSTRING SignatureGenericParameterType::GetName()
    {
        std::stringstream txt;
        txt << "!!" << _position;
        return ToWSTRING(txt.str());
    }

    SignatureGenericInstance::SignatureGenericInstance(SignatureType* typeDefinition, const std::vector<SignatureType*>& genericParameters) : SignatureCompositeType(ELEMENT_TYPE_GENERICINST, typeDefinition)
    {
        _genericParameters = genericParameters;
        //for (CType* type : genericParameters)
        //{
        //    m_genericParameters.push_back(CComPtr<CType>(type));
        //}
    }
    SignatureGenericInstance::~SignatureGenericInstance()
    {
    }
    HRESULT SignatureGenericInstance::AddToSignature(ISignatureBuilder* pSignatureBuilder)
    {
        HRESULT hr = S_OK;
        IfFailRet(SignatureCompositeType::AddToSignature(pSignatureBuilder));
        IfFailRet(pSignatureBuilder->Add((DWORD)(_genericParameters.size())));
        for (SignatureType* genericParameter : _genericParameters)
        {
            IfFailRet(genericParameter->AddToSignature(pSignatureBuilder));
        }
        return hr;
    }

    WSTRING SignatureGenericInstance::GetName()
    {
        std::stringstream buffer;
        buffer << ToString(SignatureCompositeType::GetName());
        buffer << "<";
        for (unsigned int x = 0; x < _genericParameters.size(); x++)
        {
            if (x > 0) { buffer << ","; }
            buffer << ToString(_genericParameters[x]->GetName());
        }
        buffer << ">";
        return ToWSTRING(buffer.str());
    }

    SignatureModifierType::SignatureModifierType(CorElementType type, mdToken token) : SignatureType(type), _token(token)
    {
    }
    SignatureModifierType::~SignatureModifierType()
    {
    }
    HRESULT SignatureModifierType::AddToSignature(ISignatureBuilder* pSignatureBuilder)
    {
        HRESULT hr = S_OK;

        IfFailRet(SignatureType::AddToSignature(pSignatureBuilder));
        IfFailRet(pSignatureBuilder->AddToken(_token));

        return hr;
    }
}