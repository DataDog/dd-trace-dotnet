// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>
#include <string>

namespace libdatadog {

namespace detail {
    struct ErrorImpl;
}

class error_code
{
public:
    error_code();
    error_code(std::unique_ptr<detail::ErrorImpl> error);
    ~error_code();

    error_code(error_code&& o) noexcept;
    error_code& operator=(error_code&& o) noexcept;

    std::string message() const noexcept;

    inline operator bool() const noexcept
    {
        return _details == nullptr;
    }

private:
    std::unique_ptr<detail::ErrorImpl> _details;
};
} // namespace libdatadog