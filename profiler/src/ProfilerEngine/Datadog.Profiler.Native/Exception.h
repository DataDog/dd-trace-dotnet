// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <exception>
#include <memory>
#include <string>

namespace libdatadog {

struct SuccessImpl;

class Exception : public std::exception
{
public:
    Exception(std::unique_ptr<SuccessImpl> error);
    ~Exception();

    char const* what() const noexcept override;

private:
    std::unique_ptr<SuccessImpl> _impl;
};
} // namespace libdatadog
