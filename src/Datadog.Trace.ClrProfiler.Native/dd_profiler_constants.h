#ifndef DD_PROFILER_CONSTANTS_H
#define DD_PROFILER_CONSTANTS_H

#include <string>

#include "environment_variables.h"
#include "logging.h"

namespace trace {

  inline WSTRING env_vars_to_display[]{
    environment::tracing_enabled,
    environment::debug_enabled,
    environment::calltarget_enabled,
    environment::profiler_home_path,
    environment::integrations_path,
    environment::include_process_names,
    environment::exclude_process_names,
    environment::agent_host,
    environment::agent_port,
    environment::env,
    environment::service_name,
    environment::service_version,
    environment::disabled_integrations,
    environment::log_path,
    environment::log_directory,
    environment::clr_disable_optimizations,
    environment::clr_enable_inlining,
    environment::domain_neutral_instrumentation,
    environment::dump_il_rewrite_enabled,
    environment::netstandard_enabled,
    environment::azure_app_services,
    environment::azure_app_services_app_pool_id,
    environment::azure_app_services_cli_telemetry_profile_value};

  inline WSTRING skip_assembly_prefixes[]{
    _LU("Datadog.Trace"),
    _LU("MessagePack"),
    _LU("Microsoft.AI"),
    _LU("Microsoft.ApplicationInsights"),
    _LU("Microsoft.Build"),
    _LU("Microsoft.CSharp"),
    _LU("Microsoft.Extensions"),
    _LU("Microsoft.Web.Compilation.Snapshots"),
    _LU("Sigil"),
    _LU("System.Core"),
    _LU("System.Console"),
    _LU("System.Collections"),
    _LU("System.ComponentModel"),
    _LU("System.Diagnostics"),
    _LU("System.Drawing"),
    _LU("System.EnterpriseServices"),
    _LU("System.IO"),
    _LU("System.Runtime"),
    _LU("System.Text"),
    _LU("System.Threading"),
    _LU("System.Xml"),
    _LU("Newtonsoft"),
  };

  inline WSTRING skip_assemblies[]{
      _LU("mscorlib"),
      _LU("netstandard"),
      _LU("System.Configuration"),
      _LU("Microsoft.AspNetCore.Razor.Language"),
      _LU("Microsoft.AspNetCore.Mvc.RazorPages"),
      _LU("Anonymously Hosted DynamicMethods Assembly"),
      _LU("ISymWrapper")
  };

  inline WSTRING managed_profiler_full_assembly_version = _LU("Datadog.Trace.ClrProfiler.Managed, Version=1.24.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb");

  inline WSTRING calltarget_modification_action = _LU("CallTargetModification");

}  // namespace trace

#endif  // DD_PROFILER_CONSTANTS_H