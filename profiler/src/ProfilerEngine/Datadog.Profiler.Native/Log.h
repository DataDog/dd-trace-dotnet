// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "EnvironmentVariables.h"

#include <mutex>
#include <string>

#include "shared/src/native-src/logger.h"
#include "shared/src/native-src/logmanager.h"
#include "shared/src/native-src/string.h"

namespace ds = datadog::shared;

class Log final
{
private:
    struct ProfilerLoggerPolicy
    {
        inline static const std::string file_name = "DD-DotNet-Profiler-Native";
#ifdef _WIN32
        inline static const shared::WSTRING folder_path = WStr(R"(Datadog .NET Tracer\logs)");
#endif
        inline static const std::string pattern = "[%Y-%m-%d %H:%M:%S.%e | %l | PId: %P | TId: %t] %v";
        static bool enable_buffering() {
            // We don't buffer logs, as we know we are definitely instrumenting
            return false;
        }
        struct logging_environment
        {
            inline static const shared::WSTRING log_path = EnvironmentVariables::LogPath;
            inline static const shared::WSTRING log_directory = EnvironmentVariables::LogDirectory;
            inline static const shared::WSTRING deprecated_log_directory = EnvironmentVariables::DeprecatedLogDirectory;
        };
    };

    inline static ds::Logger* const Instance = ds::LogManager::Get<Log::ProfilerLoggerPolicy>();

public:
    static bool IsDebugEnabled()
    {
        return Instance->IsDebugEnabled();
    }

    static void EnableDebug()
    {
        Instance->EnableDebug(true);
    }

    template <typename... Args>
    static inline void Debug(const Args&... args)
    {
        Instance->Debug<Args...>(args...);
    }

    template <typename... Args>
    static void Info(const Args&... args)
    {
        Instance->Info<Args...>(args...);
    }

    template <typename... Args>
    static void Warn(const Args&... args)
    {
        Instance->Warn<Args...>(args...);
    }

    template <typename... Args>
    static void Error(const Args&... args)
    {
        Instance->Error<Args...>(args...);
    }
};

#define LogOnce(level, ...)                                                                                                \
    do                                                                                                                     \
    {                                                                                                                      \
        static std::once_flag UNIQUE_ONCE_FLAG_##__COUNTER__;                                                              \
        std::call_once(                                                                                                    \
            UNIQUE_ONCE_FLAG_##__COUNTER__, [](auto&&... args) { Log::level(std::forward<decltype(args)>(args)...); }, __VA_ARGS__); \
    } while (0) // NOLINT