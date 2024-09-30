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

bool IsAzureAppServices()
{
    CheckIfTrue(shared::GetEnvironmentValue(environment::azure_app_services));
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

bool IsIastEnabled()
{
    ToBooleanWithDefault(shared::GetEnvironmentValue(environment::iast_enabled), false);
}

bool IsAsmSettingEnabled()
{
    ToBooleanWithDefault(shared::GetEnvironmentValue(environment::asm_enabled), false);
}

bool IsRaspSettingEnabled()
{
    ToBooleanWithDefault(shared::GetEnvironmentValue(environment::rasp_enabled), true);
}

bool IsRaspEnabled()
{
    return IsRaspSettingEnabled() && IsAsmSettingEnabled();
}

bool IsEditAndContinueEnabledCore()
{
    ToBooleanWithDefault(shared::GetEnvironmentValue(environment::ide_edit_and_continue_core), false);
}
bool IsEditAndContinueEnabledNetFx()
{
    ToBooleanWithDefault(shared::GetEnvironmentValue(environment::ide_edit_and_continue_netfx), false);
}
bool IsEditAndContinueEnabled()
{
    return IsEditAndContinueEnabledCore() || IsEditAndContinueEnabledNetFx();
}

} // namespace trace