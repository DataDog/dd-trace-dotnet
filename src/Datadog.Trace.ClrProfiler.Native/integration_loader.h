#ifndef DD_CLR_PROFILER_INTEGRATION_LOADER_H_
#define DD_CLR_PROFILER_INTEGRATION_LOADER_H_

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

using json = nlohmann::json;

class IntegrationLoader {
 public:
  std::vector<integration> LoadIntegrationsFromFile(
      const std::wstring& file_path);
  std::vector<integration> LoadIntegrationsFromStream(std::istream& stream);

 private:
  std::optional<integration> IntegrationFromJson(const json::value_type& src);
  std::optional<method_replacement> MethodReplacementFromJson(
      const json::value_type& src);
  method_reference MethodReferenceFromJson(const json::value_type& src);
};

}  // namespace trace

#endif