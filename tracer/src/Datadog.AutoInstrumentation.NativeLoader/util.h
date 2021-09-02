#pragma once
#include <filesystem>

#include "pal.h"
#include "string.h"

using namespace datadog::shared::nativeloader;

const std::string conf_filename = "loader.conf";
const WSTRING cfg_filepath_env = WStr("DD_NATIVELOADER_CONFIGFILE");

// Gets the profiler path
static WSTRING GetProfilerPath()
{
    WSTRING profiler_path;

    //
    // There's no particular reason for the order of the environment variables.
    // The only requirement was in the architecture, from the specific bitness
    // to the generic one.
    //
    // For the other cases we follow the convention from the latest key format (CORECLR)
    // to the oldest one (COR).
    //

#if BIT64
    profiler_path = GetEnvironmentValue(WStr("CORECLR_PROFILER_PATH_64"));
    if (profiler_path.length() > 0)
    {
        Info("GetProfilerPath: CORECLR_PROFILER_PATH_64 = ", profiler_path);
    }
    else
    {
        profiler_path = GetEnvironmentValue(WStr("COR_PROFILER_PATH_64"));
        if (profiler_path.length() > 0)
        {
            Info("GetProfilerPath: COR_PROFILER_PATH_64 = ", profiler_path);
        }
    }
#else
    profiler_path = GetEnvironmentValue(WStr("CORECLR_PROFILER_PATH_32"));
    if (profiler_path.length() > 0)
    {
        Info("GetProfilerPath: CORECLR_PROFILER_PATH_32 = ", profiler_path);
    }
    else
    {
        profiler_path = GetEnvironmentValue(WStr("COR_PROFILER_PATH_32"));
        if (profiler_path.length() > 0)
        {
            Info("GetProfilerPath: COR_PROFILER_PATH_32 = ", profiler_path);
        }
    }
#endif

    if (profiler_path.length() == 0)
    {
        profiler_path = GetEnvironmentValue(WStr("CORECLR_PROFILER_PATH"));
        if (profiler_path.length() > 0)
        {
            Info("GetProfilerPath: CORECLR_PROFILER_PATH = ", profiler_path);
        }
    }
    if (profiler_path.length() == 0)
    {
        profiler_path = GetEnvironmentValue(WStr("COR_PROFILER_PATH"));
        if (profiler_path.length() > 0)
        {
            Info("GetProfilerPath: COR_PROFILER_PATH = ", profiler_path);
        }
    }

    if (profiler_path.length() == 0)
    {
        Warn("GetProfilerPath: The profiler path cannot be found.");
    }

    return profiler_path;
}

// Gets the configuration file path
static std::string GetConfigurationFilePath()
{
    WSTRING env_configfile = GetEnvironmentValue(cfg_filepath_env);
    if (!env_configfile.empty())
    {
        return ToString(env_configfile);
    }
    else
    {
        std::string profilerFilePath = ToString(GetProfilerPath());
        std::filesystem::path profilerPath = std::filesystem::path(profilerFilePath).remove_filename();
        return profilerPath.append(conf_filename).string();
    }
}
