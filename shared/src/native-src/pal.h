#pragma once

#ifdef _WIN32

#include "windows.h"
#include <process.h>

#else

#include <dlfcn.h>
#include <fstream>
#include <unistd.h>

#endif

#if MACOS
#include <libproc.h>
#endif

#include "dd_filesystem.hpp"
#include "../../../shared/src/native-src/string.h" // NOLINT
#include "../../../shared/src/native-src/util.h"
// namespace fs is an alias defined in "dd_filesystem.hpp"

namespace shared
{

template <class TLoggerPolicy>
inline shared::WSTRING GetDatadogLogFilePath(const std::string& file_name_suffix)
{
    const auto file_name = TLoggerPolicy::file_name + file_name_suffix + ".log";

    WSTRING directory = GetEnvironmentValue(TLoggerPolicy::logging_environment::log_directory);

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
    fs::path program_data_path;
    program_data_path = GetEnvironmentValue(WStr("PROGRAMDATA"));

    if (program_data_path.empty())
    {
        program_data_path = WStr(R"(C:\ProgramData)");
    }

    // on Windows WSTRING == wstring
    return (program_data_path / TLoggerPolicy::folder_path / file_name).wstring();
#else
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
    return fs::path(current_process_path).filename();
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

inline WSTRING GetCurrentModuleFileName()
{
    static WSTRING moduleFileName = EmptyWStr;
    if (moduleFileName != EmptyWStr)
    {
        // use cached version
        return moduleFileName;
    }

#ifdef _WIN32
    HMODULE hModule;
    if (GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT | GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS,
                           (LPCTSTR) GetCurrentModuleFileName, &hModule))
    {
        WCHAR lpFileName[1024];
        DWORD lpFileNameLength = GetModuleFileNameW(hModule, lpFileName, 1024);
        if (lpFileNameLength > 0)
        {
            moduleFileName = WSTRING(lpFileName, lpFileNameLength);
            return moduleFileName;
        }
    }
#else
    Dl_info info;
    if (dladdr((void*) GetCurrentModuleFileName, &info))
    {
        moduleFileName = ToWSTRING(ToString(info.dli_fname));
        return moduleFileName;
    }
#endif

    return EmptyWStr;
}

inline WSTRING GetProcessStartTime()
{
#if _WIN32
    FILETIME creationTime, exitTime, kernetlTime, userTime;
    if (GetProcessTimes(GetCurrentProcess(), &creationTime, &exitTime, &kernetlTime, &userTime))
    {
        SYSTEMTIME systemTime;
        if (FileTimeToSystemTime(&creationTime, &systemTime))
        {
            std::ostringstream ossMessage;

            ossMessage << std::setw(2) << std::setfill('0') << systemTime.wDay << "-" << std::setw(2)
                       << std::setfill('0') << systemTime.wMonth << "-" << systemTime.wYear << "_" << std::setw(2)
                       << std::setfill('0') << systemTime.wHour << "-" << std::setw(2) << std::setfill('0')
                       << systemTime.wMinute << "-" << std::setw(2) << std::setfill('0') << systemTime.wSecond;

            return ToWSTRING(ossMessage.str());
        }
    }
    return EmptyWStr;
#else
    return EmptyWStr;
#endif
}

} // namespace shared
