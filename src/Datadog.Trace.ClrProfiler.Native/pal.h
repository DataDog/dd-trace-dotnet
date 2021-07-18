#ifndef DD_CLR_PROFILER_PAL_H_
#define DD_CLR_PROFILER_PAL_H_

#ifdef _WIN32

#include "windows.h"
#include <filesystem>
#include <process.h>

#else

#include <fstream>
#include <unistd.h>

#endif

#if MACOS
#include <libproc.h>
#endif

#include "environment_variables.h"
#include "string.h" // NOLINT
#include "util.h"

namespace trace
{

template <class TLoggerPolicy>
inline WSTRING GetDatadogLogFilePath(const std::string& file_name)
{
    WSTRING directory = GetEnvironmentValue(environment::log_directory);

    if (directory.length() > 0)
    {
        return directory +
#ifdef _WIN32
               WStr('\\') +
#else
               WStr('/') +
#endif
               ToWSTRING(file_name);
    }

    WSTRING path = GetEnvironmentValue(TLoggerPolicy::logging_environment::log_path);

    if (path.length() > 0)
    {
        return path;
    }

#ifdef _WIN32
    WSTRING program_data = GetEnvironmentValue(WStr("PROGRAMDATA"));
    std::filesystem::path program_data_path;
    if (program_data.length() == 0)
    {
        program_data_path = WStr(R"(C:\ProgramData)");
    }

    // on Windows WSTRING == wstring
    return (program_data_path / TLoggerPolicy::product_folder_name / WStr("logs") / file_name).wstring();
#else
    // TODO: should we consider having a logs folder on non-Windows ?
    return ToWSTRING("/var/log/datadog/dotnet/" + file_name);
#endif
}

inline WSTRING GetCurrentProcessName()
{
#ifdef _WIN32
    const DWORD length = 260;
    WCHAR buffer[length]{};

    const DWORD len = GetModuleFileName(nullptr, buffer, length);
    const WSTRING current_process_path(buffer);
    return std::filesystem::path(current_process_path).filename();
#elif MACOS
    const int length = 260;
    char* buffer = new char[length];
    proc_name(getpid(), buffer, length);
    return ToWSTRING(std::string(buffer));
#else
    std::fstream comm("/proc/self/comm");
    std::string name;
    std::getline(comm, name);
    return ToWSTRING(name);
#endif
}

inline int GetPID()
{
#ifdef _WIN32
    return _getpid();
#else
    return getpid();
#endif
}

} // namespace trace

#endif // DD_CLR_PROFILER_PAL_H_
