#include "integration_loader.h"
#include "util.h"

namespace trace {

using json = nlohmann::json;

std::vector<Integration> LoadIntegrationsFromEnvironment() {
  std::vector<Integration> integrations;
  for (const auto& f : GetEnvironmentValues(kIntegrationsEnvironmentName)) {
    LOG_APPEND(L"loading integrations from " << f);
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
    LOG_APPEND(L"failed to load integrations");
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

std::optional<Integration> IntegrationFromJson(const json::value_type& src) {
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

  std::vector<MethodReplacement> replacements;
  auto arr = src.value("method_replacements", json::array());
  if (arr.is_array()) {
    for (auto& el : arr) {
      auto mr = MethodReplacementFromJson(el);
      if (mr.has_value()) {
        replacements.push_back(mr.value());
      }
    }
  }
  return Integration(name, replacements);
}

std::optional<MethodReplacement> MethodReplacementFromJson(
    const json::value_type& src) {
  if (!src.is_object()) {
    return {};
  }

  auto caller = MethodReferenceFromJson(src.value("caller", json::object()));
  auto target = MethodReferenceFromJson(src.value("target", json::object()));
  auto wrapper = MethodReferenceFromJson(src.value("wrapper", json::object()));
  return MethodReplacement(caller, target, wrapper);
}

MethodReference MethodReferenceFromJson(const json::value_type& src) {
  if (!src.is_object()) {
    return {};
  }

  std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
  std::wstring assembly = converter.from_bytes(src.value("assembly", ""));
  std::wstring type = converter.from_bytes(src.value("type", ""));
  std::wstring method = converter.from_bytes(src.value("method", ""));
  auto arr = src.value("signature", json::array());
  std::vector<BYTE> signature;
  if (arr.is_array()) {
    for (auto& el : arr) {
      if (el.is_number_unsigned()) {
        signature.push_back(BYTE(el.get<BYTE>()));
      }
    }
  }
  return MethodReference(assembly, type, method, signature);
}

}  // namespace

}  // namespace trace
