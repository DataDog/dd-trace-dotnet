#ifndef DD_CLR_PROFILER_DEBUGGER_ENVIRONMENT_VARIABLES_H_
#define DD_CLR_PROFILER_DEBUGGER_ENVIRONMENT_VARIABLES_H_

#include "../../../shared/src/native-src/string.h" // NOLINT

using namespace shared;

namespace debugger
{
namespace environment
{

    // Determine whether to enter "instrument all" mode where the Debugger instrumentation
    // is applied to every jit compiled method. Only useful for testing purposes. Default is false.
    const WSTRING internal_instrument_all_enabled = WStr("DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL");

    // Determines if the Dynamic Instrumentation (aka live debugger) is enabled.
    const WSTRING debugger_enabled = WStr("DD_DYNAMIC_INSTRUMENTATION_ENABLED");

    // Determines if the Exception Replay product is enabled.
    const WSTRING exception_debugging_enabled = WStr("DD_EXCEPTION_DEBUGGING_ENABLED"); // Old name
    const WSTRING exception_replay_enabled = WStr("DD_EXCEPTION_REPLAY_ENABLED");

} // namespace environment
} // namespace debugger

#endif
