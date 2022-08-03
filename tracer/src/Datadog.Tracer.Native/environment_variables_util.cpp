#include "environment_variables_util.h"

namespace trace
{

bool DisableOptimizations()
{
    CheckIfTrue(shared::GetEnvironmentValue(environment::clr_disable_optimizations));
}

bool EnableInlining()
{
    ToBooleanWithDefault(shared::GetEnvironmentValue(environment::clr_enable_inlining), true);
}

bool IsNGENEnabled()
{
    ToBooleanWithDefault(shared::GetEnvironmentValue(environment::clr_enable_ngen), true);
}

bool IsDebugEnabled()
{
    CheckIfTrue(shared::GetEnvironmentValue(environment::debug_enabled));
}

bool IsDumpILRewriteEnabled()
{
    CheckIfTrue(shared::GetEnvironmentValue(environment::dump_il_rewrite_enabled));
}

bool IsTracingDisabled()
{
    CheckIfFalse(shared::GetEnvironmentValue(environment::tracing_enabled));
}

bool IsAzureAppServices()
{
    CheckIfTrue(shared::GetEnvironmentValue(environment::azure_app_services));
}

bool NeedsAgentInAAS()
{
    CheckIfTrue(shared::GetEnvironmentValue(environment::aas_needs_agent));
}

bool NeedsDogstatsdInAAS()
{
    CheckIfTrue(shared::GetEnvironmentValue(environment::aas_needs_dogstatsd));
}

bool IsTraceAnnotationEnabled()
{
    ToBooleanWithDefault(shared::GetEnvironmentValue(environment::trace_annotations_enabled), true);
}

bool IsAzureFunctionsEnabled()
{
    ToBooleanWithDefault(shared::GetEnvironmentValue(environment::azure_functions_enabled), true);
}

bool IsVersionCompatibilityEnabled()
{
    ToBooleanWithDefault(shared::GetEnvironmentValue(environment::internal_version_compatibility), true);
}

} // namespace trace