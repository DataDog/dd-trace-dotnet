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

bool IsDynamicInstrumentationManagedActivationDisabled()
{
    CheckIfFalse(GetEnvironmentValue(environment::dynamic_instrumentation_managed_activation_enabled));
}

bool IsExceptionReplayManagedActivationDisabled()
{
    CheckIfFalse(GetEnvironmentValue(environment::exception_replay_managed_activation_enabled));
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