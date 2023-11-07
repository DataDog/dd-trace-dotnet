// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Exception.h"
#include "SuccessImpl.hpp"

namespace libdatadog {

Exception::~Exception() = default;

Exception::Exception(std::unique_ptr<SuccessImpl> error) :
    _impl(std::move(error))
{
}
char const* Exception::what() const noexcept
{
    return _impl->message().c_str();
}
} // namespace libdatadog
