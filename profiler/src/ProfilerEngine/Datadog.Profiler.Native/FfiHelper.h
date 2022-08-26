// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>
#include <string_view>
#include <vector>

extern "C"
{
#include "datadog/common.h"
}

class FfiHelper
{
public:
    FfiHelper() = delete;

    static ddog_ByteSlice StringToByteSlice(std::string const& str);
    static ddog_ByteSlice StringToByteSlice(char const* str);
    static ddog_Slice_c_char StringToCharSlice(std::string const& str);
    static ddog_Slice_c_char StringToCharSlice(std::string_view str);
    static ddog_ValueType CreateValueType(std::string const& type, std::string const& unit);
};