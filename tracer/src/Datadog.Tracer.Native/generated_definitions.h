#pragma once
#include "../../../shared/src/native-src/pal.h"
#include "cor_profiler.h"
#include <map>
#include "version.h"

namespace trace
{

constexpr auto TRACER_ASSEMBLY = WStr("Datadog.Trace, Version=" DD_PROFILER_VERSION ".0, Culture = neutral, PublicKeyToken = def86d061d0d2eeb");

class GeneratedDefinitions
{
public:
    static std::vector<CallTargetDefinition2>* GetCallTargets(WSTRING platform);
    static std::vector<WCHAR*>* GetCallSites(WSTRING platform);
};
} // namespace trace