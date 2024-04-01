#include "debugger_environment_variables_util.h"
#include "environment_variables_util.h"

namespace debugger
{

bool IsDebuggerEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::debugger_enabled));
}

bool IsExceptionDebuggingEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::exception_debugging_enabled));
}

bool IsDebuggerInstrumentAllEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::internal_instrument_all_enabled));
}

} // namespace debugger