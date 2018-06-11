#pragma once

#include <map>
#include <corhlpr.h>

// forward declarations
struct TypeReference;
struct MemberReference;

template <typename TKey, typename TValue>
class Lookup
{
private:
    static const TValue m_default{};
    std::map<TKey, TValue> m_map{};

public:
    const TValue& operator[](const TKey& key) const
    {
        auto search = m_map.find(key);

        if (search != m_map.end())
        {
            return search->second;
        }

        return m_default;
    }

    TValue& operator[](const TKey& key)
    {
        return m_map[key];
    }
};

typedef Lookup<TypeReference, mdTypeRef> TypeRefLookup;
typedef Lookup<MemberReference, mdMemberRef> MemberRefLookup;
