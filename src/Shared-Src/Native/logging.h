#ifndef DD_CLR_PROFILER_LOGGING_H_
#define DD_CLR_PROFILER_LOGGING_H_

#include "string.h"
#include "util.h"
#include "pal.h"

#include <spdlog/spdlog.h>

#include "spdlog/sinks/null_sink.h"
#include "spdlog/sinks/rotating_file_sink.h"

#include <iostream>
#include <memory>
#include <string>
#include <regex>

#if __has_include(<filesystem>)
#include <filesystem>
namespace fs = std::filesystem;
#elif __has_include(<experimental/filesystem>)
#include <experimental/filesystem>
namespace fs = std::experimental::filesystem;
#else
error "Missing the <filesystem> header."
#endif

namespace shared {

    extern bool dump_il_rewrite_enabled;

    template <class T>
    void WriteToStream(std::ostringstream& oss, T const& x)
    {
        if constexpr (std::is_same<T, shared::WSTRING>::value)
        {
            oss << ToString(x);
        }
        else
        {
            oss << x;
        }
    }

    template <typename...Args>
    static std::string LogToString(Args const&... args)
    {
        std::ostringstream oss;
        (WriteToStream(oss, args), ...);

        return oss.str();
    }


    template<class TLoggerPolicy>
    class Logger : public Singleton<Logger<TLoggerPolicy>>
    {
            friend class Singleton<Logger>;


        public:
            template <typename... Args>
            static void Debug(const Args... args) { if (Logger<TLoggerPolicy>::IsDebugEnabled()) Logger<TLoggerPolicy>::Instance()->Debug(LogToString(args...)); }
            template <typename... Args>
            static void Info(const Args... args) { Logger<TLoggerPolicy>::Instance()->Info(LogToString(args...)); }
            template <typename... Args>
            static void Warn(const Args... args) { Logger<TLoggerPolicy>::Instance()->Warn(LogToString(args...)); }
            template <typename... Args>
            static void Error(const Args... args) { Logger<TLoggerPolicy>::Instance()->Error(LogToString(args...)); }
            template <typename... Args>
            static void Critical(const Args... args) { Logger<TLoggerPolicy>::Instance()->Critical(LogToString(args...)); }

            static void EnableDebug() { Logger<TLoggerPolicy>::Instance()->EnableDebugLog(); }
            static bool IsDebugEnabled() { return Logger<TLoggerPolicy>::Instance()->IsDebugLogEnabled(); }

        private:
            Logger();
            ~Logger();
            static std::string DatadogLogFilePath(const std::string& file_name_suffix);
            static std::string GetLogPath(const std::string& file_name_suffix);

            static void Shutdown() { spdlog::shutdown(); }

            void Debug(const std::string& str) { if (m_debug_logging_enabled) { m_fileout->debug(str); } };
            void Info(const std::string& str) { m_fileout->info(str); };
            void Warn(const std::string& str) { m_fileout->warn(str); };
            void Error(const std::string& str) { m_fileout->error(str); };
            void Critical(const std::string& str) { m_fileout->critical(str); };
            void Flush() { m_fileout->flush(); }
            void EnableDebugLog() { m_debug_logging_enabled = true; }
            bool IsDebugLogEnabled() { return m_debug_logging_enabled; }

            std::shared_ptr<spdlog::logger> m_fileout;
            bool m_debug_logging_enabled;
    };


    static std::string SanitizeProcessName(std::string const& processName)
    {
        const auto process_name_without_extension = processName.substr(0, processName.find_last_of("."));
        const std::regex dash_or_space_or_tab("-|\\s|\\t");
        return std::regex_replace(process_name_without_extension, dash_or_space_or_tab, "_");
    }

    template <class TLoggerPolicy>
    Logger<TLoggerPolicy>::Logger()
    {
        m_debug_logging_enabled = false;

        spdlog::set_error_handler([](const std::string& msg) {
            // By writing into the stderr was changing the behavior in a CI scenario.
            // There's not a good way to report errors when trying to create the log file.
            // But we never should be changing the normal behavior of an app.
            });

        spdlog::flush_every(std::chrono::seconds(3));

        static auto current_process_name = ToString(GetCurrentProcessName());
        static auto current_process_name_sanitized = SanitizeProcessName(current_process_name);

        static auto file_name_suffix = "-" + current_process_name_sanitized;

        try {
            m_fileout = spdlog::rotating_logger_mt("Logger", GetLogPath(file_name_suffix), 1048576 * 5, 10);
        }
        catch (...) {
            std::cerr << "Logger Handler: Error creating native log file." << std::endl;
            m_fileout = spdlog::null_logger_mt("Logger");
        }

        m_fileout->set_level(spdlog::level::debug);

        m_fileout->set_pattern(TLoggerPolicy::pattern); //

        m_fileout->flush_on(spdlog::level::info);
    };

    template <class TLoggerPolicy>
    Logger<TLoggerPolicy>::~Logger()
    {
        m_fileout->flush();
        spdlog::shutdown();
    }

    static fs::path GetProductBaseDirectoryPath()
    {
#ifdef _WIN32
        char* p_program_data;
        size_t length;
        const errno_t result = _dupenv_s(&p_program_data, &length, "PROGRAMDATA");
        std::string program_data;

        if (SUCCEEDED(result) && p_program_data != nullptr && length > 0)
        {
            program_data = std::string(p_program_data);
        }
        else
        {
            program_data = R"(C:\ProgramData)";
        }
        
        return fs::path(program_data) / R"(Datadog-APM\logs\DotNet)";
#else
        return fs::path("/var/log/datadog/dotnet");
#endif
    }

    template <class TLoggerPolicy>
    std::string Logger<TLoggerPolicy>::DatadogLogFilePath(const std::string& file_name_suffix) {
        WSTRING directory = GetEnvironmentValue(TLoggerPolicy::environment::log_directory);

        WSTRING log_file_name = TLoggerPolicy::filename + ToWSTRING(file_name_suffix) + ToWSTRING(".log");

        if (directory.length() > 0) {
            auto directory_path = fs::path(directory);
            return ToString((directory_path / log_file_name).native());
        }

        WSTRING path = GetEnvironmentValue(TLoggerPolicy::environment::log_path);

        if (path.length() > 0) {
            return ToString(path);
        }

        fs::path log_directory_path = GetProductBaseDirectoryPath();
        return ToString((log_directory_path / log_file_name).native());
    }

    template <class TLoggerPolicy>
    std::string Logger<TLoggerPolicy>::GetLogPath(const std::string& file_name_suffix) {
        auto path = DatadogLogFilePath(file_name_suffix);

        const auto log_path = fs::path(path);

        if (log_path.has_parent_path()) {
            const auto parent_path = log_path.parent_path();

            if (!fs::exists(parent_path)) {
                fs::create_directories(parent_path);
            }
        }

        return path;
    }
}  // namespace trace

#endif  // DD_CLR_PROFILER_LOGGING_H_
