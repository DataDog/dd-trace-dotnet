#include "method_signature.h"

namespace instrumented_assembly_generator
{
namespace
{
    HRESULT ParseByte(PCCOR_SIGNATURE& pbCur, PCCOR_SIGNATURE pbEnd, BYTE* pbOut)
    {
        if (pbCur < pbEnd)
        {
            *pbOut = *pbCur;
            pbCur++;
            return S_OK;
        }

        return E_FAIL;
    }

    HRESULT ParseNumber(PCCOR_SIGNATURE& pbCur, const PCCOR_SIGNATURE pbEnd, unsigned* pOut)
    {
        HRESULT hr;
        // parse the variable _length number format (0-4 bytes)
        BYTE b1 = 0, b2 = 0, b3 = 0, b4 = 0;

        // at least one byte in the encoding, read that
        IfFailRet(ParseByte(pbCur, pbEnd, &b1));

        if (b1 == 0xff)
        {
            // special encoding of 'NULL'
            // not sure what this means as a number, don't expect to see it except for string lengths
            // which we don't encounter anyway so calling it an error
            return E_FAIL;
        }

        // early out on 1 byte encoding
        if ((b1 & 0x80) == 0)
        {
            *pOut = (int) b1;
            return S_OK;
        }

        // now at least 2 bytes in the encoding, read 2nd byte
        IfFailRet(ParseByte(pbCur, pbEnd, &b2));

        // early out on 2 byte encoding
        if ((b1 & 0x40) == 0)
        {
            *pOut = (((b1 & 0x3f) << 8) | b2);
            return S_OK;
        }

        // must be a 4 byte encoding
        if ((b1 & 0x20) != 0)
        {
            // 4 byte encoding has this bit clear -- error if not
            return E_FAIL;
        }

        IfFailRet(ParseByte(pbCur, pbEnd, &b3));

        IfFailRet(ParseByte(pbCur, pbEnd, &b4));

        *pOut = ((b1 & 0x1f) << 24) | (b2 << 16) | (b3 << 8) | b4;
        return S_OK;
    }

    bool IsSupportedElementType(const BYTE elemType)
    {
        switch (elemType)
        {
            case ELEMENT_TYPE_PTR:
                // CustomMod*  VOID
                // CustomMod*  Type

            case ELEMENT_TYPE_FNPTR:
                // FNPTR MethodDefSig
                // FNPTR MethodRefSig

            case ELEMENT_TYPE_ARRAY:
                // ARRAY Type ArrayShape

            case ELEMENT_TYPE_CMOD_OPT:
            case ELEMENT_TYPE_CMOD_REQD:
            case ELEMENT_TYPE_TYPEDBYREF:
                return false;

            default:
                break;
        }
        return true;
    }

    HRESULT ParseTypeDefOrRefEncoded(PCCOR_SIGNATURE& pbCur, const PCCOR_SIGNATURE pbEnd, BYTE* pTableTypeOut,
                                     unsigned* pIndexOut)
    {
        // parse an encoded typedef (0x02), typeref (0x01) or typespec (0x1b)
        unsigned encoded = 0;
        HRESULT hr;
        IfFailRet(ParseNumber(pbCur, pbEnd, &encoded));

        // Get the table type (typedef, typeref or typespec)
        *pTableTypeOut = (BYTE) (encoded & 0x3);

        // Get the index in the table
        *pIndexOut = (encoded >> 2);
        return S_OK;
    }

    HRESULT ParseType(PCCOR_SIGNATURE& pbCur, const PCCOR_SIGNATURE pbEnd)
    {
        /*
        Type ::=
          BOOLEAN | CHAR | I1 | U1 | I2 | U2 | I4 | U4 | I8 | U8 | R4 | R8 | I | U
        | ARRAY Type ArrayShape (general array, see §II.23.2.13)
        | CLASS TypeDefOrRefOrSpecEncoded
        | FNPTR MethodDefSig
        | FNPTR MethodRefSig
        | GENERICINST (CLASS | VALUETYPE) TypeDefOrRefOrSpecEncoded GenArgCount Type*
        | MVAR number
        | OBJECT
        | PTR CustomMod* Type
        | PTR CustomMod* VOID
        | STRING
        | SZARRAY CustomMod* Type (single dimensional, zero-based array i.e., *vector)
        | VALUETYPE TypeDefOrRefOrSpecEncoded
        | VAR number
        */

        BYTE elemType;
        BYTE indexType;
        unsigned index;
        unsigned number;
        HRESULT hr;
        IfFailRet(ParseByte(pbCur, pbEnd, &elemType));

        // Exit early for unsupported types
        if (!IsSupportedElementType(elemType))
        {
            return E_FAIL;
        }

        // All simple types
        if (elemType <= ELEMENT_TYPE_STRING || elemType == ELEMENT_TYPE_OBJECT || elemType == ELEMENT_TYPE_I ||
            elemType == ELEMENT_TYPE_U)
            return S_OK;

        // Type will be in the next byte
        if (elemType == ELEMENT_TYPE_BYREF) return ParseType(pbCur, pbEnd);

        // Parse type
        switch (elemType)
        {
            case ELEMENT_TYPE_CLASS:
            case ELEMENT_TYPE_VALUETYPE:
                // TypeDefOrRefEncoded
                IfFailRet(ParseTypeDefOrRefEncoded(pbCur, pbEnd, &indexType, &index));
                break;

            case ELEMENT_TYPE_SZARRAY:
                // SZARRAY Type

                // CMOD is not supported
                if (*pbCur == ELEMENT_TYPE_CMOD_OPT || *pbCur == ELEMENT_TYPE_CMOD_REQD)
                {
                    return E_FAIL;
                }

                IfFailRet(ParseType(pbCur, pbEnd));

                break;

            case ELEMENT_TYPE_GENERICINST:
                // GENERICINST (CLASS | VALUETYPE) TypeDefOrRefEncoded GenArgCount Type Type*

                IfFailRet(ParseByte(pbCur, pbEnd, &elemType));

                if (elemType != ELEMENT_TYPE_CLASS && elemType != ELEMENT_TYPE_VALUETYPE) return E_FAIL;

                IfFailRet(ParseTypeDefOrRefEncoded(pbCur, pbEnd, &indexType, &index));

                IfFailRet(ParseNumber(pbCur, pbEnd, &number));

                for (unsigned i = 0; i < number; i++)
                {
                    IfFailRet(ParseType(pbCur, pbEnd));
                }

                break;

            case ELEMENT_TYPE_VAR:
            case ELEMENT_TYPE_MVAR:
                // Class type or method type <T> number
                IfFailRet(ParseNumber(pbCur, pbEnd, &number));

                break;

            default:
                return E_FAIL;
        }
        return S_OK;
    }

