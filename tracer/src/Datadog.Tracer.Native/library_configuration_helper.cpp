#include "library_configuration_helper.h"
#include "log.h"
#include "logger.h"
#include <cstdlib>
#include <datadog/common.h>
#include <datadog/library-config.h>

namespace datadog::shared
{

// Implementation of the ReadFile method
void LibraryConfigurationHelper::ReadConfigurations()
{
//#ifndef LINUX
//    configs_loaded = true;
//    return;
//#endif

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

    ddog_Result_VecLibraryConfig config_result = ddog_library_configurator_get(configurator);

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
    ddog_library_configurator_drop(configurator);
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