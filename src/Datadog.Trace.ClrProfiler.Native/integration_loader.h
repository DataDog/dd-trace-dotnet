#ifndef DD_CLR_PROFILER_INTEGRATION_LOADER_H_
#define DD_CLR_PROFILER_INTEGRATION_LOADER_H_

#include <codecvt>
#include <fstream>
#include <locale>
#include <optional>
#include <string>
#include <vector>

#include "integration.h"
#include "json.hpp"
#include "macros.h"

namespace trace {

const std::wstring kIntegrationsEnvironmentName = L"DD_INTEGRATIONS";

using json = nlohmann::json;

// LoadIntegrationsFromEnvironment loads integrations from any files specified
// in the DD_INTEGRATIONS environment variable
std::vector<Integration> LoadIntegrationsFromEnvironment();
// LoadIntegrationsFromFile loads the integrations from a file
std::vector<Integration> LoadIntegrationsFromFile(
    const std::wstring& file_path);
// LoadIntegrationsFromFile loads the integrations from a stream
std::vector<Integration> LoadIntegrationsFromStream(std::istream& stream);

namespace {

std::optional<Integration> IntegrationFromJson(const json::value_type& src);
std::optional<MethodReplacement> MethodReplacementFromJson(
    const json::value_type& src);
MethodReference MethodReferenceFromJson(const json::value_type& src);

}  // namespace

}  // namespace trace

#endif
