#include "debugger_environment_variables_util.h"
#include "environment_variables_util.h"

namespace debugger
{

bool IsDebuggerEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::debugger_enabled));
}

bool IsDebuggerInstrumentAllEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::internal_instrument_all_enabled));
}

bool IsFaultTolerantInstrumentationEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::fault_tolerant_instrumentation_enabled));
}

} // namespace debugger