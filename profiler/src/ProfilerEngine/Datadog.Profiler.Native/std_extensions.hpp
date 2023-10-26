// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>

extern "C"
{
#include "datadog/common.h"
}

namespace std {
inline std::string to_string(ddog_Error const* e)
{
    auto message = ddog_Error_message(e);
    return std::string(message.ptr, message.len);
}
} // namespace std