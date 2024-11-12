#include "signature_info.h"
#include "iast_util.h"
#include "module_info.h"

namespace iast
{
    SignatureInfo::SignatureInfo(ModuleInfo* moduleInfo, PCCOR_SIGNATURE pSig, DWORD nSig)
    {
        //SignatureParser parser;
        _module = moduleInfo;
        this->_pSig = pSig;
        this->_nSig = nSig;
        _signatureType = SignatureTypes::Unknown;

        ULONG cbRead = 0;
        HRESULT hr = S_OK;
        // Read the calling convention
        _callingConvention = IMAGE_CEE_CS_CALLCONV_MAX;
        CorSigUncompressData(pSig, (ULONG*)&_callingConvention);
        if (_callingConvention == IMAGE_CEE_CS_CALLCONV_FIELD)
        {
            _signatureType = SignatureTypes::Field;
            hr = ParseFieldSignature(pSig, nSig, &cbRead);
        }
        else if (_callingConvention == IMAGE_CEE_CS_CALLCONV_GENERICINST)
        {
            _signatureType = SignatureTypes::TypeSpec;
            hr = ParseGenericInstSignature(pSig, nSig, &cbRead);
        }
        else if (_callingConvention == IMAGE_CEE_CS_CALLCONV_LOCAL_SIG)
        {
            _signatureType = SignatureTypes::LocalsSignature;
            hr = ParseLocalsSignature(pSig, nSig, &cbRead);
        }
        else if (_callingConvention == IMAGE_CEE_CS_CALLCONV_PROPERTY)
        {
            _signatureType = SignatureTypes::Property;
            hr = ParsePropertySignature(pSig, nSig, &cbRead);
        }
        else if (_callingConvention == IMAGE_CEE_CS_CALLCONV_MAX)
        {
            trace::Logger::Error("ERROR: Unexpected calling convention on method signature.");
        }
        else
        {
            hr = ParseMethodSignature(pSig, nSig, (ULONG*)&_callingConvention, &_returnType, &_params, &_genericParamCount, &cbRead);
        }

        if (_params.size() > 0)
        {
            std::stringstream buffer;
            buffer << "(";
            int count = 0;
            for (auto p : _params)
            {
                if (count++ > 0)
                {
                    buffer << ",";
                }
                buffer << ToString(p->GetName());
            }
            buffer << ")";
            _paramsString = ToWSTRING(buffer.str());
        }
        else
        {
            _paramsString = WStr("()");
        }

        if (_returnType)
        {
            _returnTypeString = _returnType->GetName();
        }
    }

    SignatureInfo::~SignatureInfo()
    {
        DEL(_returnType);
        for (auto p : _params)
        {
            delete(p);
        }
        _params.clear();
    }
    WSTRING& SignatureInfo::GetReturnTypeString()
    {
        return _returnTypeString;
    }
    WSTRING& SignatureInfo::GetParamsRepresentation()
    {
        return _paramsString;
    }


    HRESULT SignatureInfo::AddElementType(CorElementType corType)
    {
        COR_SIGNATURE buffer[32]; COR_SIGNATURE* sigBuilder = buffer;
        sigBuilder += CorSigCompressElementType(corType, sigBuilder);
        for (int x = 0; x < sigBuilder - buffer; x++)
        {
            _dynamicSig.push_back(buffer[x]);
        }
        return S_OK;
    }
    HRESULT SignatureInfo::AddToken(mdToken token)
    {
        COR_SIGNATURE buffer[32]; COR_SIGNATURE* sigBuilder = buffer;
        sigBuilder += CorSigCompressToken(token, sigBuilder);
        for (int x = 0; x < sigBuilder - buffer; x++)
        {
            _dynamicSig.push_back(buffer[x]);
        }
        return S_OK;
    }
    HRESULT SignatureInfo::AddData(const BYTE* data, ULONG size)
    {
        for (ULONG x = 0; x < size; x++)
        {
            _dynamicSig.push_back(data[x]);
        }
        return S_OK;
    }
    HRESULT SignatureInfo::Add(DWORD data)
    {
        COR_SIGNATURE buffer[32]; COR_SIGNATURE* sigBuilder = buffer;
        sigBuilder += CorSigCompressData(data, sigBuilder);
        for (int x = 0; x < sigBuilder - buffer; x++)
        {
            _dynamicSig.push_back(buffer[x]);
        }
        return S_OK;
    }

