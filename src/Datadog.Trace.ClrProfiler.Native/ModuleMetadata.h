#pragma once

#include <unordered_map>
#include <corhlpr.h>
#include "Integration.h"

class ModuleMetadata
{
private:
    std::unordered_map<std::wstring, mdMemberRef> wrapper_refs{};
    std::unordered_map<std::wstring, mdTypeRef> wrapper_parent_type{};

public:
    std::wstring assemblyName = L"";
    std::vector<integration> m_Integrations = {};

    ModuleMetadata(std::wstring assembly_name,
                   std::vector<integration> integration_bases)
        : assemblyName(std::move(assembly_name)),
          m_Integrations(std::move(integration_bases))
    {
    }

    bool TryGetWrapperMemberRef(const std::wstring& keyIn, mdMemberRef& valueOut) const
    {
        const auto search = wrapper_refs.find(keyIn);

        if (search != wrapper_refs.end())
        {
            valueOut = search->second;
            return true;
        }

        return false;
    }

    bool TryGetWrapperParentTypeRef(const std::wstring& keyIn, mdTypeRef& valueOut) const
    {
        const auto search = wrapper_parent_type.find(keyIn);

        if (search != wrapper_parent_type.end())
        {
            valueOut = search->second;
            return true;
        }

        return false;
    }

    void SetWrapperMemberRef(const std::wstring& keyIn, const mdMemberRef valueIn)
    {
        wrapper_refs[keyIn] = valueIn;
    }

    void SetWrapperParentTypeRef(const std::wstring& keyIn, const mdTypeRef valueIn)
    {
        wrapper_parent_type[keyIn] = valueIn;
    }
};
