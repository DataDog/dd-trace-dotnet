#ifndef DD_CLR_PROFILER_LOGGER_IMPL_H_
#define DD_CLR_PROFILER_LOGGER_IMPL_H_
#include "util.h"
#include "environment_variables.h"
#include "string.h"
#include "pal.h"

#include "spdlog/sinks/null_sink.h"
#include "spdlog/sinks/rotating_file_sink.h"

#ifndef _WIN32
typedef struct stat Stat;
#endif

#include <spdlog/spdlog.h>

#include <iostream>
#include <memory>
#include <filesystem>

namespace trace
{

template <typename TLoggerPolicy>
class LoggerImpl : public Singleton<LoggerImpl<TLoggerPolicy>>
{
    friend class Singleton<LoggerImpl<TLoggerPolicy>>;

private:
    std::shared_ptr<spdlog::logger> m_fileout;
    static std::string GetLogPath(const std::string& file_name_suffix);
    LoggerImpl();
    ~LoggerImpl();

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

    void Flush();

    void EnableDebug();
    bool IsDebugEnabled() const;

    static void Shutdown()
    {
        spdlog::shutdown();
    }

private:
    bool m_debug_logging_enabled;
};

#ifndef _WIN32
// for linux and osx we need a function to get the path from a filepath
inline std::string getPathName(const std::string& s)
{
    char sep = '/';
    size_t i = s.rfind(sep, s.length());
    if (i != std::string::npos)
    {
        return s.substr(0, i);
    }
    return "";
}
#endif

template <typename TLoggerPolicy>
std::string LoggerImpl<TLoggerPolicy>::GetLogPath(const std::string& file_name_suffix)
{
    const auto path = ToString(GetDatadogLogFilePath<TLoggerPolicy>(file_name_suffix));

#ifdef _WIN32
    // on VC++, use std::filesystem (C++ 17) to
    // create directory if missing
    const auto log_path = std::filesystem::path(path);

    if (log_path.has_parent_path())
    {
        const auto parent_path = log_path.parent_path();

        if (!std::filesystem::exists(parent_path))
        {
            std::filesystem::create_directories(parent_path);
        }
    }
#else
    // on linux and osx we use the basic C approach
    const auto log_path = getPathName(path);
    Stat st;
    if (log_path != "" && stat(log_path.c_str(), &st) != 0)
    {
        mkdir(log_path.c_str(), 0777);
    }
#endif

    return path;
}

template <typename TLoggerPolicy>
LoggerImpl<TLoggerPolicy>::LoggerImpl()
{
    m_debug_logging_enabled = false;

    spdlog::set_error_handler([](const std::string& msg) {
        // By writing into the stderr was changing the behavior in a CI scenario.
        // There's not a good way to report errors when trying to create the log file.
        // But we never should be changing the normal behavior of an app.
        // std::cerr << "LoggerImpl Handler: " << msg << std::endl;
    });

    spdlog::flush_every(std::chrono::seconds(3));

    static auto current_process_name = ToString(GetCurrentProcessName());
    static auto current_process_id = GetPID();
    static auto current_process_without_extension =
        current_process_name.substr(0, current_process_name.find_last_of("."));

    static auto file_name_suffix = "-" + current_process_without_extension + "-" + std::to_string(current_process_id);

    try
    {
        m_fileout = spdlog::rotating_logger_mt("Logger", GetLogPath(file_name_suffix), 1048576 * 5, 10);
    }
    catch (...)
    {
        std::cerr << "LoggerImpl Handler: Error creating native log file." << std::endl;
        m_fileout = spdlog::null_logger_mt("LoggerImpl");
    }

    m_fileout->set_level(spdlog::level::debug);

    m_fileout->set_pattern(TLoggerPolicy::pattern);

    m_fileout->flush_on(spdlog::level::info);
};

template <typename TLoggerPolicy>
LoggerImpl<TLoggerPolicy>::~LoggerImpl()
{
    m_fileout->flush();
    spdlog::shutdown();
};

template <class T>
void WriteToStream(std::ostringstream& oss, T const& x)
{
    if constexpr (std::is_same<T, WSTRING>::value)
    {
        oss << ToString(x);
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

template <typename TLoggerPolicy>
template <typename... Args>
void LoggerImpl<TLoggerPolicy>::Debug(const Args&... args)
{
    if (IsDebugEnabled())
    {
        m_fileout->debug(LogToString(args...));
    }
}

template <typename TLoggerPolicy>
template <typename... Args>
void LoggerImpl<TLoggerPolicy>::Info(const Args&... args)
{
    m_fileout->info(LogToString(args...));
}

template <typename TLoggerPolicy>
template <typename... Args>
void LoggerImpl<TLoggerPolicy>::Warn(const Args&... args)
{
    m_fileout->warn(LogToString(args...));
}

template <typename TLoggerPolicy>
template <typename... Args>
void LoggerImpl<TLoggerPolicy>::Error(const Args&... args)
{
    m_fileout->error(LogToString(args...));
}

template <typename TLoggerPolicy>
template< typename... Args>
void LoggerImpl<TLoggerPolicy>::Critical(const Args&... args)
{
    m_fileout->critical(LogToString(args...));
}

template <typename TLoggerPolicy>
void LoggerImpl<TLoggerPolicy>::Flush()
{
    m_fileout->flush();
}

template <typename TLoggerPolicy>
void LoggerImpl<TLoggerPolicy>::EnableDebug()
{
    m_debug_logging_enabled = true;
}

template <typename TLoggerPolicy>
bool LoggerImpl<TLoggerPolicy>::IsDebugEnabled() const
{
    return m_debug_logging_enabled;
}

} // namespace shared

#endif // DD_CLR_PROFILER_LOGGER_IMPL_H_
