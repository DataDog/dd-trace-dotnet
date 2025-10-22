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
    Success(SuccessImpl* error);
    ~Success();

    Success(Success&& o) noexcept;
    Success& operator=(Success&& o) noexcept;

    std::string const& message() const noexcept;

    inline operator bool() const noexcept
    {
        return _details == nullptr;
    }

private:
    // in this case we do not use a unique_ptr to avoid
    // calling into the ctor and dtor excessively.
    SuccessImpl* _details;
};
} // namespace libdatadog