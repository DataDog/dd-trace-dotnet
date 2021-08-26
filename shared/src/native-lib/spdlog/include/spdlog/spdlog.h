//
// Copyright(c) 2015-2018 Gabi Melman.
// Distributed under the MIT License (http://opensource.org/licenses/MIT)
//
// spdlog main header file.
// see example.cpp for usage example

#ifndef SPDLOG_H
#define SPDLOG_H
#pragma once

#include "spdlog/common.h"
#include "spdlog/details/registry.h"
#include "spdlog/logger.h"
#include "spdlog/version.h"

#include <chrono>
#include <functional>
#include <memory>
#include <string>

namespace spdlog {

// Default logger factory-  creates synchronous loggers
struct synchronous_factory
{
    template<typename Sink, typename... SinkArgs>
    static std::shared_ptr<spdlog::logger> create(std::string logger_name, SinkArgs &&... args)
    {
        auto sink = std::make_shared<Sink>(std::forward<SinkArgs>(args)...);
        auto new_logger = std::make_shared<logger>(std::move(logger_name), std::move(sink));
        details::registry::instance().initialize_logger(new_logger);
        return new_logger;
    }
};

using default_factory = synchronous_factory;

// Create and register a logger with a templated sink type
// The logger's level, formatter and flush level will be set according the
// global settings.
// Example:
// spdlog::create<daily_file_sink_st>("logger_name", "dailylog_filename", 11, 59);
template<typename Sink, typename... SinkArgs>
inline std::shared_ptr<spdlog::logger> create(std::string logger_name, SinkArgs &&... sink_args)
{
    return default_factory::create<Sink>(std::move(logger_name), std::forward<SinkArgs>(sink_args)...);
}

// Return an existing logger or nullptr if a logger with such name doesn't
// exist.
// example: spdlog::get("my_logger")->info("hello {}", "world");
inline std::shared_ptr<logger> get(const std::string &name)
{
    return details::registry::instance().get(name);
}

// Set global formatter. Each sink in each logger will get a clone of this object
inline void set_formatter(std::unique_ptr<spdlog::formatter> formatter)
{
    details::registry::instance().set_formatter(std::move(formatter));
}

// Set global format string.
// example: spdlog::set_pattern("%Y-%m-%d %H:%M:%S.%e %l : %v");
inline void set_pattern(std::string pattern, pattern_time_type time_type = pattern_time_type::local)
{
    set_formatter(std::unique_ptr<spdlog::formatter>(new pattern_formatter(std::move(pattern), time_type)));
}

// Set global logging level
inline void set_level(level::level_enum log_level)
{
    details::registry::instance().set_level(log_level);
}

// Set global flush level
inline void flush_on(level::level_enum log_level)
{
    details::registry::instance().flush_on(log_level);
}

// Start/Restart a periodic flusher thread
// Warning: Use only if all your loggers are thread safe!
inline void flush_every(std::chrono::seconds interval)
{
    details::registry::instance().flush_every(interval);
}

// Set global error handler
inline void set_error_handler(log_err_handler handler)
{
    details::registry::instance().set_error_handler(std::move(handler));
}

// Register the given logger with the given name
inline void register_logger(std::shared_ptr<logger> logger)
{
    details::registry::instance().register_logger(std::move(logger));
}

// Apply a user defined function on all registered loggers
// Example:
// spdlog::apply_all([&](std::shared_ptr<spdlog::logger> l) {l->flush();});
inline void apply_all(const std::function<void(std::shared_ptr<logger>)> &fun)
{
    details::registry::instance().apply_all(fun);
}

// Drop the reference to the given logger
inline void drop(const std::string &name)
{
    details::registry::instance().drop(name);
}

// Drop all references from the registry
inline void drop_all()
{
    details::registry::instance().drop_all();
}

// stop any running threads started by spdlog and clean registry loggers
inline void shutdown()
{
    details::registry::instance().shutdown();
}

// Automatic registration of loggers when using spdlog::create() or spdlog::create_async
inline void set_automatic_registration(bool automatic_registation)
{
    details::registry::instance().set_automatic_registration(automatic_registation);
}

// API for using default logger (stdout_color_mt),
// e.g: spdlog::info("Message {}", 1);
//
// The default logger object can be accessed using the spdlog::default_logger():
// For example, to add another sink to it:
// spdlog::default_logger()->sinks()->push_back(some_sink);
//
// The default logger can replaced using spdlog::set_default_logger(new_logger).
// For example, to replace it with a file logger.
//
// IMPORTANT:
// The default API is thread safe (for _mt loggers), but:
// set_default_logger() *should not* be used concurrently with the default API.
// e.g do not call set_default_logger() from one thread while calling spdlog::info() from another.

inline std::shared_ptr<spdlog::logger> default_logger()
{
    return details::registry::instance().default_logger();
}

inline spdlog::logger *default_logger_raw()
{
    return details::registry::instance().get_default_raw();
}

inline void set_default_logger(std::shared_ptr<spdlog::logger> default_logger)
{
    details::registry::instance().set_default_logger(std::move(default_logger));
}

template<typename... Args>
inline void log(source_loc source, level::level_enum lvl, const char *fmt, const Args &... args)
{
    default_logger_raw()->log(source, lvl, fmt, args...);
}

template<typename... Args>
inline void log(level::level_enum lvl, const char *fmt, const Args &... args)
{
    default_logger_raw()->log(source_loc{}, lvl, fmt, args...);
}

template<typename... Args>
inline void trace(const char *fmt, const Args &... args)
{
    default_logger_raw()->trace(fmt, args...);
}

template<typename... Args>
inline void debug(const char *fmt, const Args &... args)
{
    default_logger_raw()->debug(fmt, args...);
}

template<typename... Args>
inline void info(const char *fmt, const Args &... args)
{
    default_logger_raw()->info(fmt, args...);
}

template<typename... Args>
inline void warn(const char *fmt, const Args &... args)
{
    default_logger_raw()->warn(fmt, args...);
}

template<typename... Args>
inline void error(const char *fmt, const Args &... args)
{
    default_logger_raw()->error(fmt, args...);
}

template<typename... Args>
inline void critical(const char *fmt, const Args &... args)
{
    default_logger_raw()->critical(fmt, args...);
}

template<typename T>
inline void log(level::level_enum lvl, const T &msg)
{
    default_logger_raw()->log(lvl, msg);
}

template<typename T>
inline void trace(const T &msg)
{
    default_logger_raw()->trace(msg);
}

template<typename T>
inline void debug(const T &msg)
{
    default_logger_raw()->debug(msg);
}

template<typename T>
inline void info(const T &msg)
{
    default_logger_raw()->info(msg);
}

template<typename T>
inline void warn(const T &msg)
{
    default_logger_raw()->warn(msg);
}

template<typename T>
inline void error(const T &msg)
{
    default_logger_raw()->error(msg);
}

template<typename T>
inline void critical(const T &msg)
{
    default_logger_raw()->critical(msg);
}

#ifdef SPDLOG_WCHAR_TO_UTF8_SUPPORT
template<typename... Args>
inline void log(level::level_enum lvl, const wchar_t *fmt, const Args &... args)
{
    default_logger_raw()->log(lvl, fmt, args...);
}

template<typename... Args>
inline void trace(const wchar_t *fmt, const Args &... args)
{
    default_logger_raw()->trace(fmt, args...);
}

template<typename... Args>
inline void debug(const wchar_t *fmt, const Args &... args)
{
    default_logger_raw()->debug(fmt, args...);
}

template<typename... Args>
inline void info(const wchar_t *fmt, const Args &... args)
{
    default_logger_raw()->info(fmt, args...);
}

template<typename... Args>
inline void warn(const wchar_t *fmt, const Args &... args)
{
    default_logger_raw()->warn(fmt, args...);
}

template<typename... Args>
inline void error(const wchar_t *fmt, const Args &... args)
{
    default_logger_raw()->error(fmt, args...);
}

template<typename... Args>
inline void critical(const wchar_t *fmt, const Args &... args)
{
    default_logger_raw()->critical(fmt, args...);
}

#endif // SPDLOG_WCHAR_TO_UTF8_SUPPORT

} // namespace spdlog

