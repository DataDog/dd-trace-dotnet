#include "fault_tolerant_environment_variables.h"
#include "environment_variables_util.h"

namespace fault_tolerant
{

bool IsFaultTolerantInstrumentationEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::fault_tolerant_instrumentation_enabled));
}

} // namespace debugger