    HRESULT SignatureInfo::FromSignature(DWORD cbBuffer, const BYTE* pCorSignature, SignatureType** ppType, DWORD* pdwSigSize)
    {
        if (pCorSignature == nullptr || cbBuffer == 0)
        {
            return E_INVALIDARG;
        }

        *ppType = nullptr;
        *pdwSigSize = 0;

        HRESULT hr = S_OK;
        PCCOR_SIGNATURE currentSignature = pCorSignature;
        CorElementType sigElement = ELEMENT_TYPE_END;
        DWORD currentSize = 1;

        SignatureType* createdType;

        switch ((sigElement = static_cast<CorElementType>(*currentSignature)))
        {
        case ELEMENT_TYPE_VOID:
        case ELEMENT_TYPE_BOOLEAN:
        case ELEMENT_TYPE_CHAR:
        case ELEMENT_TYPE_I1:
        case ELEMENT_TYPE_U1:
        case ELEMENT_TYPE_I2:
        case ELEMENT_TYPE_U2:
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
        case ELEMENT_TYPE_TYPEDBYREF:
        {
            createdType = new SignatureSimpleType(sigElement);
            if (createdType == nullptr)
            {
                return E_OUTOFMEMORY;
            }
        }
        break;
        case ELEMENT_TYPE_CLASS:
        case ELEMENT_TYPE_VALUETYPE:
        {
            mdToken tokenValue;
            currentSize += CorSigUncompressToken(&currentSignature[currentSize], &tokenValue);
            IfFailRet(FromToken(sigElement, tokenValue, &createdType));
        }
        break;
        case ELEMENT_TYPE_PTR:
        case ELEMENT_TYPE_BYREF:
        case ELEMENT_TYPE_SZARRAY:
        {
            DWORD newSize;
            SignatureType* relatedType;
            IfFailRet(FromSignature(cbBuffer - currentSize, &currentSignature[currentSize], &relatedType, &newSize));
            createdType = new SignatureCompositeType(sigElement, relatedType);
            if (createdType == nullptr)
            {
                return E_OUTOFMEMORY;
            }
            currentSize += newSize;
        }
        break;
        case ELEMENT_TYPE_FNPTR:
        {
            SignatureType* pReturnType;
            ULONG convention = IMAGE_CEE_CS_CALLCONV_MAX;
            std::vector<SignatureType*> parameterTypes;
            DWORD genericCount = 0;

            DWORD newSize = 0;
            IfFailRet(ParseMethodSignature(
                &currentSignature[currentSize],
                cbBuffer - currentSize,
                &convention,
                &pReturnType,
                &parameterTypes,
                &genericCount,
                &newSize));
            currentSize += newSize;

            IfFailRet(hr);
            hr = S_OK;

            createdType = new SignatureFunctionType((CorCallingConvention)convention, pReturnType, parameterTypes, genericCount);
            if (createdType == nullptr)
            {
                return E_OUTOFMEMORY;
            }
        }
        break;
        case ELEMENT_TYPE_ARRAY:
        {
            DWORD newSize;
            SignatureType* relatedType;
            IfFailRet(FromSignature(cbBuffer - currentSize, &currentSignature[currentSize], &relatedType, &newSize));
            currentSize += newSize;
            IfFailRet(cbBuffer > currentSize ? S_OK : E_UNEXPECTED);
            ULONG rank;
            ULONG sizeCount;
            ULONG boundsCount;

            currentSize += CorSigUncompressData(&currentSignature[currentSize], &rank);
            currentSize += CorSigUncompressData(&currentSignature[currentSize], &sizeCount);
            std::vector<ULONG> counts(sizeCount);

            for (ULONG iSizeIndex = 0; iSizeIndex < sizeCount; iSizeIndex++)
            {
                ULONG count;
                currentSize += CorSigUncompressData(&currentSignature[currentSize], &count);
                counts[iSizeIndex] = count;
            }

            currentSize += CorSigUncompressData(&currentSignature[currentSize], &boundsCount);
            std::vector<ULONG> bounds(boundsCount);

            for (ULONG iBoundIndex = 0; iBoundIndex < boundsCount; iBoundIndex++)
            {
                ULONG bound;
                currentSize += CorSigUncompressData(&currentSignature[currentSize], &bound);
                bounds[iBoundIndex] = bound;
            }

            createdType = new SignatureArrayType(relatedType, rank, counts, bounds);
            if (createdType == nullptr)
            {
                return E_OUTOFMEMORY;
            }
        }
        break;
        case ELEMENT_TYPE_MVAR:
        case ELEMENT_TYPE_VAR:
        {
            ULONG position;
            currentSize += CorSigUncompressData(&currentSignature[currentSize], &position);

            createdType = new SignatureGenericParameterType(sigElement, position);
            if (createdType == nullptr)
            {
                return E_OUTOFMEMORY;
            }
        }
        break;
        case ELEMENT_TYPE_GENERICINST:
        {
            DWORD newSize;
            SignatureType* relatedType;
            IfFailRet(FromSignature(cbBuffer - currentSize, &currentSignature[currentSize], &relatedType, &newSize));
            currentSize += newSize;
            IfFailRet(cbBuffer > currentSize ? S_OK : E_UNEXPECTED);
            ULONG argumentCount = 0;
            currentSize += CorSigUncompressData(&currentSignature[currentSize], &argumentCount);
            std::vector<SignatureType*> parameters;
            for (ULONG i = 0; i < argumentCount; i++)
            {
                SignatureType* parameterType = nullptr;
                IfFailRet(FromSignature(cbBuffer - currentSize, &currentSignature[currentSize], &parameterType, &newSize));
                currentSize += newSize;
                IfFailRet(cbBuffer >= currentSize ? S_OK : E_UNEXPECTED);
                parameters.push_back(parameterType);
            }

            createdType = new SignatureGenericInstance(relatedType, parameters);
            if (createdType == nullptr)
            {
                return E_OUTOFMEMORY;
            }
        }
        break;
        case ELEMENT_TYPE_PINNED:
        {
            DWORD newSize = 0;
            IfFailRet(FromSignature(cbBuffer - currentSize, &currentSignature[currentSize], &createdType, &newSize));
            static_cast<SignatureType*>(createdType)->SetIsPinned(true);
            currentSize += newSize;
        }
        break;
        case ELEMENT_TYPE_SENTINEL:
        {
            DWORD newSize = 0;
            IfFailRet(FromSignature(cbBuffer - currentSize, &currentSignature[currentSize], &createdType, &newSize));
            static_cast<SignatureType*>(createdType)->SetIsSentinel(true);
            currentSize += newSize;
        }
        break;
        case ELEMENT_TYPE_CMOD_REQD:
        case ELEMENT_TYPE_CMOD_OPT:
        {
            mdToken token = mdTokenNil;
            currentSize += CorSigUncompressToken(&currentSignature[currentSize], &token);
            std::vector<SignatureType*> modifiers;

            SignatureType* pModifierType;
            pModifierType = new SignatureModifierType(sigElement, token);
            if (pModifierType == nullptr)
            {
                return E_OUTOFMEMORY;
            }

            modifiers.push_back(pModifierType);

            CorElementType modifierElementType = (CorElementType)currentSignature[currentSize];
            while ((modifierElementType == ELEMENT_TYPE_CMOD_REQD) || (modifierElementType == ELEMENT_TYPE_CMOD_OPT))
            {
                ++currentSize;
                mdToken tokenInner = mdTokenNil;
                currentSize += CorSigUncompressToken(&currentSignature[currentSize], &tokenInner);
                SignatureType* pNextModifierType;
                pNextModifierType = new SignatureModifierType(modifierElementType, tokenInner);
                if (pNextModifierType == nullptr)
                {
                    return E_OUTOFMEMORY;
                }
                modifiers.push_back(pNextModifierType);
                modifierElementType = (CorElementType)currentSignature[currentSize];
            }

            DWORD newSize = 0;
            IfFailRet(FromSignature(cbBuffer - currentSize, &currentSignature[currentSize], &createdType, &newSize));
            currentSize += newSize;

            static_cast<SignatureType*>(createdType)->SetModifers(modifiers);
        }
        break;
        default:
            trace::Logger::Error("ERROR: Unexpected element type. This usually indicates a signature parsing bug");
            return E_NOTIMPL;
        }

        if (createdType)
        {
            *ppType = createdType;
            *pdwSigSize = currentSize;
            hr = S_OK;
        }
        else
        {
            //CLogging::LogError(_T("Type %d is not yet supported"), sigElement);
            hr = E_FAIL;
        }

        //CLogging::LogMessage(_T("End CTypeCreator::FromSignature"));
        return hr;
    }
    HRESULT SignatureInfo::FromToken(CorElementType type, mdToken token, SignatureType** ppType)
    {
        if (ppType == nullptr)
        {
            return E_POINTER;
        }
        if ((type != ELEMENT_TYPE_CLASS) && (type != ELEMENT_TYPE_VALUETYPE))
        {
            return E_INVALIDARG;
        }

        *ppType = nullptr;

        HRESULT hr = S_OK;

        SignatureType* tokenType;
        tokenType = new SignatureTokenType(_module, token, type);
        if (tokenType == nullptr)
        {
            return E_OUTOFMEMORY;
        }
        *ppType = tokenType;
        return hr;
    }
    HRESULT SignatureInfo::IsValueType(mdTypeDef mdTypeDefToken, BOOL* pIsValueType)
    {
        if (pIsValueType == nullptr)
        {
            return E_POINTER;
        }

        HRESULT hr = S_OK;
        *pIsValueType = FALSE;

        IMetaDataImport* pMetadataImport = _module->GetMetaDataImport();

        mdToken tkBaseType = mdTokenNil;
        DWORD flags = 0;
        IfFailRet(pMetadataImport->GetTypeDefProps(mdTypeDefToken, nullptr, 0, nullptr, &flags, &tkBaseType));

        if (IsTdInterface(flags) || IsTdAbstract(flags) || (!IsTdSealed(flags)) ||
            ((TypeFromToken(tkBaseType) != mdtTypeDef) && (TypeFromToken(tkBaseType) != mdtTypeRef)))
        {
            return S_OK;
        }

        SignatureType* pBaseType;
        IfFailRet(FromToken(ELEMENT_TYPE_CLASS, tkBaseType, &pBaseType));
        WSTRING baseName = pBaseType->GetName();

        if ((WStr("System.ValueType") == baseName) || (WStr("System.Enum") == baseName))
        {
            *pIsValueType = TRUE;
        }
        return S_OK;
    }
    HRESULT SignatureInfo::ParseTypeSequence(const BYTE* pBuffer, ULONG cbBuffer, ULONG cTypes, std::vector<SignatureType*>* ppEnumTypes, ULONG* pcbRead)
    {
        if (pBuffer == nullptr)
        {
            return E_POINTER;
        }
        if (pcbRead) { *pcbRead = 0; }

        HRESULT hr = S_OK;

        ULONG cbRead = 0;
        ULONG cbReadType;

        std::vector<SignatureType*> types;
        for (ULONG index = 0; index < cTypes; index++)
        {
            SignatureType* pType;
            IfFailRet(FromSignature(cbBuffer - cbRead, pBuffer + cbRead, &pType, &cbReadType));
            cbRead += cbReadType;
            IfFailRet(cbBuffer >= cbRead ? S_OK : E_UNEXPECTED);

            types.push_back(pType);
        }

        if (ppEnumTypes) { *ppEnumTypes = types; }
        if (pcbRead) { *pcbRead = cbRead; }

        return S_OK;
    }

