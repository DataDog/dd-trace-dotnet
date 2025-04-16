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
    std::string Str;
    ddog_prof_StringId Id;
    bool IsInitialized ;
};