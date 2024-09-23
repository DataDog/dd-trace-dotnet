// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "FfiHelper.h"

#include <stdint.h>
#include <string.h>

#include "SuccessImpl.hpp"

extern "C"
{
#include "datadog/common.h"
}

namespace libdatadog {
ddog_ByteSlice FfiHelper::StringToByteSlice(std::string const& str)
{
    return {(uint8_t*)str.c_str(), str.size()};
}

ddog_ByteSlice FfiHelper::StringToByteSlice(char const* str)
{
    return {(uint8_t*)str, strlen(str)};
}

ddog_CharSlice FfiHelper::StringToCharSlice(std::string const& str)
{
    return {str.data(), str.size()};
}

ddog_CharSlice FfiHelper::StringToCharSlice(std::string_view str)
{
    return {str.data(), str.size()};
}

ddog_prof_ValueType FfiHelper::CreateValueType(std::string const& type, std::string const& unit)
{
    auto valueType = ddog_prof_ValueType{};
    valueType.type_ = FfiHelper::StringToCharSlice(type);
    valueType.unit = FfiHelper::StringToCharSlice(unit);
    return valueType;
}

std::string FfiHelper::GetErrorMessage(ddog_Error& error)
{
    auto message = ddog_Error_message(&error);
    return std::string(message.ptr, message.len);
}

std::string FfiHelper::GetErrorMessage(ddog_MaybeError& error)
{
    return std::string((char*)error.some.message.ptr, error.some.message.len);
}

Success make_error(ddog_Error error)
{
    return Success(std::make_unique<SuccessImpl>(error));
}

Success make_error(std::string error)
{
    return Success(std::make_unique<SuccessImpl>(std::move(error)));
}

Success make_error(ddog_MaybeError error)
{
    return Success(std::make_unique<SuccessImpl>(error));
}

Success make_success()
{
    return Success();
}
} // namespace libdatadog
