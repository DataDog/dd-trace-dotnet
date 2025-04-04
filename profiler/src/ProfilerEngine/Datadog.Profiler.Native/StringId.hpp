#include <string>

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

struct StringId
{
public:
    ddog_prof_StringId Id;
};