#pragma once

#include <string>
#include <vector>
#include "TypeReference.h"
#include "MetadataReferenceLookups.h"

struct MemberReference
{
    TypeReference ContainingType{};
    std::wstring MethodName = L"";
    bool IsVirtual = false;
    CorCallingConvention CorCallingConvention = IMAGE_CEE_CS_CALLCONV_DEFAULT;
    TypeReference ReturnType{};
    std::vector<TypeReference> ArgumentTypes{};

private:
    static void AddElementTypeToSignature(PCOR_SIGNATURE pSignature,
                                          ULONG& signatureLength,
                                          const TypeReference& type,
                                          const TypeRefLookup& typeRefLookup)
    {
        // TODO: check bounds limit on pSignature[]
        pSignature[signatureLength++] = type.CorElementType;

        if (type.CorElementType == ELEMENT_TYPE_SZARRAY)
        {
            // recursive call to add the array type
            AddElementTypeToSignature(pSignature, signatureLength, *type.ArrayType, typeRefLookup);
        }
        else if (type.CorElementType == ELEMENT_TYPE_CLASS ||
                 type.CorElementType == ELEMENT_TYPE_VALUETYPE)
        {
            const mdTypeRef typeRef = typeRefLookup[type];
            COR_SIGNATURE compressedToken[8];
            const ULONG compressedTokenSize = CorSigCompressToken(typeRef, compressedToken);

            for (ULONG i = 0; i < compressedTokenSize; ++i)
            {
                pSignature[signatureLength++] = compressedToken[i];
            }
        }
    }

public:
    ULONG CreateSignature(const TypeRefLookup& typeRefLookup, PCOR_SIGNATURE pSignature) const
    {
        // member signature:
        //   calling convention
        //   argument count
        //   return type
        //   argument types

        // TODO: check bounds limit on pSignature[]
        ULONG signatureLength = 0;
        pSignature[signatureLength++] = CorCallingConvention;
        pSignature[signatureLength++] = static_cast<COR_SIGNATURE>(ArgumentTypes.size());

        // add return type to signature
        AddElementTypeToSignature(pSignature, signatureLength, ReturnType, typeRefLookup);

        // add arguments types to signature
        for (const TypeReference& argumentType : ArgumentTypes)
        {
            AddElementTypeToSignature(pSignature, signatureLength, argumentType, typeRefLookup);
        }

        return signatureLength;
    }

    // seems like relational operators are required because
    // this type is used as the key type for a std::map
    friend bool operator<(const MemberReference& lhs, const MemberReference& rhs)
    {
        if (lhs.ContainingType < rhs.ContainingType)
            return true;
        if (rhs.ContainingType < lhs.ContainingType)
            return false;
        if (lhs.MethodName < rhs.MethodName)
            return true;
        if (rhs.MethodName < lhs.MethodName)
            return false;
        if (lhs.IsVirtual < rhs.IsVirtual)
            return true;
        if (rhs.IsVirtual < lhs.IsVirtual)
            return false;
        if (lhs.CorCallingConvention < rhs.CorCallingConvention)
            return true;
        if (rhs.CorCallingConvention < lhs.CorCallingConvention)
            return false;
        if (lhs.ReturnType < rhs.ReturnType)
            return true;
        if (rhs.ReturnType < lhs.ReturnType)
            return false;
        if (lhs.ArgumentTypes.size() < rhs.ArgumentTypes.size())
            return true;
        if (rhs.ArgumentTypes.size() < lhs.ArgumentTypes.size())
            return false;

        for (size_t i = 0; i < lhs.ArgumentTypes.size(); ++i)
        {
            if (lhs.ArgumentTypes[i] < rhs.ArgumentTypes[i])
                return true;
            if (rhs.ArgumentTypes[i] < lhs.ArgumentTypes[i])
                return false;
        }

        return false;
    }

    friend bool operator<=(const MemberReference& lhs, const MemberReference& rhs)
    {
        return !(rhs < lhs);
    }

    friend bool operator>(const MemberReference& lhs, const MemberReference& rhs)
    {
        return rhs < lhs;
    }

    friend bool operator>=(const MemberReference& lhs, const MemberReference& rhs)
    {
        return !(lhs < rhs);
    }
};
