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
    const WSTRING internal_instrument_all_lines_enabled = WStr("DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL_LINES");
    const WSTRING internal_instrument_all_lines_path = WStr("DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL_LINES_PATH");
    const WSTRING internal_flow_recorder_enabled = WStr("DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ENABLED");
    const WSTRING internal_flow_recorder_output_path = WStr("DD_INTERNAL_DEBUGGER_FLOW_RECORDER_OUTPUT_PATH");
    const WSTRING internal_flow_recorder_max_methods = WStr("DD_INTERNAL_DEBUGGER_FLOW_RECORDER_MAX_METHODS");
    const WSTRING internal_flow_recorder_capture_values = WStr("DD_INTERNAL_DEBUGGER_FLOW_RECORDER_CAPTURE_VALUES");
    const WSTRING internal_flow_recorder_capture_value_methods = WStr("DD_INTERNAL_DEBUGGER_FLOW_RECORDER_CAPTURE_VALUE_METHODS");
    const WSTRING internal_flow_recorder_exclude_methods = WStr("DD_INTERNAL_DEBUGGER_FLOW_RECORDER_EXCLUDE_METHODS");
    const WSTRING internal_flow_recorder_rewrite_mode = WStr("DD_INTERNAL_DEBUGGER_FLOW_RECORDER_REWRITE_MODE");

    // Determines if the Dynamic Instrumentation (aka live debugger) product is enabled.
    const WSTRING dynamic_instrumentation_enabled = WStr("DD_DYNAMIC_INSTRUMENTATION_ENABLED");

    // Determines if the Exception Replay product is enabled.
    const WSTRING exception_replay_enabled = WStr("DD_EXCEPTION_REPLAY_ENABLED");

    // Determines if Dynamic Instrumentation should be controlled by managed activation
    const WSTRING dynamic_instrumentation_managed_activation_enabled = WStr("DD_DYNAMIC_INSTRUMENTATION_MANAGED_ACTIVATION_ENABLED");

    // Determines if Exception Replay should be controlled by managed activation
    const WSTRING exception_replay_managed_activation_enabled = WStr("DD_EXCEPTION_REPLAY_MANAGED_ACTIVATION_ENABLED");

} // namespace environment
} // namespace debugger

#endif
