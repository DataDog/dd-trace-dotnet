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
        static Logger instance = Logger::Create<TLoggerPolicy>();
        return &instance;
    }
};
}