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
#include "datadog/common.h"
#include "datadog/profiling.h"
}

namespace libdatadog {

struct SuccessImpl
{
    SuccessImpl(ddog_Error error) :
        SuccessImpl(GetErrorMessage(error))
    {
        ddog_Error_drop(&error);
    }

    SuccessImpl(ddog_MaybeError error) :
        SuccessImpl(GetErrorMessage(error.some))
    {
        assert(error.tag == DDOG_OPTION_ERROR_SOME_ERROR);
        ddog_MaybeError_drop(error);
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