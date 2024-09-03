#include "debugger_environment_variables_util.h"
#include "environment_variables_util.h"

namespace debugger
{

bool IsDebuggerEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::debugger_enabled));
}

bool IsExceptionReplayEnabled()
{
    static int sValue = -1;
    if (sValue == -1)
    {
        const auto old_exception_replay_flag = GetEnvironmentValue(environment::exception_debugging_enabled);
        const auto new_exception_replay_flag = GetEnvironmentValue(environment::exception_replay_enabled);
        sValue = (IsTrue(old_exception_replay_flag) || IsTrue(new_exception_replay_flag)) ? 1 : 0;
    }
    return sValue == 1;
}

bool IsDebuggerInstrumentAllEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::internal_instrument_all_enabled));
}

} // namespace debugger