#pragma once

#include <string>
#include <vector>
#include <corhdr.h>
#include "TypeReference.h"

struct MemberReference
{
    TypeReference ContainingType{};
    std::wstring MethodName = L"";
    bool IsVirtual = false;
    CorCallingConvention CorCallingConvention = IMAGE_CEE_CS_CALLCONV_DEFAULT;
    TypeReference ReturnType{};
    std::vector<TypeReference> ArgumentTypes{};

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
