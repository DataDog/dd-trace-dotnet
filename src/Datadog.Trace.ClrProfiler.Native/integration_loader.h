#ifndef DD_CLR_PROFILER_INTEGRATION_LOADER_H_
#define DD_CLR_PROFILER_INTEGRATION_LOADER_H_

#include <fstream>
#include <locale>
#include <nlohmann/json.hpp>
#include <string>
#include <vector>

#include "integration.h"
#include "macros.h"

namespace trace {

using json = nlohmann::json;

// LoadIntegrationsFromEnvironment loads integrations from any files specified
// in the DD_INTEGRATIONS environment variable
std::vector<Integration> LoadIntegrationsFromEnvironment();
// LoadIntegrationsFromFile loads the integrations from a file
std::vector<Integration> LoadIntegrationsFromFile(const WSTRING& file_path);
// LoadIntegrationsFromFile loads the integrations from a stream
std::vector<Integration> LoadIntegrationsFromStream(std::istream& stream);

namespace {

std::pair<Integration, bool> IntegrationFromJson(const json::value_type& src);
std::pair<MethodReplacement, bool> MethodReplacementFromJson(
    const json::value_type& src);
MethodReference MethodReferenceFromJson(const json::value_type& src);

}  // namespace

}  // namespace trace

#endif
