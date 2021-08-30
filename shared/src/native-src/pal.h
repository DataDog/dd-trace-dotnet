#ifndef DD_CLR_PROFILER_PAL_H_
#define DD_CLR_PROFILER_PAL_H_

#ifdef _WIN32

#include <process.h>
#include <filesystem>
#include "windows.h"

#else

#include <unistd.h>
#include <fstream>

#endif

#if MACOS
#include <libproc.h>
#endif

#include "environment_variables.h"
#include "string.h"  // NOLINT
#include "util.h"

namespace shared {

    inline WSTRING DatadogLogFilePath() {
        WSTRING directory = GetEnvironmentValue(environment::log_directory);

        if (directory.length() > 0) {
            return directory +
#ifdef _WIN32
                WStr('\\') +
#else
                WStr('/') +
#endif
                WStr("dotnet-tracer-native.log");
        }

        WSTRING path = GetEnvironmentValue(environment::log_path);

        if (path.length() > 0) {
            return path;
        }

#ifdef _WIN32
        char* p_program_data;
        size_t length;
        const errno_t result = _dupenv_s(&p_program_data, &length, "PROGRAMDATA");
        std::string program_data;

        if (SUCCEEDED(result) && p_program_data != nullptr && length > 0) {
            program_data = std::string(p_program_data);
        }
        else {
            program_data = R"(C:\ProgramData)";
        }

        return ToWSTRING(program_data +
            R"(\Datadog .NET Tracer\logs\dotnet-tracer-native.log)");
#else
        return WStr("/var/log/datadog/dotnet/dotnet-tracer-native.log");
#endif
    }

    inline WSTRING GetCurrentProcessName() {
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

    inline int GetPID() {
#ifdef _WIN32
        return _getpid();
#else
        return getpid();
#endif
    }

} // namespace trace

#endif  // DD_CLR_PROFILER_PAL_H_
