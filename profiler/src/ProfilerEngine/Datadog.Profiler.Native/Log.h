// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "EnvironmentVariables.h"

#include <string>

#include "shared/src/native-src/logging.h"
#include "shared/src/native-src/string.h"

class Log final
{
private:
    struct ProfilerLoggerPolicy
    {
        inline static const shared::WSTRING filename = WStr("DD-DotNet-Profiler-Native");
#ifdef _WIN32
        inline static const shared::WSTRING folder_path = WStr(R"(Datadog-APM\logs)");
#endif
        inline static const std::string pattern = "[%Y-%m-%d %H:%M:%S.%e | %l | PId: %P | TId: %t] %v";
        struct environment
        {
            inline static const shared::WSTRING log_path = EnvironmentVariables::LogPath;
            inline static const shared::WSTRING log_directory = EnvironmentVariables::LogDirectory;
        };
    };

public:
    static bool IsDebugEnabled()
    {
        return shared::Logger<ProfilerLoggerPolicy>::IsDebugEnabled();
    }

    static void EnableDebug()
    {
        shared::Logger<ProfilerLoggerPolicy>::EnableDebug();
    }

    template <typename... Args>
    static inline void Debug(const Args... args)
    {
        shared::Logger<ProfilerLoggerPolicy>::Debug<Args...>(args...);
    }

    template <typename... Args>
    static void Info(const Args... args)
    {
        shared::Logger<ProfilerLoggerPolicy>::Info<Args...>(args...);
    }

    template <typename... Args>
    static void Error(const Args... args)
    {
        shared::Logger<ProfilerLoggerPolicy>::Error<Args...>(args...);
    }
};
