#include "configuration.h"
#include "logger.h"
#include "log.h"
#include <datadog/common.h>
#include <datadog/library-config.h>
#include <cstdlib> 

namespace datadog::shared
{

// Implementation of the ReadFile method
void Configuration::ReadFile()
{
    ddog_CharSlice language = {.ptr = (char*) "dotnet", .len = 6};
    ddog_Configurator* configurator = ddog_library_configurator_new(true, language);

    if (!configurator)
    {
        // Logger::Warn("Failed to create ddog_Configurator");
        return;
    }

    std::string pathToAgent = "/etc/datadog-agent/";
    std::string pathToLocalFile = pathToAgent + "application_monitoring.yaml";
    std::string pathToManagedFile = pathToAgent + "managed/datadog-agent/stable/application_monitoring.yaml";

    ddog_library_configurator_with_local_path(configurator, from_null_terminated(pathToLocalFile.c_str()));
    ddog_library_configurator_with_fleet_path(configurator, from_null_terminated(pathToManagedFile.c_str()));
    ddog_ProcessInfo process_info{
        .args = {nullptr, 0},
        .envp = {nullptr, 0},
        .language = language,
    };

    ddog_Result_VecLibraryConfig config_result = ddog_library_configurator_get(configurator);

    if (config_result.tag == DDOG_RESULT_VEC_LIBRARY_CONFIG_ERR_VEC_LIBRARY_CONFIG)
    {
        ddog_Error err = config_result.err;
        // Logger::Warn<std::string>("%.*s", (int) err.message.len, err.message.ptr);
    }
    else
    {
        ddog_Vec_LibraryConfig configs = config_result.ok;
        configs_map.reserve(configs.len); // Access the public member configs_map

        for (int i = 0; i < configs.len; i++)
        {
            const ddog_LibraryConfig* cfg = &configs.ptr[i];
        }
        ddog_library_config_drop(configs);
    }
    ddog_library_configurator_drop(configurator);
    fileRead = true;
}

ddog_CStr Configuration::from_null_terminated(const char* str)
{
    return ddog_CStr{const_cast<char*>(str), strlen(str)};
}

std::string Configuration::GetValue(std::string const& varname)
{
    if (!fileRead)
    {
        ReadFile();
    }
    auto it = configs_map.find(varname);
    if (it != configs_map.end())
    {
        return it->second; // Directly access the value from the iterator
    }
    return "";
}
}