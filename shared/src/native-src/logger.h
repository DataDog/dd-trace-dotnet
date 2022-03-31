#pragma once

#include <memory>
#include <regex>
#include <sstream>

#include <spdlog/sinks/null_sink.h>
#include <spdlog/sinks/rotating_file_sink.h>
#include <spdlog/spdlog.h>

#include "dd_filesystem.hpp"
#include "pal.h"
#include "string.h"

namespace datadog::shared
{

/// <summary>
/// Logger class is created only by LogManager class.
/// </summary>
class Logger
{
public:

    template <typename... Args>
    void Debug(const Args&... args);

    template <typename... Args>
    void Info(const Args&... args);

    template <typename... Args>
    void Warn(const Args&... args);

    template <typename... Args>
    void Error(const Args&... args);

    template <typename... Args>
    void Critical(const Args&... args);

    inline void Flush();

    inline void EnableDebug();
    inline bool IsDebugEnabled() const;


private:

    friend class LogManager;

    Logger(std::shared_ptr<spdlog::logger> const& logger) : _internalLogger{logger}, m_debug_logging_enabled{false}
    {
    }

    ~Logger()
    {
        _internalLogger->flush();
        spdlog::shutdown(); // <--- Not sure we still should do that since in the same process we could have Tracer Logger
    }

    template <class LoggerPolicy>
    static Logger Create();

    static inline std::string SanitizeProcessName(std::string const& processName);
    static inline std::string BuildLogFileSuffix();

    template <class LoggerPolicy>
    static std::string GetLogPath(const std::string& file_name_suffix);

    template <class LoggerPolicy>
    static std::shared_ptr<spdlog::logger> CreateInternalLogger();

    std::shared_ptr<spdlog::logger> _internalLogger;
    bool m_debug_logging_enabled;
};

template <class LoggerPolicy>
inline Logger Logger::Create()
{
    return {Logger::CreateInternalLogger<LoggerPolicy>()};
}

inline std::string Logger::SanitizeProcessName(std::string const& processName)
{
    const auto process_name_without_extension = processName.substr(0, processName.find_last_of("."));
    const std::regex dash_or_space_or_tab("-|\\s|\\t");
    return std::regex_replace(process_name_without_extension, dash_or_space_or_tab, "_");
}

inline std::string Logger::BuildLogFileSuffix()
{
    std::ostringstream oss;

    auto processName = Logger::SanitizeProcessName(::shared::ToString(::shared::GetCurrentProcessName()));
    oss << "-" << processName << "-" << ::shared::GetPID();
    return oss.str();
}

template <class LoggerPolicy>
std::shared_ptr<spdlog::logger> Logger::CreateInternalLogger()
{
    spdlog::set_error_handler([](const std::string& msg) {
        // By writing into the stderr was changing the behavior in a CI scenario.
        // There's not a good way to report errors when trying to create the log file.
        // But we never should be changing the normal behavior of an app.
        // std::cerr << "LoggerImpl Handler: " << msg << std::endl;
    });

    spdlog::flush_every(std::chrono::seconds(3));

    static auto file_name_suffix = Logger::BuildLogFileSuffix();

    std::shared_ptr<spdlog::logger> logger;

    try
    {
        logger =
            spdlog::rotating_logger_mt("Logger", Logger::GetLogPath<LoggerPolicy>(file_name_suffix), 1048576 * 5, 10);
    }
    catch (...)
    {
        // By writing into the stderr was changing the behavior in a CI scenario.
        // There's not a good way to report errors when trying to create the log file.
        // But we never should be changing the normal behavior of an app.
        // std::cerr << "LoggerImpl Handler: Error creating native log file." << std::endl;
        logger = spdlog::null_logger_mt("LoggerImpl");
    }

    logger->set_level(spdlog::level::debug);

    logger->set_pattern(LoggerPolicy::pattern);

    logger->flush_on(spdlog::level::info);

    return logger;
}

template <class TLoggerPolicy>
std::string Logger::GetLogPath(const std::string& file_name_suffix)
{
    auto path = ::shared::ToString(::shared::GetDatadogLogFilePath<TLoggerPolicy>(file_name_suffix));

    const auto log_path = fs::path(path);

    if (log_path.has_parent_path())
    {
        const auto parent_path = log_path.parent_path();

        if (!fs::exists(parent_path))
        {
            fs::create_directories(parent_path);
        }
    }

    return path;
}

template <class T>
void WriteToStream(std::ostringstream& oss, T const& x)
{
    if constexpr (std::is_same<T, ::shared::WSTRING>::value)
    {
        oss << ::shared::ToString(x);
    }
    else
    {
        oss << x;
    }
}

template <typename... Args>
static std::string LogToString(Args const&... args)
{
    std::ostringstream oss;
    (WriteToStream(oss, args), ...);

    return oss.str();
}

template <typename... Args>
void Logger::Debug(const Args&... args)
{
    if (IsDebugEnabled())
    {
        _internalLogger->debug(LogToString(args...));
    }
}

template <typename... Args>
void Logger::Info(const Args&... args)
{
    _internalLogger->info(LogToString(args...));
}

template <typename... Args>
void Logger::Warn(const Args&... args)
{
    _internalLogger->warn(LogToString(args...));
}

template <typename... Args>
void Logger::Error(const Args&... args)
{
    _internalLogger->error(LogToString(args...));
}

template <typename... Args>
void Logger::Critical(const Args&... args)
{
    _internalLogger->critical(LogToString(args...));
}

inline void Logger::Flush()
{
    _internalLogger->flush();
}

inline void Logger::EnableDebug()
{
    m_debug_logging_enabled = true;
}

inline bool Logger::IsDebugEnabled() const
{
    return m_debug_logging_enabled;
}
} // namespace datadog::shared