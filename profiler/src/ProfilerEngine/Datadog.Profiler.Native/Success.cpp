// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Success.h"

#include "SuccessImpl.hpp"

namespace libdatadog {

Success::Success() :
    Success(nullptr)
{
}

Success::Success(Success&& o) noexcept
{
    *this = std::move(o);
}

Success& Success::operator=(Success&& o) noexcept
{
    if (this != &o)
    {
        _details = std::move(o._details);
    }
    return *this;
}

Success::Success(std::unique_ptr<SuccessImpl> details) :
    _details(std::move(details))
{
}

Success::~Success() = default;

std::string const & Success::message() const noexcept
{
    static std::string empty;

    if (_details == nullptr)
        return empty;
    return _details->message();
}

} // namespace libdatadog
