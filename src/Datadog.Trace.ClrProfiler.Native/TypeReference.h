#pragma once

#include <string>
#include <corhdr.h>

struct TypeReference
{
    CorElementType CorElementType;
    std::wstring AssemblyName = L"";
    std::wstring TypeName = L"";
    bool IsArray = false;

    // used if CorElementType is ELEMENT_TYPE_SZARRAY
    const TypeReference* ArrayType = nullptr;

    friend bool operator==(const TypeReference& lhs, const TypeReference& rhs)
    {
        return lhs.CorElementType == rhs.CorElementType
               && lhs.AssemblyName == rhs.AssemblyName
               && lhs.TypeName == rhs.TypeName
               && lhs.IsArray == rhs.IsArray;
    }

    friend bool operator!=(const TypeReference& lhs, const TypeReference& rhs)
    {
        return !(lhs == rhs);
    }

    // seems like relational operators are required because
    // this type is used as the key type for a std::map
    friend bool operator<(const TypeReference& lhs, const TypeReference& rhs)
    {
        if (lhs.AssemblyName < rhs.AssemblyName)
            return true;
        if (rhs.AssemblyName < lhs.AssemblyName)
            return false;
        return lhs.TypeName < rhs.TypeName;
    }

    friend bool operator<=(const TypeReference& lhs, const TypeReference& rhs)
    {
        return !(rhs < lhs);
    }

    friend bool operator>(const TypeReference& lhs, const TypeReference& rhs)
    {
        return rhs < lhs;
    }

    friend bool operator>=(const TypeReference& lhs, const TypeReference& rhs)
    {
        return !(lhs < rhs);
    }
};