    HRESULT ParseParam(PCCOR_SIGNATURE& pbCur, const PCCOR_SIGNATURE pbEnd)
    {
        // Param ::= CustomMod* ( TYPEDBYREF | [BYREF] Type )
        HRESULT hr;
        if (pbCur >= pbEnd) return E_FAIL;

        if (*pbCur == ELEMENT_TYPE_CMOD_OPT || *pbCur == ELEMENT_TYPE_CMOD_REQD || *pbCur == ELEMENT_TYPE_TYPEDBYREF)
            return E_FAIL;

        if (*pbCur == ELEMENT_TYPE_BYREF)
            // Type will be in the next byte
            pbCur++;

        IfFailRet(ParseType(pbCur, pbEnd));

        return S_OK;
    }

    HRESULT ParseRetType(PCCOR_SIGNATURE& pbCur, const PCCOR_SIGNATURE pbEnd)
    {
        // RetType ::= CustomMod* ( VOID | TYPEDBYREF | [BYREF] Type )
        HRESULT hr;
        if (pbCur >= pbEnd) return E_FAIL;

        if (*pbCur == ELEMENT_TYPE_CMOD_OPT || *pbCur == ELEMENT_TYPE_CMOD_REQD)
        {
            return E_FAIL;
        }

        if (*pbCur == ELEMENT_TYPE_TYPEDBYREF)
        {
            return E_FAIL;
        }

        if (*pbCur == ELEMENT_TYPE_VOID)
        {
            pbCur++;
            return S_OK;
        }

        if (*pbCur == ELEMENT_TYPE_BYREF)
        {
            pbCur++;
        }

        IfFailRet(ParseType(pbCur, pbEnd));

        return S_OK;
    }
} // namespace

HRESULT MethodSignature::Parse()
{
    if (_isParsed) return S_OK;
    HRESULT hr;
    PCCOR_SIGNATURE pbCur = _methodSig;
    PCCOR_SIGNATURE pbEnd = _methodSig + _sigLength;
    BYTE elemType;
    IfFailRet(ParseByte(pbCur, pbEnd, &elemType));

    if (elemType & IMAGE_CEE_CS_CALLCONV_GENERIC)
    {
        unsigned genericArgCount;
        IfFailRet(ParseNumber(pbCur, pbEnd, &genericArgCount));
        _numberOfTypeArguments = genericArgCount;
    }

    unsigned argumentsCount;
    IfFailRet(ParseNumber(pbCur, pbEnd, &argumentsCount));
    _numberOfArguments = argumentsCount;

    const PCCOR_SIGNATURE pbRet = pbCur;

    IfFailRet(ParseRetType(pbCur, pbEnd));

    auto length = pbCur - pbRet;
    auto offset = pbCur - _methodSig - length;
    _pRet = std::make_shared<MemberSignature>(_methodSig, static_cast<ULONG>(length), static_cast<ULONG>(offset));

    auto encounteredSentinel = false;
    for (unsigned i = 0; i < argumentsCount; i++)
    {
        if (pbCur >= pbEnd) return E_FAIL;

        if (*pbCur == ELEMENT_TYPE_SENTINEL)
        {
            if (encounteredSentinel) return E_FAIL;

            encounteredSentinel = true;
            pbCur++;
        }

        const PCCOR_SIGNATURE pbParam = pbCur;

        IfFailRet(ParseParam(pbCur, pbEnd));

        length = pbCur - pbParam;
        offset = pbCur - _methodSig - length;

        _arguments.emplace_back(MemberSignature(_methodSig, static_cast<ULONG>(length), static_cast<ULONG>(offset)));
    }
    _isParsed = true;
    return S_OK;
}
} // namespace instrumented_assembly_generator