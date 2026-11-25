#pragma once

#include "logger.h"

#include <memory>

namespace datadog::shared
{
class LogManager
{
public:
    LogManager() = delete;

    /// <summary>
    /// This templated method returns a pointer to a logger in respect to the TLoggerPolicy trait.
    /// Callers of this method borrow the pointer, this means that they do not have/must not call delete,
    /// nor free on it.
    /// </summary>
    /// <typeparam name="TLoggerPolicy">Policy used to configure the Logger</typeparam>
    /// <returns>A pointer to a Logger instance</returns>
    template <class TLoggerPolicy>
    static Logger* Get()
    {
#ifdef _WIN32
        static Logger instance = Logger::Create<TLoggerPolicy>();
        return &instance;
#else
        // Intentionally never destroyed. On Linux/macOS, exit() runs static destructors while other
        // native threads (JIT callbacks, ReJIT worker, profiler timers) may still be logging, so
        // destroying the logger races with them (pure virtual call / invalid free in spdlog teardown).
        // The OS reclaims the memory, Info+ messages are flushed on write (flush_on(info)), and libc
        // flushes any remaining FILE buffers at exit.
        static Logger* instance = new Logger(Logger::Create<TLoggerPolicy>());
        return instance;
#endif
    }
};
}