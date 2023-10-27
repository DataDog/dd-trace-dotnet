// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>
#include <string>
#include <utility>

#include "FfiHelper.h"

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

namespace libdatadog::detail {

struct ErrorCodeImpl
{
    ErrorCodeImpl(ddog_Error error) :
        ErrorCodeImpl(error, "", true)
    {
    }
    ErrorCodeImpl(std::string error) :
        ErrorCodeImpl({}, std::move(error), false)
    {
    }

    ~ErrorCodeImpl()
    {
        if (_useDdogError)
        {
            ddog_Error_drop(&_error);
        }
    }

    ErrorCodeImpl(ErrorCodeImpl const&) = delete;
    ErrorCodeImpl& operator=(ErrorCodeImpl const&) = delete;

    ErrorCodeImpl(ErrorCodeImpl&& o) noexcept
    {
        *this = std::move(o);
    }

    ErrorCodeImpl& operator=(ErrorCodeImpl&& o) noexcept
    {
        if (this != &o)
        {
            _error = std::exchange(o._error, {});
            _message.swap(o._message);
            _useDdogError = o._useDdogError;
        }
        return *this;
    }

    std::string const& message() const
    {
        return _message;
    }

private:
    ErrorCodeImpl(ddog_Error error, std::string m, bool useDdogError) :
        _error{error}, _message{std::move(m)}, _useDdogError{useDdogError}
    {
        if (_useDdogError)
        {
            _message = FfiHelper::ExtractMessage(_error);
        }
    }

    ddog_Error _error;
    std::string _message;
    bool _useDdogError;
};
} // namespace libdatadog::detail