#include "debugger_environment_variables_util.h"
#include "environment_variables_util.h"

namespace debugger
{

bool IsDynamicInstrumentationEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::debugger_enabled));
}

bool IsExceptionReplayEnabled()
{
    static int sErEnabledValue = -1;
    if (sErEnabledValue == -1)
    {
        const auto old_exception_replay_flag = GetEnvironmentValue(environment::exception_debugging_enabled);
        const auto new_exception_replay_flag = GetEnvironmentValue(environment::exception_replay_enabled);
        sErEnabledValue = (IsFalse(old_exception_replay_flag) || IsFalse(new_exception_replay_flag)) ? 0 : 1;
    }
    return sErEnabledValue == 1;
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