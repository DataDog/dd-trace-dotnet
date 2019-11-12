#ifndef DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_H_
#define DD_CLR_PROFILER_ENVIRONMENT_VARIABLES_H_

#include "string.h"  // NOLINT

namespace trace {
namespace environment {

// Sets whether the profiler is enabled. Default is true.
// Setting this to false disabled the profiler entirely.
const WSTRING tracing_enabled = "DD_TRACE_ENABLED"_W;

// Sets whether debug mode is enabled. Default is false.
const WSTRING debug_enabled = "DD_TRACE_DEBUG"_W;

// Sets the paths to integration definition JSON files.
// Supports multiple values separated with semi-colons, for example:
// "C:\Program Files\Datadog .NET Tracer\integrations.json;D:\temp\test_integrations.json"
const WSTRING integrations_path = "DD_INTEGRATIONS"_W;

// Sets the path to the profiler's home directory, for example:
// "C:\Program Files\Datadog .NET Tracer\" or "/opt/datadog/"
const WSTRING profiler_home_path = "DD_DOTNET_TRACER_HOME"_W;

// Sets the filename of executables the profiler can attach to.
// If not defined (default), the profiler will attach to any process.
// Supports multiple values separated with semi-colons, for example:
// "MyApp.exe;dotnet.exe"
const WSTRING include_process_names = "DD_PROFILER_PROCESSES"_W;

// Sets the filename of executables the profiler cannot attach to.
// If not defined (default), the profiler will attach to any process.
// Supports multiple values separated with semi-colons, for example:
// "MyApp.exe;dotnet.exe"
const WSTRING exclude_process_names = "DD_PROFILER_EXCLUDE_PROCESSES"_W;

// Sets the Agent's host. Default is localhost.
const WSTRING agent_host = "DD_AGENT_HOST"_W;

// Sets the Agent's port. Default is 8126.
const WSTRING agent_port = "DD_TRACE_AGENT_PORT"_W;

// Sets the "env" tag for every span.
const WSTRING env = "DD_ENV"_W;

// Sets the default service name for every span.
// If not set, Tracer will try to determine service name automatically
// from application name (e.g. entry assembly or IIS application name).
const WSTRING service_name = "DD_SERVICE_NAME"_W;

// Sets a list of integrations to disable. All other integrations will remain
// enabled. If not set (default), all integrations are enabled. Supports
// multiple values separated with semi-colons, for example:
// "ElasticsearchNet;AspNetWebApi2"
const WSTRING disabled_integrations = "DD_DISABLED_INTEGRATIONS"_W;

// Sets the path for the profiler's log file.
// If not set, default is
// "%ProgramData%"\Datadog .NET Tracer\logs\dotnet-profiler.log" on Windows or
// "/var/log/datadog/dotnet-profiler.log" on Linux.
const WSTRING log_path = "DD_TRACE_LOG_PATH"_W;

// Sets whether to disable all optimizations.
// Default is false on Windows.
// Default is true on Linux to work around a bug in the JIT compiler.
// https://github.com/dotnet/coreclr/issues/24676
// https://github.com/dotnet/coreclr/issues/12468
const WSTRING clr_disable_optimizations = "DD_CLR_DISABLE_OPTIMIZATIONS"_W;

// Sets whether to opt in to beta features
// Default is false
const WSTRING beta_features_enabled = "DD_DOTNET_BETA_FEATURES_ENABLED"_W;

}  // namespace environment
}  // namespace trace

#endif