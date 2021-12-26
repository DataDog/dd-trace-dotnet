#include "environment_variables_util.h"

namespace trace
{

bool DisableOptimizations()
{
    CheckIfTrue(GetEnvironmentValue(environment::clr_disable_optimizations));
}

bool EnableInlining()
{
    ToBooleanWithDefault(GetEnvironmentValue(environment::clr_enable_inlining), true);
}

bool IsNGENEnabled()
{
    ToBooleanWithDefault(GetEnvironmentValue(environment::clr_enable_ngen), true);
}

bool IsDebugEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::debug_enabled));
}

bool IsDumpILRewriteEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::dump_il_rewrite_enabled));
}

bool IsTracingDisabled()
{
    CheckIfFalse(GetEnvironmentValue(environment::tracing_enabled));
}

bool IsAzureAppServices()
{
    CheckIfTrue(GetEnvironmentValue(environment::azure_app_services));
}

bool NeedsAgentInAAS()
{
    CheckIfTrue(GetEnvironmentValue(environment::aas_needs_agent));
}

bool NeedsDogstatsdInAAS()
{
    CheckIfTrue(GetEnvironmentValue(environment::aas_needs_dogstatsd));
}

bool IsAzureFunctionsEnabled()
{
    ToBooleanWithDefault(GetEnvironmentValue(environment::azure_functions_enabled), true);
}

bool IsVersionCompatibilityEnabled()
{
    ToBooleanWithDefault(GetEnvironmentValue(environment::internal_version_compatibility), true);
}

} // namespace trace