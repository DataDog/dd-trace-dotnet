#pragma once

#include "environment_variables.h"
#include "string.h"
#include "logger_impl.h"

#include <string>


namespace trace
{

struct TracerLoggerPolicy
{
    inline static const std::string file_name = "dotnet-tracer-native";
#ifdef _WIN32
    // this field will be removed once merged with the profiler in order to have
    // the same product folder name
    inline static const WSTRING product_folder_name = WStr("Datadog .NET Tracer");
#endif
    inline static const std::string pattern = "%D %I:%M:%S.%e %p [%P|%t] [%l] %v";
    struct logging_environment
    {
        inline static const WSTRING log_path = environment::log_path;
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

public:
    template <typename... Args>
    static void Debug(const Args&... args)
    {
        LoggerImpl<TracerLoggerPolicy>::Instance()->Debug(args...);
    }

    template <typename... Args>
    static void Info(const Args&... args)
    {
        LoggerImpl<TracerLoggerPolicy>::Instance()->Info(args...);
    }

    template <typename... Args>
    static void Warn(const Args&... args)
    {
        LoggerImpl<TracerLoggerPolicy>::Instance()->Warn(args...);
    }
    template <typename... Args>
    static void Error(const Args&... args)
    {
        LoggerImpl<TracerLoggerPolicy>::Instance()->Error(args...);
    }
    template <typename... Args>
    static void Critical(const Args&... args)
    {
        LoggerImpl<TracerLoggerPolicy>::Instance()->Critical(args...);
    }

    static void EnableDebug()
    {
        LoggerImpl<TracerLoggerPolicy>::Instance()->EnableDebug();
    }

    static bool IsDebugEnabled()
    {
        return LoggerImpl<TracerLoggerPolicy>::Instance()->IsDebugEnabled();
    }

    static void Shutdown()
    {
        LoggerImpl<TracerLoggerPolicy>::Shutdown();
    }

    static void Flush()
    {
        LoggerImpl<TracerLoggerPolicy>::Instance()->Flush();
    }
};

} // namespace trace