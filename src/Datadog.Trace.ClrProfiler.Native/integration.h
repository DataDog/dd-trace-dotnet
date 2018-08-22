#pragma once

#include <corhlpr.h>
#include <vector>
#include "IntegrationType.h"

struct method_reference {
  const std::wstring assembly_name;
  const std::wstring type_name;
  const std::wstring method_name;
  const std::vector<BYTE> method_signature;

  method_reference() {}

  method_reference(std::wstring assembly_name, std::wstring type_name,
                   std::wstring method_name, std::vector<BYTE> method_signature)
      : assembly_name(std::move(assembly_name)),
        type_name(std::move(type_name)),
        method_name(std::move(method_name)),
        method_signature(std::move(method_signature)) {}

  std::wstring get_type_cache_key() const {
    return L"[" + assembly_name + L"]" + type_name;
  }

  std::wstring get_method_cache_key() const {
    return L"[" + assembly_name + L"]" + type_name + L"." + method_name;
  }
};

struct method_replacement {
  const method_reference caller_method;
  const method_reference target_method;
  const method_reference wrapper_method;

  method_replacement() {}

  method_replacement(method_reference caller_method,
                     method_reference target_method,
                     method_reference wrapper_method)
      : caller_method(std::move(caller_method)),
        target_method(std::move(target_method)),
        wrapper_method(std::move(wrapper_method)) {}
};

struct integration {
  const IntegrationType integration_type;
  const std::wstring integration_name;
  std::vector<method_replacement> method_replacements;

  integration() : integration_type(IntegrationType_Custom) {}

  integration(const IntegrationType integration_type,
              std::wstring integration_name,
              std::vector<method_replacement> method_replacements)
      : integration_type(integration_type),
        integration_name(std::move(integration_name)),
        method_replacements(std::move(method_replacements)) {}
};

extern std::vector<integration> default_integrations;