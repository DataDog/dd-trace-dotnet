// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "FfiHelper.h"
#include <string.h>

ddprof_ffi_ByteSlice FfiHelper::StringToByteSlice(std::string const& str)
{
    return {(uint8_t*)str.c_str(), str.size()};
}

ddprof_ffi_ByteSlice FfiHelper::StringToByteSlice(char const* str)
{
    return {(uint8_t*)str, strlen(str)};
}

ddprof_ffi_Slice_c_char FfiHelper::StringToCharSlice(std::string const& str)
{
    return {str.data(), str.size()};
}

ddprof_ffi_Slice_c_char FfiHelper::StringToCharSlice(std::string_view str)
{
    return {str.data(), str.size()};
}

ddprof_ffi_ValueType FfiHelper::CreateValueType(std::string const& type, std::string const& unit)
{
    auto valueType = ddprof_ffi_ValueType{};
    valueType.type_ = FfiHelper::StringToCharSlice(type);
    valueType.unit = FfiHelper::StringToCharSlice(unit);
    return valueType;
}