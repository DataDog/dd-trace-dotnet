// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "Success.h"
#include <string>
#include <string_view>

extern "C"
{
// Real libdatadog headers - still needed here because CrashReporting.cpp/
// CrashReportingLinux.cpp share this file's to_char_slice() helper for their
// (unrelated, real-ABI) ddog_crasht_*/ddog_Vec_Tag_push/ddog_endpoint_from_url
// calls. Safe to include alongside the PoC headers below: verified no
// colliding type/enum-constant names except DDOG_PROF_ENDPOINT_AGENT[LESS],
// which the PoC headers avoid via a DDOG_POC_ prefix.
#include "datadog/common.h"
#include "datadog_poc/common.h"
#include "datadog_poc/profiling.h"
}

namespace libdatadog {
// Real ddog_CharSlice - used by CrashReporting.cpp/CrashReportingLinux.cpp only.
ddog_CharSlice to_char_slice(std::string const& str);
ddog_CharSlice to_char_slice(std::string_view str);
constexpr ddog_CharSlice to_char_slice(const char* str)
{
    return {str, std::char_traits<char>::length(str)};
}

// Our ddog_charslice - used by the profiling wrapper (Profile.cpp, AgentProxy.hpp,
// ExporterBuilder.cpp, Tags.cpp) to call the PoC library instead of real libdatadog.
ddog_charslice to_poc_char_slice(std::string const& str);
ddog_charslice to_poc_char_slice(std::string_view str);
constexpr ddog_charslice to_poc_char_slice(const char* str)
{
    return {str, std::char_traits<char>::length(str)};
}

bool TryCreateSampleType(std::string_view type, std::string_view unit, ddog_prof_value_type& sampleType);

Success make_error(ddog_error_code error);
Success make_error(std::string error);
Success make_success();
} // namespace libdatadog