    HRESULT SignatureInfo::ParseMethodSignature(const BYTE* pSignature, DWORD cbSignature, ULONG* pCallingConvention, SignatureType** ppReturnType, std::vector<SignatureType*>* ppEnumParameterTypes, ULONG* pcGenericTypeParameters, ULONG* pcbRead)
    {
        if (pSignature == nullptr)
        {
            return E_POINTER;
        }
        if (pCallingConvention) { *pCallingConvention = IMAGE_CEE_CS_CALLCONV_MAX; }
        if (ppReturnType) { *ppReturnType = nullptr; }
        if (pcGenericTypeParameters) { *pcGenericTypeParameters = 0; }
        if (pcbRead) { *pcbRead = 0; }

        HRESULT hr = S_OK;

        ULONG cbRead = 0;

        // Read the calling convention
        ULONG callingConvention = IMAGE_CEE_CS_CALLCONV_MAX;
        cbRead = CorSigUncompressData(pSignature, &callingConvention);
        if (callingConvention == IMAGE_CEE_CS_CALLCONV_FIELD)
        {
            _signatureType = SignatureTypes::Field;
            //ParseFieldSignature(pSignature, cbSignature);
            return E_UNEXPECTED;
        }
        else if (callingConvention == IMAGE_CEE_CS_CALLCONV_GENERICINST)
        {
            _signatureType = SignatureTypes::TypeSpec;
            //ParseGenericInstSignature(pSignature, cbSignature);
            return E_UNEXPECTED;
        }
        else if (callingConvention == IMAGE_CEE_CS_CALLCONV_LOCAL_SIG)
        {
            _signatureType = SignatureTypes::LocalsSignature;
            //ParseLocalsSignature(pSignature, cbSignature);
            return E_UNEXPECTED;
        }
        else if (callingConvention == IMAGE_CEE_CS_CALLCONV_PROPERTY)
        {
            _signatureType = SignatureTypes::Property;
            //ParsePropertySignature(pSignature, cbSignature);
            return E_UNEXPECTED;
        }
        else if (callingConvention == IMAGE_CEE_CS_CALLCONV_MAX)
        {
            trace::Logger::Error("ERROR: Unexpected calling convention on method signature.");
            return E_UNEXPECTED;
        }
        IfFailRet(cbSignature > cbRead ? S_OK : E_UNEXPECTED);
        _signatureType = SignatureTypes::Method;

        // Read number of generic type parameters
        ULONG cGenericTypeParameters = 0;
        if ((callingConvention & IMAGE_CEE_CS_CALLCONV_GENERIC) == IMAGE_CEE_CS_CALLCONV_GENERIC)
        {
            cbRead += CorSigUncompressData(pSignature + cbRead, &cGenericTypeParameters);
            IfFailRet(cbSignature > cbRead ? S_OK : E_UNEXPECTED);
        }

        // Read number of parameters
        ULONG cParameterTypes = 0;
        cbRead += CorSigUncompressData(pSignature + cbRead, &cParameterTypes);
        IfFailRet(cbSignature > cbRead ? S_OK : E_UNEXPECTED);

        ULONG cbReadType;

        // Read return type
        SignatureType* pReturnType;
        IfFailRet(FromSignature(cbSignature - cbRead, pSignature + cbRead, &pReturnType, &cbReadType));
        cbRead += cbReadType;
        IfFailRet(cbSignature >= cbRead ? S_OK : E_UNEXPECTED);

        // Read parameter types
        IfFailRet(ParseTypeSequence(pSignature + cbRead, cbSignature - cbRead, cParameterTypes, ppEnumParameterTypes, &cbReadType));
        cbRead += cbReadType;
        IfFailRet(cbSignature >= cbRead ? S_OK : E_UNEXPECTED);

        if (pCallingConvention) { *pCallingConvention = callingConvention; }
        if (ppReturnType) { *ppReturnType = pReturnType; }
        if (pcGenericTypeParameters) { *pcGenericTypeParameters = cGenericTypeParameters; }
        if (pcbRead) { *pcbRead = cbRead; }

        return S_OK;
    }

