#include "debugger_environment_variables_util.h"
#include "environment_variables_util.h"

namespace debugger
{

bool IsDynamicInstrumentationDisabled()
{
    CheckIfFalse(GetEnvironmentValue(environment::debugger_enabled));
}

bool IsExceptionReplayDisabled()
{
    static int sErDisabledValue = -1;
    if (sErDisabledValue == -1)
    {
        const auto old_exception_replay_flag = GetEnvironmentValue(environment::exception_debugging_enabled);
        const auto new_exception_replay_flag = GetEnvironmentValue(environment::exception_replay_enabled);
        sErDisabledValue = (IsFalse(old_exception_replay_flag) || IsFalse(new_exception_replay_flag)) ? 1 : 0;
    }
    return sErDisabledValue == 1;
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