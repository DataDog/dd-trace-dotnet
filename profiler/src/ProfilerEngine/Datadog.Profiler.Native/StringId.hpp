#pragma once

#include <limits>
#include <string>

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

struct StringId
{
public:
    // generation.id is a std::uint64_t and represents a profile generation.
    // This not great, we should have libdatadog giving us an invalid ddog_prof_StringId instead.
    // But...the chances we hit the std::uint64_t max value are really low.
    std::string Str;
    ddog_prof_StringId Id = {.generation = {.id = std::numeric_limits<std::uint64_t>::max()}};
};