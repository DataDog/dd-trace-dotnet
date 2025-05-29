#pragma once

#include <datadog/common.h>
#include <string>
#include <unordered_map>

namespace datadog::shared
{

class LibraryConfigurationHelper
{
private:
    static inline bool configs_loaded = false;
    ddog_CStr from_null_terminated(const char* str);
    ddog_Vec_LibraryConfig cached_configs{nullptr, 0, 0};

public:
    void ReadConfigurations();
    ddog_Vec_LibraryConfig GetConfigs();
};

} // namespace datadog::shared