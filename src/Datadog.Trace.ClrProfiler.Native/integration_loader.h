#ifndef DD_CLR_PROFILER_INTEGRATION_LOADER_H_
#define DD_CLR_PROFILER_INTEGRATION_LOADER_H_

#include <cmath>

#include <codecvt>
#include <fstream>
#include <locale>
#include <nlohmann/json.hpp>
#include <string>
#include <vector>
#include <optional>

#include "integration.h"
#include "macros.h"

namespace trace {

const std::u16string kIntegrationsEnvironmentName = u"DD_INTEGRATIONS";

using json = nlohmann::json;

// LoadIntegrationsFromEnvironment loads integrations from any files specified
// in the DD_INTEGRATIONS environment variable
std::vector<Integration> LoadIntegrationsFromEnvironment();
// LoadIntegrationsFromFile loads the integrations from a file
std::vector<Integration> LoadIntegrationsFromFile(
    const std::u16string& file_path);
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
