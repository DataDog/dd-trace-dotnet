// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ErrorCode.h"

#include "ErrorCodeImpl.hpp"

namespace libdatadog {

ErrorCode::ErrorCode() :
    ErrorCode(nullptr)
{
}

ErrorCode::ErrorCode(ErrorCode&& o) noexcept
{
    *this = std::move(o);
}

ErrorCode& ErrorCode::operator=(ErrorCode&& o) noexcept
{
    if (this != &o)
    {
        _details = std::move(o._details);
    }
    return *this;
}

ErrorCode::ErrorCode(std::unique_ptr<detail::ErrorCodeImpl> details) :
    _details(std::move(details))
{
}

ErrorCode::~ErrorCode() = default;

std::string ErrorCode::message() const noexcept
{
    if (_details == nullptr)
        return std::string();
    return _details->message();
}

} // namespace libdatadog
