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
ddog_ByteSlice to_byte_slice(std::string const& str)
{
    return {(uint8_t*)str.c_str(), str.size()};
}

ddog_ByteSlice to_byte_slice(char const* str)
{
    return {(uint8_t*)str, strlen(str)};
}

ddog_CharSlice to_char_slice(std::string const& str)
{
    return {str.data(), str.size()};
}

ddog_CharSlice to_char_slice(std::string_view str)
{
    return {str.data(), str.size()};
}

ddog_prof_ValueType CreateValueType(ddog_prof_StringId typeId, ddog_prof_StringId unitId)
{
    return {.type_id = typeId, .unit_id = unitId};
}

std::string GetErrorMessage(ddog_Error& error)
{
    auto message = ddog_Error_message(&error);
    return std::string(message.ptr, message.len);
}

std::string GetErrorMessage(ddog_MaybeError& error)
{
    return std::string((char*)error.some.message.ptr, error.some.message.len);
}

Success make_error(ddog_Error error)
{
    return Success(new SuccessImpl(error));
}

Success make_error(ddog_prof_Status status)
{
    return Success(new SuccessImpl(status));
}

Success make_error(std::string error)
{
    return Success(new SuccessImpl(std::move(error)));
}

Success make_error(ddog_MaybeError error)
{
    return Success(new SuccessImpl(error));
}

Success make_success()
{
    return Success();
}
} // namespace libdatadog