//
// enable/disable log calls at compile time according to global level.
//
// define SPDLOG_ACTIVE_LEVEL to one of those (before including spdlog.h):
// SPDLOG_LEVEL_TRACE,
// SPDLOG_LEVEL_DEBUG,
// SPDLOG_LEVEL_INFO,
// SPDLOG_LEVEL_WARN,
// SPDLOG_LEVEL_ERROR,
// SPDLOG_LEVEL_CRITICAL,
// SPDLOG_LEVEL_OFF
//

#define SPDLOG_LOGGER_CALL(logger, level, ...)                                                                                             \
    if (logger->should_log(level))                                                                                                         \
    logger->log(spdlog::source_loc{SPDLOG_FILE_BASENAME(__FILE__), __LINE__, SPDLOG_FUNCTION}, level, __VA_ARGS__)

#if SPDLOG_ACTIVE_LEVEL <= SPDLOG_LEVEL_TRACE
#define SPDLOG_LOGGER_TRACE(logger, ...) SPDLOG_LOGGER_CALL(logger, spdlog::level::trace, __VA_ARGS__)
#define SPDLOG_TRACE(...) SPDLOG_LOGGER_TRACE(spdlog::default_logger_raw(), __VA_ARGS__)
#else
#define SPDLOG_LOGGER_TRACE(logger, ...) (void)0
#define SPDLOG_TRACE(...) (void)0
#endif

