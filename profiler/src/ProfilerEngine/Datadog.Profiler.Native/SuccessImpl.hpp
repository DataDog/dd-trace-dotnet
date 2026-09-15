// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cassert>
#include <memory>
#include <string>
#include <utility>

#include "FfiHelper.h"

extern "C"
{
#include "datadog_poc/common.h"
#include "datadog_poc/profiling.h"
}

namespace libdatadog {

struct SuccessImpl
{
    SuccessImpl(ddog_error_code error) :
        SuccessImpl(std::string(ddog_last_error_message()))
    {
        assert(error != DDOG_OK);
        (void)error; // only used for the assert above - the message is already captured
    }

    SuccessImpl(std::string message) :
        _message{std::move(message)}
    {
    }

    std::string const& message() const
    {
        return _message;
    }

private:
    std::string _message;
};
} // namespace libdatadog