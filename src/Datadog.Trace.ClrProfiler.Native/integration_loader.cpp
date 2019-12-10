#include "integration_loader.h"

#include <exception>
#include <stdexcept>

#include "environment_variables.h"
#include "logging.h"
#include "util.h"

namespace trace {

using json = nlohmann::json;

std::vector<Integration> LoadIntegrationsFromEnvironment() {
  std::vector<Integration> integrations;
  WSTRING integrations_paths =
      GetEnvironmentValue(environment::integrations_path);
  
  // If DD_INTEGRATIONS is empty (the new default), use the
  // DD_DOTNET_TRACER_HOME variable to look for a fallback integrations.json
  if (integrations_paths.empty()) {
    auto profiler_home_path =
        GetEnvironmentValue(environment::profiler_home_path);

    if (!profiler_home_path.empty()) {
      integrations_paths = AppendToPath(profiler_home_path, "integrations.json"_W);
    }
  }

  for (const auto f : SplitAndTrim(integrations_paths)) {
    Debug("Loading integrations from file: ", f);
    auto is = LoadIntegrationsFromFile(f);
    for (auto& i : is) {
      integrations.push_back(i);
    }
  }
  return integrations;
}

std::vector<Integration> LoadIntegrationsFromFile(const WSTRING& file_path) {
  std::vector<Integration> integrations;

  try {
    std::ifstream stream;
    stream.open(ToString(file_path));

    if (static_cast<bool>(stream)) {
      integrations = LoadIntegrationsFromStream(stream);
    } else {
      Warn("Failed to load integrations from file ", file_path);
    }

    stream.close();
  } catch (...) {
    auto ex = std::current_exception();
    try {
      if (ex) {
        std::rethrow_exception(ex);
      }
    } catch (const std::exception& ex) {
      Warn("Failed to load integrations: ", ex.what());
    }
  }

  return integrations;
}

std::vector<Integration> LoadIntegrationsFromStream(std::istream& stream) {
  std::vector<Integration> integrations;

  try {
    json j;
    // parse the stream
    stream >> j;

    for (auto& el : j) {
      auto i = IntegrationFromJson(el);
      if (std::get<1>(i)) {
        integrations.push_back(std::get<0>(i));
      }
    }

    // Debug("Loaded integrations: ", j.dump());
  } catch (const json::parse_error& e) {
    Warn("Invalid integrations:", e.what());
  } catch (const json::type_error& e) {
    Warn("Invalid integrations:", e.what());
  } catch (...) {
    auto ex = std::current_exception();
    try {
      if (ex) {
        std::rethrow_exception(ex);
      }
    } catch (const std::exception& ex) {
      Warn("Failed to load integrations: ", ex.what());
    }
  }

  return integrations;
}

namespace {

std::pair<Integration, bool> IntegrationFromJson(const json::value_type& src) {
  if (!src.is_object()) {
    return std::make_pair<Integration, bool>({}, false);
  }

  // first get the name, which is required
  const auto name = ToWSTRING(src.value("name", ""));
  if (name.empty()) {
    Warn("Integration name is missing for integration: ", src.dump());
    return std::make_pair<Integration, bool>({}, false);
  }

  std::vector<MethodReplacement> replacements;
  auto arr = src.value("method_replacements", json::array());
  if (arr.is_array()) {
    for (auto& el : arr) {
      auto mr = MethodReplacementFromJson(el);
      if (std::get<1>(mr)) {
        replacements.push_back(std::get<0>(mr));
      }
    }
  }
  return std::make_pair<Integration, bool>({name, replacements}, true);
}

std::pair<MethodReplacement, bool> MethodReplacementFromJson(
    const json::value_type& src) {
  if (!src.is_object()) {
    return std::make_pair<MethodReplacement, bool>({}, false);
  }

  const auto caller =
      MethodReferenceFromJson(src.value("caller", json::object()), false);
  const auto target =
      MethodReferenceFromJson(src.value("target", json::object()), true);
  const auto wrapper =
      MethodReferenceFromJson(src.value("wrapper", json::object()), false);
  return std::make_pair<MethodReplacement, bool>({caller, target, wrapper},
                                                 true);
}

MethodReference MethodReferenceFromJson(const json::value_type& src,
                                        const bool is_target_method) {
  if (!src.is_object()) {
    return {};
  }

  const auto assembly = ToWSTRING(src.value("assembly", ""));
  const auto type = ToWSTRING(src.value("type", ""));
  const auto method = ToWSTRING(src.value("method", ""));
  auto raw_signature = src.value("signature", json::array());

  const auto eoj = src.end();
  USHORT min_major = 0;
  USHORT min_minor = 0;
  USHORT min_patch = 0;
  USHORT max_major = USHRT_MAX;
  USHORT max_minor = USHRT_MAX;
  USHORT max_patch = USHRT_MAX;
  std::vector<WSTRING> signature_type_array;

  if (is_target_method) {
    // these fields only exist in the target definition

    if (src.find("minimum_major") != eoj) {
      min_major = src["minimum_major"].get<USHORT>();
    }
    if (src.find("minimum_minor") != eoj) {
      min_minor = src["minimum_minor"].get<USHORT>();
    }
    if (src.find("minimum_patch") != eoj) {
      min_patch = src["minimum_patch"].get<USHORT>();
    }
    if (src.find("maximum_major") != eoj) {
      max_major = src["maximum_major"].get<USHORT>();
    }
    if (src.find("maximum_minor") != eoj) {
      max_minor = src["maximum_minor"].get<USHORT>();
    }
    if (src.find("maximum_patch") != eoj) {
      max_patch = src["maximum_patch"].get<USHORT>();
    }

    if (src.find("signature_types") != eoj) {
      // c++ is unable to handle null values in this array
      // we would need to write out own parsing here for null values
      auto sig_types = src["signature_types"].get<std::vector<std::string>>();
      signature_type_array = std::vector<WSTRING>(sig_types.size());
      for (auto i = sig_types.size() - 1; i < sig_types.size(); i--) {
        signature_type_array[i] = ToWSTRING(sig_types[i]);
      }
    }
  }

  std::vector<BYTE> signature;
  if (raw_signature.is_array()) {
    for (auto& el : raw_signature) {
      if (el.is_number_unsigned()) {
        signature.push_back(BYTE(el.get<BYTE>()));
      }
    }
  } else if (raw_signature.is_string()) {
    // load as a hex string
    std::string str = raw_signature;
    bool flip = false;
    char prev = 0;
    for (auto& c : str) {
      BYTE b = 0;
      if ('0' <= c && c <= '9') {
        b = c - '0';
      } else if ('a' <= c && c <= 'f') {
        b = c - 'a' + 10;
      } else if ('A' <= c && c <= 'F') {
        b = c - 'A' + 10;
      } else {
        // skip any non-hex character
        continue;
      }
      if (flip) {
        signature.push_back((prev << 4) + b);
      }
      flip = !flip;
      prev = b;
    }
  }
  return MethodReference(assembly, type, method,
                         Version(min_major, min_minor, min_patch, 0),
                         Version(max_major, max_minor, max_patch, USHRT_MAX),
                         signature, signature_type_array);
}

}  // namespace

}  // namespace trace
