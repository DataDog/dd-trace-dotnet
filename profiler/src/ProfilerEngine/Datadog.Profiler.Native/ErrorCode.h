// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>
#include <string>

namespace libdatadog {

namespace detail {
    struct ErrorCodeImpl;
}

class ErrorCode
{
public:
    ErrorCode();
    ErrorCode(std::unique_ptr<detail::ErrorCodeImpl> error);
    ~ErrorCode();

    ErrorCode(ErrorCode&& o) noexcept;
    ErrorCode& operator=(ErrorCode&& o) noexcept;

    std::string message() const noexcept;

    inline operator bool() const noexcept
    {
        return _details == nullptr;
    }

private:
    std::unique_ptr<detail::ErrorCodeImpl> _details;
};
} // namespace libdatadog