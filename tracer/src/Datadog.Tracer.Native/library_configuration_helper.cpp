#include "library_configuration_helper.h"
#include "../../../shared/src/native-src/string.h"
#include "log.h"
#include "logger.h"
#include <cstdlib>
#include <datadog/common.h>
#include <datadog/library-config.h>
using shared::WSTRING;

namespace datadog::shared
{

// Implementation of the ReadFile method
void LibraryConfigurationHelper::ReadConfigurations()
{
    if (configs_loaded)
    {
        return;
    }

    ddog_CharSlice language = {.ptr = (char*) "dotnet", .len = 6};
    ddog_Configurator* configurator = ddog_library_configurator_new(true, language);

    if (!configurator)
    {
        // Logger::Warn("Failed to create ddog_Configurator");
        return;
    }

    std::vector<std::string> env_entries;
    std::vector<ddog_CharSlice> env_slices;

    for (int i = 0; i <= (int) DDOG_LIBRARY_CONFIG_NAME_DD_VERSION; ++i)
    {
        ddog_LibraryConfigName configName = (ddog_LibraryConfigName) i;
        auto env_var_name = ddog_library_config_name_to_env(configName);

        // Convert ddog_CharSlice to std::string
        std::string key(env_var_name.ptr, env_var_name.length);

        WSTRING wkey = ::shared::ToWSTRING(key);
        WSTRING value = ::shared::GetEnvironmentValue(wkey);
        if (value.empty())
        {
            continue;
        }

        std::string val = ::shared::ToString(value);
        std::string entry;
        entry.reserve(key.size() + 1 + val.size());
        entry.append(key);
        entry.append("=");
        entry.append(val);

        env_entries.push_back(entry);
        env_slices.push_back({env_entries.back().data(), env_entries.back().size()});
    }
   
    ddog_Slice_CharSlice env_slice = {env_slices.data(), env_slices.size()};

    // Create process info
    ddog_ProcessInfo process_info{
        .args = {nullptr, 0},
        .envp = env_slice,
        .language = language,
    };

    ddog_library_configurator_with_process_info(configurator, process_info);
    ddog_Result_VecLibraryConfig config_result = ddog_library_configurator_get(configurator);

    //// Clean up
    //free(envp);
    //ddog_library_configurator_drop(configurator);

    if (config_result.tag == DDOG_RESULT_VEC_LIBRARY_CONFIG_ERR_VEC_LIBRARY_CONFIG)
    {
        ddog_Error err = config_result.err;
        // Logger::Warn<std::string>("%.*s", (int) err.message.len, err.message.ptr);
    }
    else
    {
        cached_configs = config_result.ok;
    }
    configs_loaded = true;
    // ddog_library_configurator_drop(configurator);
}

ddog_Vec_LibraryConfig LibraryConfigurationHelper::GetConfigs()
{
    return cached_configs;
}

ddog_CStr LibraryConfigurationHelper::from_null_terminated(const char* str)
{
    return ddog_CStr{const_cast<char*>(str), strlen(str)};
}
} // namespace datadog::shared