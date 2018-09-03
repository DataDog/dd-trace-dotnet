#pragma once

#include <codecvt>
#include <fstream>
#include <locale>
#include <optional>
#include <string>
#include <vector>

#include "Integration.h"
#include "Macros.h"
#include "json.hpp"

namespace trace {

const std::wstring kIntegrationsEnvironmentName = L"DATADOG_INTEGRATIONS";

using json = nlohmann::json;

// LoadIntegrationsFromEnvironment loads integrations from any files specified
// in the DATADOG_INTEGRATIONS environment variable
std::vector<integration> LoadIntegrationsFromEnvironment();
// LoadIntegrationsFromFile loads the integrations from a file
std::vector<integration> LoadIntegrationsFromFile(
    const std::wstring& file_path);
// LoadIntegrationsFromFile loads the integrations from a stream
std::vector<integration> LoadIntegrationsFromStream(std::istream& stream);

namespace {

std::optional<integration> IntegrationFromJson(const json::value_type& src);
std::optional<method_replacement> MethodReplacementFromJson(
    const json::value_type& src);
method_reference MethodReferenceFromJson(const json::value_type& src);

}  // namespace

}  // namespace trace
