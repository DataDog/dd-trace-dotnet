#include "generated_definitions.h"
#include "../../../../shared/src/native-src/version.h"
#include "../logger.h"
#include "corprof.h"
#include "generated_callsites.g.h"

namespace trace
{

std::vector<WCHAR*>* GeneratedDefinitions::GetCallSites()
{
    return &g_callSites;
}

} // namespace trace