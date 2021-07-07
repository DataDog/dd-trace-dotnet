#pragma once
#include <filesystem>

#include "pal.h"
#include "string.h"

using namespace datadog::nativeloader;

const std::string conf_filename = "loader.conf";
#if _WIN32
const std::string dynExtension = ".dll";
#elif LINUX
const std::string dynExtension = ".so";
#elif MACOS
const std::string dynExtension = ".dylib";
#endif

// Gets the profiler path
static WSTRING GetProfilerPath()
{
    WSTRING profiler_path;
    profiler_path = GetEnvironmentValue(WStr("CORECLR_PROFILER_PATH"));
    if (profiler_path.length() == 0)
    {
        profiler_path = GetEnvironmentValue(WStr("COR_PROFILER_PATH"));
    }

#if BIT64
    if (profiler_path.length() == 0)
    {
        profiler_path = GetEnvironmentValue(WStr("CORECLR_PROFILER_PATH_64"));
    }
    if (profiler_path.length() == 0)
    {
        profiler_path = GetEnvironmentValue(WStr("COR_PROFILER_PATH_64"));
    }
#else
    if (profiler_path.length() == 0)
    {
        profiler_path = GetEnvironmentValue(WStr("CORECLR_PROFILER_PATH_32"));
    }
    if (profiler_path.length() == 0)
    {
        profiler_path = GetEnvironmentValue(WStr("COR_PROFILER_PATH_32"));
    }
#endif

    return profiler_path;
}

// Gets the configuration file path
static std::string GetConfigurationFilePath()
{
    std::string profilerFilePath = ToString(GetProfilerPath());
    std::filesystem::path profilerPath = std::filesystem::path(profilerFilePath).remove_filename();
    return profilerPath.append(conf_filename).string();
}