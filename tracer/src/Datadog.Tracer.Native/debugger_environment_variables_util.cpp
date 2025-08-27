#include "debugger_environment_variables_util.h"
#include "environment_variables_util.h"

namespace debugger
{

bool IsDynamicInstrumentationEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::dynamic_instrumentation_enabled));
}

bool IsExceptionReplayEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::exception_replay_enabled));
}

bool IsDynamicInstrumentationStableConfigDisabled()
{
    CheckIfFalse(GetEnvironmentValue(environment::dynamic_instrumentation_stable_config_enabled));
}

bool IsExceptionReplayStableConfigDisabled()
{
    CheckIfFalse(GetEnvironmentValue(environment::exception_replay_stable_config_enabled));
}

bool IsDebuggerInstrumentAllEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::internal_instrument_all_enabled));
}

bool IsDebuggerInstrumentAllLinesEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::internal_instrument_all_lines_enabled));
}

} // namespace debugger