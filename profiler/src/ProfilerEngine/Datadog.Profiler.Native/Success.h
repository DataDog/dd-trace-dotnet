// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>
#include <string>

namespace libdatadog {

struct SuccessImpl;

class Success
{
public:
    Success();
    Success(std::unique_ptr<SuccessImpl> error);
    ~Success();

    Success(Success&& o) noexcept;
    Success& operator=(Success&& o) noexcept;

    std::string const& message() const noexcept;

    inline operator bool() const noexcept
    {
        return _details == nullptr;
    }

private:
    std::unique_ptr<SuccessImpl> _details;
};
} // namespace libdatadog