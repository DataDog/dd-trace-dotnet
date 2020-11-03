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
const WSTRING service_name = "DD_SERVICE"_W;

// Sets the "service_version" tag for every span that belong to the root service (and not an external service).
const WSTRING service_version = "DD_VERSION"_W;

// Sets a list of integrations to disable. All other integrations will remain
// enabled. If not set (default), all integrations are enabled. Supports
// multiple values separated with semi-colons, for example:
// "ElasticsearchNet;AspNetWebApi2"
const WSTRING disabled_integrations = "DD_DISABLED_INTEGRATIONS"_W;

// Sets the path for the profiler's log file.
// If not set, default is
// "%ProgramData%"\Datadog .NET Tracer\logs\dotnet-tracer-native.log" on Windows or
// "/var/log/datadog/dotnet/dotnet-tracer-native.log" on Linux.
const WSTRING log_path = "DD_TRACE_LOG_PATH"_W;

// Sets whether to disable all JIT optimizations.
// Default value is false (do not disable all optimizations).
// https://github.com/dotnet/coreclr/issues/24676
// https://github.com/dotnet/coreclr/issues/12468
const WSTRING clr_disable_optimizations = "DD_CLR_DISABLE_OPTIMIZATIONS"_W;

// Sets whether to intercept method calls when the caller method is inside a
// domain-neutral assembly. This is dangerous because the integration assembly
// Datadog.Trace.ClrProfiler.Managed.dll must also be loaded domain-neutral,
// otherwise a sharing violation (HRESULT 0x80131401) may occur. This setting should only be
// enabled when there is only one AppDomain or, when hosting applications in IIS,
// the user can guarantee that all Application Pools on the system have at most
// one application.
// Default is false. Only used in .NET Framework 4.5 and 4.5.1.
// https://github.com/DataDog/dd-trace-dotnet/pull/671
const WSTRING domain_neutral_instrumentation = "DD_TRACE_DOMAIN_NEUTRAL_INSTRUMENTATION"_W;

// Indicates whether the profiler is running in the context
// of Azure App Services
const WSTRING azure_app_services = "DD_AZURE_APP_SERVICES"_W;

// The app_pool_id in the context of azure app services
const WSTRING azure_app_services_app_pool_id = "APP_POOL_ID"_W;

// The DOTNET_CLI_TELEMETRY_PROFILE in the context of azure app services
const WSTRING azure_app_services_cli_telemetry_profile_value =
    "DOTNET_CLI_TELEMETRY_PROFILE"_W;

// Determine whether to instrument calls into netstandard.dll.
// Default to false for now to avoid the unexpected overhead of additional spans.
const WSTRING netstandard_enabled = "DD_TRACE_NETSTANDARD_ENABLED"_W;

// Enable the profiler to dump the IL original code and modification to the log.
const WSTRING dump_il_rewrite_enabled = "DD_DUMP_ILREWRITE_ENABLED"_W;

}  // namespace environment
}  // namespace trace

#endif
