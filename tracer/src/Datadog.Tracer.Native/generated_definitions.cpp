#include "generated_definitions.h"
#include "corprof.h"
#include "../Datadog.Trace/Generated/generated_net461.h"
#include "../Datadog.Trace/Generated/generated_net6_0.h"
#include "../Datadog.Trace/Generated/generated_netcoreapp3_1.h"
#include "../Datadog.Trace/Generated/generated_netstandard2_0.h"

namespace trace
{
std::vector<CallTargetDefinition2> GeneratedDefinitions::GetCallTargets(WSTRING platform)
{
    // if (platform == WStr("net6.0")) { return GetCallTargets_Net6_0(); }

    return std::vector<CallTargetDefinition2>();
}

std::vector<WSTRING> GeneratedDefinitions::GetCallSites(WSTRING platform)
{
    if (platform == WStr("net461"))
    {
        return GetCallSites_net461();
    }
    if (platform == WStr("net461_Rasp"))
    {
        return GetCallSites_net461_Rasp();
    }
    if (platform == WStr("net6.0"))
    {
        return GetCallSites_net6_0();
    }
    if (platform == WStr("net6.0_Rasp"))
    {
        return GetCallSites_net6_0_Rasp();
    }
    if (platform == WStr("netstandard2.0"))
    {
        return GetCallSites_netstandard2_0();
    }
    if (platform == WStr("netstandard2.0_Rasp"))
    {
        return GetCallSites_netstandard2_0_Rasp();
    }
    if (platform == WStr("netcoreapp3.1"))
    {
        return GetCallSites_netcoreapp3_1();
    }
    if (platform == WStr("netcoreapp3.1_Rasp"))
    {
        return GetCallSites_netcoreapp3_1_Rasp();
    }

    // TODO -> add the corresponding platform definitions entry

    return std::vector<WSTRING>();
}


} // namespace trace
