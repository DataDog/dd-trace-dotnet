#include "debugger_environment_variables_util.h"
#include "environment_variables_util.h"

#include <exception>

namespace debugger
{

namespace
{
constexpr int DefaultFlowRecorderMaxMethods = 10000;
}

bool IsDynamicInstrumentationEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::dynamic_instrumentation_enabled));
}

bool IsExceptionReplayEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::exception_replay_enabled));
}

bool IsDynamicInstrumentationManagedActivationDisabled()
{
    CheckIfFalse(GetEnvironmentValue(environment::dynamic_instrumentation_managed_activation_enabled));
}

bool IsExceptionReplayManagedActivationDisabled()
{
    CheckIfFalse(GetEnvironmentValue(environment::exception_replay_managed_activation_enabled));
}

bool IsDebuggerInstrumentAllEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::internal_instrument_all_enabled));
}

bool IsDebuggerInstrumentAllLinesEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::internal_instrument_all_lines_enabled));
}

bool IsDebuggerFlowRecorderEnabled()
{
    CheckIfTrue(GetEnvironmentValue(environment::internal_flow_recorder_enabled));
}

int GetDebuggerFlowRecorderMaxMethods()
{
    const auto value = shared::GetEnvironmentValue(environment::internal_flow_recorder_max_methods);
    if (value.empty())
    {
        return DefaultFlowRecorderMaxMethods;
    }

    try
    {
        const auto parsed = std::stoi(value);
        return parsed > 0 ? parsed : DefaultFlowRecorderMaxMethods;
    }
    catch (const std::exception&)
    {
        return DefaultFlowRecorderMaxMethods;
    }
}

WSTRING GetDebuggerFlowRecorderCaptureValueMethods()
{
    return shared::GetEnvironmentValue(environment::internal_flow_recorder_capture_value_methods);
}

WSTRING GetDebuggerFlowRecorderCaptureValues()
{
    return shared::GetEnvironmentValue(environment::internal_flow_recorder_capture_values);
}

WSTRING GetDebuggerFlowRecorderExcludeMethods()
{
    return shared::GetEnvironmentValue(environment::internal_flow_recorder_exclude_methods);
}

WSTRING GetDebuggerFlowRecorderRewriteMode()
{
    return shared::GetEnvironmentValue(environment::internal_flow_recorder_rewrite_mode);
}

bool IsDebuggerFlowRecorderEntryOnlyRewriteMode()
{
    return GetDebuggerFlowRecorderRewriteMode() == WStr("entry-only");
}

} // namespace debugger