#if SPDLOG_ACTIVE_LEVEL <= SPDLOG_LEVEL_DEBUG
#define SPDLOG_LOGGER_DEBUG(logger, ...) SPDLOG_LOGGER_CALL(logger, spdlog::level::debug, __VA_ARGS__)
#define SPDLOG_DEBUG(...) SPDLOG_LOGGER_DEBUG(spdlog::default_logger_raw(), __VA_ARGS__)
#else
#define SPDLOG_LOGGER_DEBUG(logger, ...) (void)0
#define SPDLOG_DEBUG(...) (void)0
#endif

#if SPDLOG_ACTIVE_LEVEL <= SPDLOG_LEVEL_INFO
#define SPDLOG_LOGGER_INFO(logger, ...) SPDLOG_LOGGER_CALL(logger, spdlog::level::info, __VA_ARGS__)
#define SPDLOG_INFO(...) SPDLOG_LOGGER_INFO(spdlog::default_logger_raw(), __VA_ARGS__)
#else
#define SPDLOG_LOGGER_INFO(logger, ...) (void)0
#define SPDLOG_INFO(...) (void)0
#endif

#if SPDLOG_ACTIVE_LEVEL <= SPDLOG_LEVEL_WARN
#define SPDLOG_LOGGER_WARN(logger, ...) SPDLOG_LOGGER_CALL(logger, spdlog::level::warn, __VA_ARGS__)
#define SPDLOG_WARN(...) SPDLOG_LOGGER_WARN(spdlog::default_logger_raw(), __VA_ARGS__)
#else
#define SPDLOG_LOGGER_WARN(logger, ...) (void)0
#define SPDLOG_WARN(...) (void)0
#endif

#if SPDLOG_ACTIVE_LEVEL <= SPDLOG_LEVEL_ERROR
#define SPDLOG_LOGGER_ERROR(logger, ...) SPDLOG_LOGGER_CALL(logger, spdlog::level::err, __VA_ARGS__)
#define SPDLOG_ERROR(...) SPDLOG_LOGGER_ERROR(spdlog::default_logger_raw(), __VA_ARGS__)
#else
#define SPDLOG_LOGGER_ERROR(logger, ...) (void)0
#define SPDLOG_ERROR(...) (void)0
#endif

#if SPDLOG_ACTIVE_LEVEL <= SPDLOG_LEVEL_CRITICAL
#define SPDLOG_LOGGER_CRITICAL(logger, ...) SPDLOG_LOGGER_CALL(logger, spdlog::level::critical, __VA_ARGS__)
#define SPDLOG_CRITICAL(...) SPDLOG_LOGGER_CRITICAL(spdlog::default_logger_raw(), __VA_ARGS__)
#else
#define SPDLOG_LOGGER_CRITICAL(logger, ...) (void)0
#define SPDLOG_CRITICAL(...) (void)0
#endif

#endif // SPDLOG_H
