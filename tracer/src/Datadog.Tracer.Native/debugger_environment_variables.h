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

} // namespace environment
} // namespace debugger

#endif
