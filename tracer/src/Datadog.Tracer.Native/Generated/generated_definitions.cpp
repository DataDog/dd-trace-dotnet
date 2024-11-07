#include "generated_definitions.h"
#include "../../../../shared/src/native-src/version.h"
#include "../logger.h"
#include "corprof.h"
#include "generated_callsites.g.h"
#include "generated_calltargets.g.h"

namespace trace
{

std::vector<WCHAR*>* GeneratedDefinitions::GetCallSites()
{
    return &g_callSites;
}

std::vector<CallTargetDefinition2>* GeneratedDefinitions::GetCallTargets()
{
    return &g_callTargets;
}

} // namespace trace