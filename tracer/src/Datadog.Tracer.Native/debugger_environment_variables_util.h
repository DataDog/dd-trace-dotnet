#ifndef DD_CLR_PROFILER_DEBUGGER_ENVIRONMENT_VARIABLES_UTIL_H_
#define DD_CLR_PROFILER_DEBUGGER_ENVIRONMENT_VARIABLES_UTIL_H_

#include "../../../shared/src/native-src/util.h"
#include "debugger_environment_variables.h"
#include "string.h"

namespace debugger
{

bool IsDebuggerEnabled();
bool IsExceptionReplayEnabled();
bool IsDebuggerInstrumentAllEnabled();
bool IsDebuggerInstrumentAllLinesEnabled();

} // namespace debugger

#endif // DD_CLR_PROFILER_DEBUGGER_ENVIRONMENT_VARIABLES_UTIL_H_