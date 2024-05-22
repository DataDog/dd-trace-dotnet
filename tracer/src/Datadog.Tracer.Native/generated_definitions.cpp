#include "generated_definitions.h"
#include "corprof.h"
#include "logger.h"
#include "../Datadog.Trace/Generated/generated_calltargets_net461.h"
#include "../Datadog.Trace/Generated/generated_callsites_net461.h"
#include "../Datadog.Trace/Generated/generated_calltargets_net6_0.h"
#include "../Datadog.Trace/Generated/generated_callsites_net6_0.h"
#include "../Datadog.Trace/Generated/generated_calltargets_netcoreapp3_1.h"
#include "../Datadog.Trace/Generated/generated_callsites_netcoreapp3_1.h"
#include "../Datadog.Trace/Generated/generated_calltargets_netstandard2_0.h"
#include "../Datadog.Trace/Generated/generated_callsites_netstandard2_0.h"

namespace trace
{
WCHAR* assemblyName = (WCHAR*) WStr("Datadog.Trace, Version=2.52.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb");


std::vector<CallTargetDefinition2> GeneratedDefinitions::GetCallTargets(WSTRING platform)
{
    Logger::Debug("GeneratedDefinitions::GetCallTargets: platform: ", platform);

    if (platform == WStr("net461"))
    {
        return g_callTargets_net461;
    }
    if (platform == WStr("net6.0"))
    {
        return g_callTargets_net6_0;
    }
    if (platform == WStr("netstandard2.0"))
    {
        return g_callTargets_netstandard2_0;
    }
    if (platform == WStr("netcoreapp3.1"))
    {
        return g_callTargets_netcoreapp3_1;
    }

    return std::vector<CallTargetDefinition2>();
}

std::vector<WCHAR*> GeneratedDefinitions::GetCallSites(WSTRING platform)
{
    Logger::Debug("GeneratedDefinitions::GetCallSites: platform: ", platform);

    if (platform == WStr("net461"))
    {
        return g_callSites_net461;
    }
    if (platform == WStr("net461_Rasp"))
    {
        return g_callSites_net461_Rasp;
    }
    if (platform == WStr("net6.0"))
    {
        return g_callSites_net6_0;
    }
    if (platform == WStr("net6.0_Rasp"))
    {
        return g_callSites_net6_0_Rasp;
    }
    if (platform == WStr("netstandard2.0"))
    {
        return g_callSites_netstandard2_0;
    }
    if (platform == WStr("netstandard2.0_Rasp"))
    {
        return g_callSites_netstandard2_0_Rasp;
    }
    if (platform == WStr("netcoreapp3.1"))
    {
        return g_callSites_netcoreapp3_1;
    }
    if (platform == WStr("netcoreapp3.1_Rasp"))
    {
        return g_callSites_netcoreapp3_1_Rasp;
    }

    return std::vector<WCHAR*>();
}

WCHAR* GetCallTargets_kk_0[] = {(WCHAR*) WStr("")};
WCHAR* GetCallTargets_kk_1[] = {(WCHAR*) WStr(""), (WCHAR*) WStr(""), (WCHAR*) WStr("")};
WCHAR* GetCallTargets_kk_2[] = {(WCHAR*) WStr("")};


std::vector<CallTargetDefinition2> g_callTargets = 
{
    {(WCHAR*) WStr(""), (WCHAR*) WStr(""), (WCHAR*) WStr(""), GetCallTargets_kk_0, 0, 0, 0, 0, 0, 0, 0,(WCHAR*) WStr(""), (WCHAR*) WStr(""), CallTargetKind::Default, 0},
    {(WCHAR*) WStr(""), (WCHAR*) WStr(""), (WCHAR*) WStr(""), GetCallTargets_kk_1, 0, 0, 0, 0, 0, 0, 0,(WCHAR*) WStr(""), (WCHAR*) WStr(""), CallTargetKind::Default, 0},
    {(WCHAR*) WStr(""), (WCHAR*) WStr(""), (WCHAR*) WStr(""), GetCallTargets_kk_2, 0, 0, 0, 0, 0, 0, 0,(WCHAR*) WStr(""), (WCHAR*) WStr(""), CallTargetKind::Default, 0},
};

} // namespace trace
