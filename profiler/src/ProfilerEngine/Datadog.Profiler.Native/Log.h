// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "EnvironmentVariables.h"

#include <string>

#include "shared/src/native-src/logger_impl.h"
#include "shared/src/native-src/string.h"

class Log final
{
private:
    struct ProfilerLoggerPolicy
    {
        inline static const std::string file_name = "DD-DotNet-Profiler-Native";
#ifdef _WIN32
        inline static const shared::WSTRING folder_path = WStr(R"(Datadog-APM\logs\DotNet)");
#endif
        inline static const std::string pattern = "[%Y-%m-%d %H:%M:%S.%e | %l | PId: %P | TId: %t] %v";
        struct logging_environment
        {
            inline static const shared::WSTRING log_path = EnvironmentVariables::LogPath;
            inline static const shared::WSTRING log_directory = EnvironmentVariables::LogDirectory;
        };
    };

public:
    static bool IsDebugEnabled()
    {
        return shared::LoggerImpl<ProfilerLoggerPolicy>::Instance()->IsDebugEnabled();
    }

    static void EnableDebug()
    {
        shared::LoggerImpl<ProfilerLoggerPolicy>::Instance()->EnableDebug();
    }

    template <typename... Args>
    static inline void Debug(const Args... args)
    {
        shared::LoggerImpl<ProfilerLoggerPolicy>::Instance()->Debug<Args...>(args...);
    }

    template <typename... Args>
    static void Info(const Args... args)
    {
        shared::LoggerImpl<ProfilerLoggerPolicy>::Instance()->Info<Args...>(args...);
    }

    template <typename... Args>
    static void Error(const Args... args)
    {
        shared::LoggerImpl<ProfilerLoggerPolicy>::Instance()->Error<Args...>(args...);
    }
};
