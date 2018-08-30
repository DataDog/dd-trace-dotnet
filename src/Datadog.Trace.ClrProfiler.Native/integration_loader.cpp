#include "integration_loader.h"
#include "util.h"

namespace trace {

using json = nlohmann::json;

std::vector<integration> LoadIntegrationsFromEnvironment() {
  std::vector<integration> integrations;
  for (const auto& f : GetEnvironmentValues(kIntegrationsEnvironmentName)) {
    LOG_APPEND(L"loading integrations from " << f);
    auto is = LoadIntegrationsFromFile(f);
    for (auto& i : is) {
      integrations.push_back(i);
    }
  }
  return integrations;
}

std::vector<integration> LoadIntegrationsFromFile(
    const std::wstring& file_path) {
  std::vector<integration> integrations;

  try {
    std::ifstream stream;
    stream.open(file_path);
    integrations = LoadIntegrationsFromStream(stream);
    stream.close();
  } catch (...) {
    LOG_APPEND(L"failed to load integrations");
  }

  return integrations;
}

std::vector<integration> LoadIntegrationsFromStream(std::istream& stream) {
  std::vector<integration> integrations;

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

    LOG_APPEND(L"loaded integrations: " << j.dump().c_str());
  } catch (const json::parse_error& e) {
    LOG_APPEND(L"invalid integrations: " << e.what());
  } catch (const json::type_error& e) {
    LOG_APPEND(L"invalid integrations: " << e.what());
  } catch (...) {
    LOG_APPEND(L"failed to load integrations");
  }

  return integrations;
}

namespace {

std::optional<integration> IntegrationFromJson(const json::value_type& src) {
  if (!src.is_object()) {
    return {};
  }

  // first get the name, which is required
  std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
  std::wstring name = converter.from_bytes(src.value("name", ""));
  if (name.empty()) {
    LOG_APPEND(L"integration name is missing for integration: "
               << src.dump().c_str());
    return {};
  }

  std::vector<method_replacement> replacements;
  auto arr = src.value("method_replacements", json::array());
  if (arr.is_array()) {
    for (auto& el : arr) {
      auto mr = MethodReplacementFromJson(el);
      if (mr.has_value()) {
        replacements.push_back(mr.value());
      }
    }
  }
  return integration(IntegrationType_Custom, name, replacements);
}

std::optional<method_replacement> MethodReplacementFromJson(
    const json::value_type& src) {
  if (!src.is_object()) {
    return {};
  }

  auto caller = MethodReferenceFromJson(src.value("caller", json::object()));
  auto target = MethodReferenceFromJson(src.value("target", json::object()));
  auto wrapper = MethodReferenceFromJson(src.value("wrapper", json::object()));
  return method_replacement(caller, target, wrapper);
}

method_reference MethodReferenceFromJson(const json::value_type& src) {
  if (!src.is_object()) {
    return {};
  }

  std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
  std::wstring assembly = converter.from_bytes(src.value("assembly", ""));
  std::wstring type = converter.from_bytes(src.value("type", ""));
  std::wstring method = converter.from_bytes(src.value("method", ""));
  auto arr = src.value("signature", json::array());
  std::vector<uint8_t> signature;
  if (arr.is_array()) {
    for (auto& el : arr) {
      if (el.is_number_unsigned()) {
        signature.push_back(uint8_t(el.get<uint64_t>()));
      }
    }
  }
  return method_reference(assembly, type, method, signature);
}

}  // namespace

}  // namespace trace