    HRESULT SignatureInfo::ParseFieldSignature(const BYTE* pSignature, DWORD cbSignature, ULONG* pcbRead)
    {
        HRESULT hr = S_OK;
        ULONG cbRead = 0;
        // Read the calling convention
        ULONG callingConvention = IMAGE_CEE_CS_CALLCONV_MAX;
        cbRead = CorSigUncompressData(pSignature, &callingConvention);
        if (callingConvention != IMAGE_CEE_CS_CALLCONV_FIELD)
        {
            return E_FAIL;
        }
        hr = FromSignature(cbSignature - cbRead, pSignature + cbRead, &_returnType, &cbRead);
        return hr;
    }
    HRESULT SignatureInfo::ParseGenericInstSignature(const BYTE* pSignature, DWORD cbSignature, ULONG* pcbRead)
    {
        HRESULT hr = S_OK;
        ULONG cbRead = 0;
        // Read the calling convention
        ULONG callingConvention = IMAGE_CEE_CS_CALLCONV_MAX;
        cbRead = CorSigUncompressData(pSignature, &callingConvention);
        if (callingConvention != IMAGE_CEE_CS_CALLCONV_GENERICINST)
        {
            return E_FAIL;
        }
        //Read
        hr = FromSignature(cbSignature - cbRead, pSignature + cbRead, &_returnType, &cbRead);
        return hr;
    }
    HRESULT SignatureInfo::ParseLocalsSignature(const BYTE* pSignature, DWORD cbSignature, ULONG* pcbRead)
    {
        HRESULT hr = S_OK;
        ULONG cbRead = 0;
        // Read the calling convention
        ULONG callingConvention = IMAGE_CEE_CS_CALLCONV_MAX;
        cbRead = CorSigUncompressData(pSignature, &callingConvention);
        if (callingConvention != IMAGE_CEE_CS_CALLCONV_LOCAL_SIG)
        {
            return E_FAIL;
        }
        // Read Param Count
        ULONG paramCount = 0;
        cbRead += CorSigUncompressData(pSignature + cbRead, &paramCount);
        //Read Params
        for (ULONG x = 0; x < paramCount; x++)
        {
            SignatureType* paramType;
            ULONG paramRead = 0;
            IfFailRet(FromSignature(cbSignature - cbRead, pSignature + cbRead, &paramType, &paramRead));
            cbRead += paramRead;
            _params.push_back(paramType);
        }
        return hr;
    }
    HRESULT SignatureInfo::ParsePropertySignature(const BYTE* pSignature, DWORD cbSignature, ULONG* pcbRead)
    {
        HRESULT hr = S_OK;
        ULONG cbRead = 0;
        // Read the calling convention
        ULONG callingConvention = IMAGE_CEE_CS_CALLCONV_MAX;
        cbRead = CorSigUncompressData(pSignature, &callingConvention);
        if (callingConvention != IMAGE_CEE_CS_CALLCONV_PROPERTY)
        {
            return E_FAIL;
        }
        hr = FromSignature(cbSignature - cbRead, pSignature + cbRead, &_returnType, &cbRead);
        return hr;
    }

    SignatureTypes SignatureInfo::GetType()
    {
        return _signatureType;
    }

    PCCOR_SIGNATURE SignatureInfo::GetSignature(DWORD* sigSize)
    {
        if (!_pSig)
        {
            _pSig = &_dynamicSig[0];
            _nSig = (DWORD)_dynamicSig.size();
        }
        if (sigSize) { *sigSize = _nSig; }
        return _pSig;
    }
    bool SignatureInfo::HasThis()
    {
        return (_callingConvention & IMAGE_CEE_CS_CALLCONV_HASTHIS) == IMAGE_CEE_CS_CALLCONV_HASTHIS;
    }
    int SignatureInfo::GetEffectiveParamCount()
    {
        auto res = _params.size();
        if (HasThis()) { res++; }
        return (int)res;
    }

    WSTRING SignatureInfo::CharacterizeMember(WSTRING memberName)
    {
        if (_signatureType == SignatureTypes::Method)
        {
            auto paramsStr = GetParamsRepresentation();
            return memberName.c_str() + paramsStr;
        }
        return memberName;
    }
}
