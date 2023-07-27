#ifndef DD_CLR_PROFILER_FAULT_TOLERANT_ENVIRONMENT_VARIABLES_H_
#define DD_CLR_PROFILER_FAULT_TOLERANT_ENVIRONMENT_VARIABLES_H_

#include "../../../shared/src/native-src/string.h" // NOLINT

using namespace shared;

namespace fault_tolerant
{
namespace environment
{
    // Determines if the Fault-Tolerant Instrumentation is enabled.
    const WSTRING fault_tolerant_instrumentation_enabled = WStr("DD_INTERNAL_FAULT_TOLERANT_INSTRUMENTATION_ENABLED");

} // namespace environment
} // namespace fault_tolerant

#endif
