#include "integration_loader.h"
#include "logging.h"
#include "util.h"

namespace trace {

using json = nlohmann::json;

std::vector<Integration> LoadIntegrationsFromEnvironment() {
  std::vector<Integration> integrations;
  for (const auto& f : GetEnvironmentValues(kIntegrationsEnvironmentName)) {
    GetLogger()->info("loading integrations from {}", f);
    auto is = LoadIntegrationsFromFile(f);
    for (auto& i : is) {
      integrations.push_back(i);
    }
  }
  return integrations;
}

std::vector<Integration> LoadIntegrationsFromFile(
    const std::wstring& file_path) {
  std::vector<Integration> integrations;

  try {
    std::ifstream stream;
    stream.open(file_path);
    integrations = LoadIntegrationsFromStream(stream);
    stream.close();
  } catch (...) {
    GetLogger()->error("failed to load integrations");
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
      if (i.has_value()) {
        integrations.push_back(i.value());
      }
    }

    GetLogger()->info("loaded integrations: {}", j.dump());
  } catch (const json::parse_error& e) {
    GetLogger()->error("invalid integrations: {}", e.what());
  } catch (const json::type_error& e) {
    GetLogger()->error("invalid integrations: {}", e.what());
  } catch (...) {
    GetLogger()->error("failed to load integrations");
  }

  return integrations;
}

namespace {

std::pair<Integration, bool> IntegrationFromJson(const json::value_type& src) {
  if (!src.is_object()) {
    return make_pair<Integration, bool>({}, false);
  }

  // first get the name, which is required
  std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
  std::wstring name = converter.from_bytes(src.value("name", ""));
  if (name.empty()) {
    GetLogger()->error("integration name is missing for integration: {}",
                       src.dump());
    return make_pair<Integration, bool>({}, false);
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
  return make_pair<Integration, bool>({name, replacements}, true);
}

std::pair<MethodReplacement, bool> MethodReplacementFromJson(
    const json::value_type& src) {
  if (!src.is_object()) {
    return make_pair<MethodReplacement, bool>({}, false);
  }

  auto caller = MethodReferenceFromJson(src.value("caller", json::object()));
  auto target = MethodReferenceFromJson(src.value("target", json::object()));
  auto wrapper = MethodReferenceFromJson(src.value("wrapper", json::object()));
  return make_pair<MethodReplacement, bool>({caller, target, wrapper}, true);
}

MethodReference MethodReferenceFromJson(const json::value_type& src) {
  if (!src.is_object()) {
    return {};
  }

  std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
  std::wstring assembly = converter.from_bytes(src.value("assembly", ""));
  std::wstring type = converter.from_bytes(src.value("type", ""));
  std::wstring method = converter.from_bytes(src.value("method", ""));
  auto raw_signature = src.value("signature", json::array());
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
  return MethodReference(assembly, type, method, signature);
}

}  // namespace

}  // namespace trace
