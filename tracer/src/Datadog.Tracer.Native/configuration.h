#pragma once

#include <datadog/common.h>
#include <datadog/library-config.h>
#include <string>
#include <unordered_map>

namespace datadog::shared
{

class Configuration
{
private:
    bool fileRead = false;                                    // Initialize fileRead
    std::unordered_map<std::string, std::string> configs_map; // Declare configs_map
    void ReadFile();
    ddog_CStr from_null_terminated(const char* str);

public:
    std::string GetValue(std::string const& varname);
};

} // namespace datadog::shared