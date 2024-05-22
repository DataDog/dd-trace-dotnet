#pragma once
#include "../../../shared/src/native-src/pal.h"
#include "cor_profiler.h"
#include <map>

namespace trace
{

class GeneratedDefinitions
{
public:
    static std::vector<CallTargetDefinition2> GetCallTargets(WSTRING platform);
    static std::vector<WCHAR*> GetCallSites(WSTRING platform);
};
} // namespace trace