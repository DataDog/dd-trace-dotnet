#pragma once

#include "environment_variables.h"

#include "../../../shared/src/native-src/logger.h"
#include "../../../shared/src/native-src/logmanager.h"
#include "../../../shared/src/native-src/string.h"

#include <string>

namespace ds = datadog::shared;

namespace trace
{

struct TracerLoggerPolicy
{
    inline static const std::string file_name = "dotnet-tracer-native";
#ifdef _WIN32
    inline static const shared::WSTRING folder_path = WStr(R"(Datadog .NET Tracer\logs)");
#endif
    inline static const std::string pattern = "%D %I:%M:%S.%e %p [%P|%t] [%l] %v";
    struct logging_environment
    {
        // cannot reuse environment::log_path variable. On alpine, test fails
        inline static const shared::WSTRING log_path = WStr("DD_TRACE_LOG_PATH");
        inline static const shared::WSTRING log_directory = WStr("DD_TRACE_LOG_DIRECTORY");
    };
};

class Logger
{
private:
    Logger() = delete;
    Logger(Logger&) = delete;
    Logger(Logger&&) = delete;
    Logger& operator=(Logger&) = delete;
    Logger& operator=(Logger&&) = delete;

    inline static ds::Logger* const Instance = ds::LogManager::Get<TracerLoggerPolicy>();

public:
    template <typename... Args>
    static void Debug(const Args&... args)
    {
        Instance->Debug(args...);
    }

    template <typename... Args>
    static void Info(const Args&... args)
    {
        Instance->Info(args...);
    }

    template <typename... Args>
    static void Warn(const Args&... args)
    {
        Instance->Warn(args...);
    }
    template <typename... Args>
    static void Error(const Args&... args)
    {
        Instance->Error(args...);
    }
    template <typename... Args>
    static void Critical(const Args&... args)
    {
        Instance->Critical(args...);
    }

    static void EnableDebug()
    {
        Instance->EnableDebug();
    }

    static bool IsDebugEnabled()
    {
        return Instance->IsDebugEnabled();
    }

    static void Flush()
    {
        Instance->Flush();
    }
};

} // namespace trace