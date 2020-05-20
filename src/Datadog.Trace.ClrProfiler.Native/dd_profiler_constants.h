#pragma once
#include <string>

#include "environment_variables.h"
#include "logging.h"

namespace trace {

inline WSTRING env_vars_to_display[]{
    environment::tracing_enabled,
    environment::debug_enabled,
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
    environment::clr_disable_optimizations,
    environment::domain_neutral_instrumentation,
    environment::azure_app_services,
    environment::azure_app_services_app_pool_id,
    environment::azure_app_services_cli_telemetry_profile_value};

 inline WSTRING skip_assembly_prefixes[]{
    "Datadog.Trace"_W,
    "MessagePack"_W,
    "Microsoft.AI"_W,
    "Microsoft.ApplicationInsights"_W,
    "Microsoft.Build"_W,
    "Microsoft.CSharp"_W,
    "Microsoft.Extensions"_W,
    "Sigil"_W,
    "System.Core"_W,
    "System.Console"_W,
    "System.Collections"_W,
    "System.ComponentModel"_W,
    "System.Diagnostics"_W,
    "System.IO"_W,
    "System.Runtime"_W,
    "System.Text"_W,
    "System.Threading"_W,
    "System.Xml"_W,
    "Newtonsoft"_W,};

  inline WSTRING skip_assemblies[]{
      "mscorlib"_W,
      "netstandard"_W,
      "System.Configuration"_W,
      "Microsoft.AspNetCore.Razor.Language"_W,
      "Microsoft.AspNetCore.Mvc.RazorPages"_W,
      "Anonymously Hosted DynamicMethods Assembly"_W,
      "ISymWrapper"_W};

}  // namespace trace
