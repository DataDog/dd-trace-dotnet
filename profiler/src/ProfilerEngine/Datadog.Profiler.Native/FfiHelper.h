// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "Success.h"
#include <string>
#include <string_view>
#include <vector>

#include "shared/src/native-src/dd_span.hpp"

extern "C"
{
#include "datadog/common.h"
}

namespace libdatadog {

template <class T>
ddog_ByteSlice to_byte_slice(std::vector<T> const& v)
{
    return {(uint8_t*)v.data(), v.size()};
}
template <class T>
ddog_ByteSlice to_byte_slice(shared::span<T> const& v)
{
    return {(uint8_t*)v.data(), v.size()};
}

ddog_CharSlice to_char_slice(std::string const& str);
ddog_CharSlice to_char_slice(std::string_view str);
constexpr ddog_CharSlice to_char_slice(const char* str)
{
    return {str, std::char_traits<char>::length(str)};
}
ddog_prof_ValueType CreateValueType(std::string const& type, std::string const& unit);

std::string GetErrorMessage(ddog_Error& error);
std::string GetErrorMessage(ddog_MaybeError& error);

Success make_error(ddog_Error error);
Success make_error(std::string error);
Success make_error(ddog_MaybeError error);
Success make_success();
} // namespace libdatadog