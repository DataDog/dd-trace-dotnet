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
    WStr("Datadog.Trace"),
    WStr("MessagePack"),
    WStr("Microsoft.AI"),
    WStr("Microsoft.ApplicationInsights"),
    WStr("Microsoft.Build"),
    WStr("Microsoft.CSharp"),
    WStr("Microsoft.Extensions"),
    WStr("Microsoft.Web.Compilation.Snapshots"),
    WStr("Sigil"),
    WStr("System.Core"),
    WStr("System.Console"),
    WStr("System.Collections"),
    WStr("System.ComponentModel"),
    WStr("System.Diagnostics"),
    WStr("System.Drawing"),
    WStr("System.EnterpriseServices"),
    WStr("System.IO"),
    WStr("System.Runtime"),
    WStr("System.Text"),
    WStr("System.Threading"),
    WStr("System.Xml"),
    WStr("Newtonsoft"),
  };

  inline WSTRING skip_assemblies[]{
      WStr("mscorlib"),
      WStr("netstandard"),
      WStr("System.Configuration"),
      WStr("Microsoft.AspNetCore.Razor.Language"),
      WStr("Microsoft.AspNetCore.Mvc.RazorPages"),
      WStr("Anonymously Hosted DynamicMethods Assembly"),
      WStr("ISymWrapper")
  };

  inline WSTRING managed_profiler_full_assembly_version = WStr("Datadog.Trace.ClrProfiler.Managed, Version=1.26.3.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb");

  inline WSTRING calltarget_modification_action = WStr("CallTargetModification");

}  // namespace trace

#endif  // DD_PROFILER_CONSTANTS_H