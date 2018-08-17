#pragma once

#include <codecvt>
#include <fstream>
#include <locale>
#include <string>
#include <vector>

#include "Integration.h"
#include "Macros.h"
#include "json.hpp"

using json = nlohmann::json;

class IntegrationLoader
{
public:
    static std::vector<integration> load_integrations_from_file(const std::wstring& file_path);
    static std::vector<integration> load_integrations_from_stream(std::istream& stream);

private:
    static std::pair<integration, bool> integration_from_json(const json::value_type& src);
    static std::pair<method_replacement, bool> method_replacement_from_json(const json::value_type& src);
    static method_reference method_reference_from_json(const json::value_type& src);
};
