#pragma once

#include <vector>
#include <corhlpr.h>
#include "IntegrationType.h"

struct method_replacement
{
    const mdMethodDef caller_method_token = mdMethodDefNil;
    const mdMethodDef target_method_token = mdMethodDefNil;
    const std::wstring wrapper_method_name;
    const std::vector<BYTE> wrapper_method_signature;

    method_replacement(const mdMethodDef caller_method_token,
                       const mdMethodDef target_method_token,
                       std::wstring wrapper_method_name,
                       std::vector<BYTE> wrapper_method_signature)
        : caller_method_token(caller_method_token),
          target_method_token(target_method_token),
          wrapper_method_name(std::move(wrapper_method_name)),
          wrapper_method_signature(std::move(wrapper_method_signature))
    {
    }
};

struct integration
{
    const IntegrationType integration_type;
    const std::wstring integration_name;
    const std::wstring target_assembly_name;
    const std::wstring wrapper_assembly_name;
    const std::wstring wrapper_type_name;
    std::vector<method_replacement> method_replacements;

    integration(const IntegrationType integration_type,
                std::wstring integration_name,
                std::wstring target_assembly_name,
                std::wstring wrapper_assembly_name,
                std::wstring wrapper_type_name,
                std::vector<method_replacement> method_replacements)
        : integration_type(integration_type),
          integration_name(std::move(integration_name)),
          target_assembly_name(std::move(target_assembly_name)),
          wrapper_assembly_name(std::move(wrapper_assembly_name)),
          wrapper_type_name(std::move(wrapper_type_name)),
          method_replacements(std::move(method_replacements))
    {
    }

    std::wstring get_wrapper_type_key() const
    {
        return L"[" + wrapper_assembly_name + L"]" + wrapper_type_name;
    }

    std::wstring get_wrapper_method_key(const method_replacement& method) const
    {
        return L"[" + wrapper_assembly_name + L"]" + wrapper_type_name + L"." + method.wrapper_method_name;
    }
};

extern const integration asp_net_mvc5_integration;

extern const std::vector<integration> all_integrations;
