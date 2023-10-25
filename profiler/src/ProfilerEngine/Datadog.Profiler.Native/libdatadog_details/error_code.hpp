// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>
#include <string>
#include <utility>

#include "std_extensions.hpp"

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

namespace libdatadog::detail {

struct ErrorImpl
{
    ErrorImpl(ddog_Error error) :
        ErrorImpl(error, std::string(), true)
    {
    }
    ErrorImpl(std::string error) :
        ErrorImpl({}, std::move(error), false)
    {
    }

    ~ErrorImpl()
    {
        if (_freeError)
        {
            ddog_Error_drop(&_error);
        }
    }

    ErrorImpl(ErrorImpl const&) = delete;
    ErrorImpl& operator=(ErrorImpl const&) = delete;

    ErrorImpl(ErrorImpl&& o) noexcept
    {
        *this = std::move(o);
    }

    ErrorImpl& operator=(ErrorImpl&& o) noexcept
    {
        if (this != &o)
        {
            _error = std::exchange(o._error, {});
            _message.swap(o._message);
            _freeError = o._freeError;
        }
        return *this;
    }

    std::string message() const
    {
        if (!_message.empty())
        {
            return _message;
        }
        return std::to_string(&_error);
    }

private:
    ErrorImpl(ddog_Error error, std::string m, bool freeError) :
        _error{error}, _message{std::move(m)}, _freeError{freeError}
    {
    }

    ddog_Error _error;
    std::string _message;
    bool _freeError;
};

template<class T>
inline error_code make_error(T s)
{
    return error_code(std::make_unique<ErrorImpl>(std::move(s)));
}

inline error_code make_success()
{
    return error_code{};
}
} // namespace libdatadog::detail