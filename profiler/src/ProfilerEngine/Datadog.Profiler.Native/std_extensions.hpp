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