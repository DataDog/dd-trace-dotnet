// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "ErrorCode.h"
#include <string>
#include <string_view>

extern "C"
{
#include "datadog/common.h"
}

namespace libdatadog {
class FfiHelper
{
public:
    FfiHelper() = delete;

    static ddog_ByteSlice StringToByteSlice(std::string const& str);
    static ddog_ByteSlice StringToByteSlice(char const* str);
    static ddog_CharSlice StringToCharSlice(std::string const& str);
    static ddog_CharSlice StringToCharSlice(std::string_view str);
    static ddog_prof_ValueType CreateValueType(std::string const& type, std::string const& unit);
    static std::string ExtractMessage(ddog_Error& error);
};

ErrorCode make_error(ddog_Error& error);
ErrorCode make_error(std::string error);
ErrorCode make_success();
} // namespace libdatadog