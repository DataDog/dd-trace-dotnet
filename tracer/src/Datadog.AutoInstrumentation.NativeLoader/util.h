#pragma once
#include <filesystem>

#include "pal.h"
#include "string.h"

using namespace datadog::shared::nativeloader;

const std::string conf_filename = "loader.conf";
const WSTRING cfg_filepath_env = WStr("DD_NATIVELOADER_CONFIGFILE");

static std::filesystem::path GetModuleFolderPath(HMODULE module)
{
    WCHAR moduleFilePath[MAX_PATH] = {0};
    if (GetModuleFileName(module, moduleFilePath, MAX_PATH) != 0)
    {
        return std::filesystem::path(moduleFilePath).remove_filename();
    }

    return std::filesystem::path();
}

// Gets the configuration file path
static std::filesystem::path GetConfigurationFilePath(std::filesystem::path&& moduleFolderPath)
{
    std::filesystem::path env_configfile = GetEnvironmentValue(cfg_filepath_env);
    if (!env_configfile.empty())
    {
        return env_configfile;
    }
    else
    {
        return moduleFolderPath.append(conf_filename);
    }
}
