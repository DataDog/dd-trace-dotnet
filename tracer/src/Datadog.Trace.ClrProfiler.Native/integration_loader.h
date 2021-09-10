#ifndef DD_CLR_PROFILER_INTEGRATION_LOADER_H_
#define DD_CLR_PROFILER_INTEGRATION_LOADER_H_

#include <fstream>
#include <locale>
#include <nlohmann/json.hpp>
#include <string>
#include <vector>

#include "integration.h"
#include "macros.h"

namespace trace
{

using json = nlohmann::json;

// LoadIntegrationsFromEnvironment loads integrations from any files specified
// in the DD_INTEGRATIONS environment variable
void LoadIntegrationsFromEnvironment(std::vector<IntegrationMethod>& integrationMethods, const bool isCallTargetEnabled,
                                     const bool isNetstandardEnabled,
                                     const std::vector<WSTRING>& disabledIntegrationNames);

// LoadIntegrationsFromFile loads the integrations from a file
void LoadIntegrationsFromFile(const WSTRING& file_path, std::vector<IntegrationMethod>& integrationMethods,
                              const bool isCallTargetEnabled, const bool isNetstandardEnabled,
                              const std::vector<WSTRING>& disabledIntegrationNames);

// LoadIntegrationsFromFile loads the integrations from a stream
void LoadIntegrationsFromStream(std::istream& stream, std::vector<IntegrationMethod>& integrationMethods,
                                const bool isCallTargetEnabled, const bool isNetstandardEnabled,
                                const std::vector<WSTRING>& disabledIntegrationNames);

namespace
{

    void IntegrationFromJson(const json::value_type& src, std::vector<IntegrationMethod>& integrationMethods,
                             const bool isCallTargetEnabled, const bool isNetstandardEnabled,
                             const std::vector<WSTRING>& disabledIntegrationNames);

    void MethodReplacementFromJson(const json::value_type& src, const WSTRING& integrationName,
                                   std::vector<IntegrationMethod>& integrationMethods,
                                   const bool isCallTargetEnabled, const bool isNetstandardEnabled);

    MethodReference MethodReferenceFromJson(const json::value_type& src, const bool is_target_method,
                                            const bool is_wrapper_method);

} // namespace

} // namespace trace

#endif
