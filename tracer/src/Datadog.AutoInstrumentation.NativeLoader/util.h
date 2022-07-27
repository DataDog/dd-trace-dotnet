#pragma once

#ifdef _WIN32
#include <windows.h>
EXTERN_C IMAGE_DOS_HEADER __ImageBase;
#define HINST_THISCOMPONENT ((HINSTANCE) &__ImageBase)
#else
#define _GNU_SOURCE
#include <dlfcn.h>
#endif

#include "../../../shared/src/native-src/dd_filesystem.hpp"
// namespace fs is an alias defined in "dd_filesystem.hpp"
#include "../../../shared/src/native-src/pal.h"
#include "../../../shared/src/native-src/string.h"

const std::string conf_filename = "loader.conf";
const ::shared::WSTRING cfg_filepath_env = WStr("DD_NATIVELOADER_CONFIGFILE");
const ::shared::WSTRING cfg_instrumentation_verification_env = WStr("DD_WRITE_INSTRUMENTATION_TO_DISK");
const ::shared::WSTRING cfg_log_directory_env = WStr("DD_TRACE_LOG_DIRECTORY");
inline static const ::shared::WSTRING datadog_logs_folder_path = WStr(R"(Datadog .NET Tracer\logs)");

static fs::path GetCurrentModuleFolderPath()
{
#ifdef _WIN32
    WCHAR moduleFilePath[MAX_PATH] = {0};
    if (::GetModuleFileName((HMODULE) HINST_THISCOMPONENT, moduleFilePath, MAX_PATH) != 0)
    {
        return fs::path(moduleFilePath).remove_filename();
    }
#else
    Dl_info info;
    if (dladdr((void*)GetCurrentModuleFolderPath, &info))
    {
        return fs::path(info.dli_fname).remove_filename();
    }
#endif
    return {};
}

static ::shared::WSTRING GetDatadogLogsDirectoryPath()
{
    ::shared::WSTRING directory = shared::GetEnvironmentValue(cfg_log_directory_env);

    if (directory.length() > 0)
    {
        return directory;
    }

#ifdef _WIN32
    fs::path program_data_path = shared::GetEnvironmentValue(WStr("PROGRAMDATA"));

    if (program_data_path.empty())
    {
        program_data_path = WStr(R"(C:\ProgramData)");
    }

    // on Windows WSTRING == wstring
    return (program_data_path / datadog_logs_folder_path).wstring();
#else
    return shared::ToWSTRING("/var/log/datadog/dotnet/");
#endif
}

// Gets the configuration file path
static fs::path GetConfigurationFilePath()
{
    fs::path env_configfile = shared::GetEnvironmentValue(cfg_filepath_env);
    if (!env_configfile.empty())
    {
        return env_configfile;
    }
    else
    {
        return GetCurrentModuleFolderPath() / conf_filename;
    }
}

inline bool IsRunningOnIIS()
{
    const auto& process_name = ::shared::GetCurrentProcessName();
    return process_name == WStr("w3wp.exe") || process_name == WStr("iisexpress.exe");
}
