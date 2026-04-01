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

bool IsCountUnit(std::string_view unit)
{
    return unit == "count" || unit == "counts";
}

bool IsBytesUnit(std::string_view unit)
{
    return unit == "byte" || unit == "bytes";
}

bool IsNanosecondsUnit(std::string_view unit)
{
    return unit == "nanosecond" || unit == "nanoseconds" || unit == "Nanosecond" || unit == "Nanoseconds";
}

bool TryCreateSampleType(std::string_view type, std::string_view unit, ddog_prof_SampleType& sampleType)
{
    if (type == "alloc-samples" && IsCountUnit(unit))
    {
        sampleType = DDOG_PROF_SAMPLE_TYPE_ALLOC_SAMPLES;
        return true;
    }

    if (type == "alloc-size" && IsBytesUnit(unit))
    {
        sampleType = DDOG_PROF_SAMPLE_TYPE_ALLOC_SIZE;
        return true;
    }

    if (type == "cpu" && IsNanosecondsUnit(unit))
    {
        sampleType = DDOG_PROF_SAMPLE_TYPE_CPU_LEGACY;
        return true;
    }

    if (type == "cpu-samples" && IsCountUnit(unit))
    {
        sampleType = DDOG_PROF_SAMPLE_TYPE_CPU_SAMPLES;
        return true;
    }

    if (type == "exception" && IsCountUnit(unit))
    {
        sampleType = DDOG_PROF_SAMPLE_TYPE_EXCEPTION_LEGACY;
        return true;
    }

    if (type == "inuse-objects" && IsCountUnit(unit))
    {
        sampleType = DDOG_PROF_SAMPLE_TYPE_INUSE_OBJECTS;
        return true;
    }

    if (type == "inuse-space" && IsBytesUnit(unit))
    {
        sampleType = DDOG_PROF_SAMPLE_TYPE_INUSE_SPACE;
        return true;
    }

    if (type == "lock-count" && IsCountUnit(unit))
    {
        sampleType = DDOG_PROF_SAMPLE_TYPE_LOCK_COUNT;
        return true;
    }

    if (type == "lock-time" && IsNanosecondsUnit(unit))
    {
        sampleType = DDOG_PROF_SAMPLE_TYPE_LOCK_TIME;
        return true;
    }

    if (type == "request-time" && IsNanosecondsUnit(unit))
    {
        sampleType = DDOG_PROF_SAMPLE_TYPE_REQUEST_TIME;
        return true;
    }

    if (type == "timeline" && IsNanosecondsUnit(unit))
    {
        sampleType = DDOG_PROF_SAMPLE_TYPE_TIMELINE;
        return true;
    }

    if (type == "wall" && IsNanosecondsUnit(unit))
    {
        sampleType = DDOG_PROF_SAMPLE_TYPE_WALL_LEGACY;
        return true;
    }

    if ((type == "wall-time" || type == "RealTime") && IsNanosecondsUnit(unit))
    {
        sampleType = DDOG_PROF_SAMPLE_TYPE_WALL_TIME;
        return true;
    }

    return false;
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
