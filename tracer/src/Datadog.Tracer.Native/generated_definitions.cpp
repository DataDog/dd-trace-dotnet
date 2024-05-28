#include "generated_definitions.h"
#include "corprof.h"
#include "logger.h"
#include "../../../shared/src/native-src/version.h"
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

WCHAR* assemblyName = (WCHAR*)TRACER_ASSEMBLY;

std::vector<CallTargetDefinition2>* GeneratedDefinitions::GetCallTargets(WSTRING platform)
{
    std::vector<CallTargetDefinition2>* res = nullptr;

    if (platform == WStr("net461"))
    {
        res = &g_callTargets_net461;
    }
    if (platform == WStr("net6.0"))
    {
        res = &g_callTargets_net6_0;
    }
    if (platform == WStr("netstandard2.0"))
    {
        res = &g_callTargets_netstandard2_0;
    }
    if (platform == WStr("netcoreapp3.1"))
    {
        res = &g_callTargets_netcoreapp3_1;
    }

    if (res != nullptr)
    {
        Logger::Debug("GeneratedDefinitions::GetCallTargets: platform: ", platform, " -> Definitions returned: ", res->size());
    }
    else
    {
        Logger::Error("GeneratedDefinitions::GetCallTargets: platform not found: ", platform);
    }

    return res;
}

std::vector<WCHAR*>* GeneratedDefinitions::GetCallSites(WSTRING platform)
{
    std::vector<WCHAR*>* res = nullptr;

    Logger::Debug("GeneratedDefinitions::GetCallSites: platform: ", platform);

    if (platform == WStr("net461"))
    {
        res = &g_callSites_net461;
    }
    if (platform == WStr("net461_Rasp"))
    {
        res = &g_callSites_net461_Rasp;
    }
    if (platform == WStr("net6.0"))
    {
        res = &g_callSites_net6_0;
    }
    if (platform == WStr("net6.0_Rasp"))
    {
        res = &g_callSites_net6_0_Rasp;
    }
    if (platform == WStr("netstandard2.0"))
    {
        res = &g_callSites_netstandard2_0;
    }
    if (platform == WStr("netstandard2.0_Rasp"))
    {
        res = &g_callSites_netstandard2_0_Rasp;
    }
    if (platform == WStr("netcoreapp3.1"))
    {
        res = &g_callSites_netcoreapp3_1;
    }
    if (platform == WStr("netcoreapp3.1_Rasp"))
    {
        res = &g_callSites_netcoreapp3_1_Rasp;
    }

    if (res != nullptr)
    {
        Logger::Debug("GeneratedDefinitions::GetCallSites: platform: ", platform,
                      " -> Definitions returned: ", res->size());
    }
    else
    {
        Logger::Error("GeneratedDefinitions::GetCallSites: platform not found: ", platform);
    }

    return res;
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
