// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>
#include <string_view>
#include <vector>

extern "C"
{
#include "ddprof/ffi.h"
}

class FfiHelper
{
public:
    FfiHelper() = delete;

    static ddprof_ffi_ByteSlice StringToByteSlice(std::string const& str);
    static ddprof_ffi_ByteSlice StringToByteSlice(char const* str);
    static ddprof_ffi_Slice_c_char StringToCharSlice(std::string const& str);
    static ddprof_ffi_Slice_c_char StringToCharSlice(std::string_view str);
    static ddprof_ffi_ValueType CreateValueType(std::string const& type, std::string const& unit);
};