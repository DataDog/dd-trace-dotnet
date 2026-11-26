// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "FfiHelper.h"

#include <stdint.h>
#include <string.h>

#include "SuccessImpl.hpp"

extern "C"
{
#include "datadog/common.h"
#include "datadog_poc/common.h"
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

ddog_charslice to_poc_char_slice(std::string const& str)
{
    return {str.data(), str.size()};
}

ddog_charslice to_poc_char_slice(std::string_view str)
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

// Canonical (type, unit) pprof strings, one per recognized friendly name.
// These are not our own invention - they're taken verbatim from real
// libdatadog's SampleType -> ValueType mapping (libdd-profiling/src/api/sample_type.rs),
// which is *not* just an identity mapping: e.g. both "wall-time" and the
// legacy alias "RealTime" normalize to the same wire string ("wall-time",
// "nanoseconds"), distinct from "wall" ("wall", "nanoseconds"). Preserving
// this table (rather than passing whatever raw string the caller used)
// keeps the wire format identical to what real libdatadog would have sent.
static ddog_prof_value_type MakeValueType(const char* type, const char* unit)
{
    return {to_poc_char_slice(type), to_poc_char_slice(unit)};
}

bool TryCreateSampleType(std::string_view type, std::string_view unit, ddog_prof_value_type& sampleType)
{
    if (type == "alloc-samples" && IsCountUnit(unit))
    {
        sampleType = MakeValueType("alloc-samples", "count");
        return true;
    }

    if (type == "alloc-size" && IsBytesUnit(unit))
    {
        sampleType = MakeValueType("alloc-size", "bytes");
        return true;
    }

    if (type == "cpu" && IsNanosecondsUnit(unit))
    {
        sampleType = MakeValueType("cpu", "nanoseconds");
        return true;
    }

    if (type == "cpu-samples" && IsCountUnit(unit))
    {
        sampleType = MakeValueType("cpu-samples", "count");
        return true;
    }

    if (type == "exception" && IsCountUnit(unit))
    {
        sampleType = MakeValueType("exception", "count");
        return true;
    }

    if (type == "inuse-objects" && IsCountUnit(unit))
    {
        sampleType = MakeValueType("inuse-objects", "count");
        return true;
    }

    if (type == "inuse-space" && IsBytesUnit(unit))
    {
        sampleType = MakeValueType("inuse-space", "bytes");
        return true;
    }

    if (type == "lock-count" && IsCountUnit(unit))
    {
        sampleType = MakeValueType("lock-count", "count");
        return true;
    }

    if (type == "lock-time" && IsNanosecondsUnit(unit))
    {
        sampleType = MakeValueType("lock-time", "nanoseconds");
        return true;
    }

    if (type == "request-time" && IsNanosecondsUnit(unit))
    {
        sampleType = MakeValueType("request-time", "nanoseconds");
        return true;
    }

    if (type == "timeline" && IsNanosecondsUnit(unit))
    {
        sampleType = MakeValueType("timeline", "nanoseconds");
        return true;
    }

    if (type == "wall" && IsNanosecondsUnit(unit))
    {
        sampleType = MakeValueType("wall", "nanoseconds");
        return true;
    }

    if ((type == "wall-time" || type == "RealTime") && IsNanosecondsUnit(unit))
    {
        sampleType = MakeValueType("wall-time", "nanoseconds");
        return true;
    }

    return false;
}

Success make_error(ddog_error_code error)
{
    return Success(std::make_unique<SuccessImpl>(error));
}

Success make_error(std::string error)
{
    return Success(std::make_unique<SuccessImpl>(std::move(error)));
}

Success make_success()
{
    return Success();
}
} // namespace libdatadog
