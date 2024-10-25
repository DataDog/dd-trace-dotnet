#ifndef DD_PROFILER_CONSTANTS_H
#define DD_PROFILER_CONSTANTS_H

#include <string>

#include "environment_variables.h"
#include "logger.h"

namespace trace
{

const shared::WSTRING env_vars_to_display[]{environment::tracing_enabled,
                                    environment::debug_enabled,
                                    environment::profiler_home_path,
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
                                    environment::clr_enable_ngen,
                                    environment::dump_il_rewrite_enabled,
                                    environment::ci_visibility_enabled,
                                    environment::azure_app_services,
                                    environment::azure_app_services_app_pool_id,
                                    environment::azure_app_services_cli_telemetry_profile_value,
                                    environment::iast_enabled};

const shared::WSTRING skip_assembly_prefixes[]{
    WStr("Microsoft.AI"),
    WStr("Microsoft.ApplicationInsights"),
    WStr("Microsoft.Build"),
    WStr("Microsoft.CSharp"),
    WStr("Microsoft.Extensions"),
    WStr("Microsoft.Web.Compilation.Snapshots"),
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
    WStr("System.Numerics"),
};

const shared::WSTRING include_assemblies[]{
    WStr("Microsoft.Extensions.Logging"),
    WStr("Microsoft.Extensions.Logging.Abstractions"),
    WStr("Microsoft.Extensions.Telemetry"),
    WStr("System.Diagnostics.Process"),
    WStr("Microsoft.Extensions.Identity.Core"),
    WStr("System.Runtime.Remoting"),
};

// Note that this list should be kept in sync with the values in shared/src/Datadog.Trace.ClrProfiler.Native/util.h
// Note that you also consider adding to the SSI tracer/build/artifacts/requirements.json file
const shared::WSTRING default_exclude_assemblies[]{
    WStr("aspnet_state.exe"),
    WStr("csc.exe"),
    WStr("dd-trace"),
    WStr("dd-trace.exe"),
    WStr("devenv.exe"),
    WStr("iisexpresstray.exe"),
    WStr("Microsoft.ServiceHub.Controller.exe"),
    WStr("MSBuild.exe"),
    WStr("MsDtsSrvr.exe"),
    WStr("msvsmon.exe"),
    WStr("PerfWatson2.exe"),
    WStr("ServiceHub.DataWarehouseHost.exe"),
    WStr("ServiceHub.Host.CLR.exe"),
    WStr("ServiceHub.Host.CLR.x86.exe"),
    WStr("ServiceHub.IdentityHost.exe"),
    WStr("ServiceHub.RoslynCodeAnalysisService32.exe"),
    WStr("ServiceHub.SettingsHost.exe"),
    WStr("ServiceHub.TestWindowStoreHost.exe"),
    WStr("ServiceHub.ThreadedWaitDialog.exe"),
    WStr("ServiceHub.VSDetouredHost.exe"),
    WStr("sqlagent.exe"),
    WStr("sqlbrowser.exe"),
    WStr("sqlservr.exe"),
    WStr("VBCSCompiler.exe"),
    WStr("vsdbg"),
    WStr("vsdbg.exe"),
};

const shared::WSTRING skip_traceattribute_assembly_prefixes[]{
    WStr("System."), WStr("Microsoft."), WStr("Datadog.")};

const shared::WSTRING skip_assemblies[]{WStr("mscorlib"),
                                WStr("netstandard"),
                                WStr("System.Configuration"),
                                WStr("Microsoft.AspNetCore.Razor.Language"),
                                WStr("Microsoft.AspNetCore.Mvc.RazorPages"),
                                WStr("Anonymously Hosted DynamicMethods Assembly"),
                                WStr("Datadog.AutoInstrumentation.ManagedLoader"),
                                WStr("ISymWrapper")};

const shared::WSTRING mscorlib_assemblyName = WStr("mscorlib");
const shared::WSTRING system_private_corelib_assemblyName = WStr("System.Private.CoreLib");
const shared::WSTRING datadog_trace_clrprofiler_managed_loader_assemblyName = WStr("Datadog.Trace.ClrProfiler.Managed.Loader");

const shared::WSTRING managed_profiler_full_assembly_version =
    WStr("Datadog.Trace, Version=3.4.1.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb");

const shared::WSTRING managed_profiler_name = WStr("Datadog.Trace");
const shared::WSTRING manual_instrumentation_name = WStr("Datadog.Trace.Manual");

const shared::WSTRING nonwindows_nativemethods_type = WStr("Datadog.Trace.ClrProfiler.NativeMethods+NonWindows");
const shared::WSTRING windows_nativemethods_type = WStr("Datadog.Trace.ClrProfiler.NativeMethods+Windows");

const shared::WSTRING appsec_nonwindows_nativemethods_type = WStr("Datadog.Trace.AppSec.Waf.NativeBindings.NativeLibrary+NonWindows");
const shared::WSTRING appsec_windows_nativemethods_type = WStr("Datadog.Trace.AppSec.Waf.NativeBindings.NativeLibrary+Windows");
const shared::WSTRING profiler_nativemethods_type = WStr("Datadog.Trace.ContinuousProfiler.NativeInterop+NativeMethods");
const shared::WSTRING native_loader_nativemethods_type = WStr("Datadog.Trace.NativeLoader+NativeMethods");

const shared::WSTRING debugger_nonwindows_nativemethods_type = WStr("Datadog.Trace.Debugger.PInvoke.DebuggerNativeMethods+NonWindows");
const shared::WSTRING debugger_windows_nativemethods_type = WStr("Datadog.Trace.Debugger.PInvoke.DebuggerNativeMethods+Windows");

const shared::WSTRING fault_tolerant_nonwindows_nativemethods_type = WStr("Datadog.Trace.FaultTolerant.FaultTolerantNativeMethods+NonWindows");
const shared::WSTRING fault_tolerant_windows_nativemethods_type = WStr("Datadog.Trace.FaultTolerant.FaultTolerantNativeMethods+Windows");

const shared::WSTRING calltarget_modification_action = WStr("CallTargetModification");

const shared::WSTRING distributed_tracer_type_name = WStr("Datadog.Trace.ClrProfiler.DistributedTracer");
const shared::WSTRING calltargetbubbleexception_tracer_type_name = WStr("Datadog.Trace.ClrProfiler.CallTarget.CallTargetBubbleUpException");
const shared::WSTRING calltargetbubbleexception_tracer_function_name = WStr("IsCallTargetBubbleUpException");
const shared::WSTRING distributed_tracer_interface_name = WStr("Datadog.Trace.ClrProfiler.IDistributedTracer");
const shared::WSTRING distributed_tracer_target_method_name = WStr("__GetInstanceForProfiler__");

const shared::WSTRING instrumentation_type_name = WStr("Datadog.Trace.ClrProfiler.Instrumentation");

#ifdef _WIN32
const shared::WSTRING native_dll_filename = WStr("DATADOG.TRACER.NATIVE.DLL");
#elif MACOS
const shared::WSTRING native_dll_filename = WStr("Datadog.Tracer.Native.dylib");
#else
const shared::WSTRING native_dll_filename = WStr("Datadog.Tracer.Native.so");
#endif

const AssemblyProperty managed_profiler_assembly_property = AssemblyProperty(
    managed_profiler_name,
    new BYTE[160]{0,   36,  0,   0,   4,   128, 0,  0,   148, 0,   0,   0,   6,   2,   0,   0,   0,   36,  0,   0,
                  82,  83,  65,  49,  0,   4,   0,  0,   1,   0,   1,   0,   37,  184, 85,  200, 188, 65,  177, 212,
                  126, 119, 127, 194, 71,  57,  41, 153, 202, 111, 85,  60,  219, 3,   15,  172, 142, 59,  208, 16,
                  23,  29,  237, 153, 130, 84,  13, 152, 133, 83,  147, 95,  68,  247, 221, 88,  203, 75,  23,  251,
                  185, 38,  83,  213, 194, 220, 81, 18,  105, 104, 134, 102, 91,  49,  124, 111, 146, 121, 91,  246,
                  75,  234, 178, 64,  92,  80,  28, 138, 48,  203, 27,  49,  177, 84,  30,  214, 110, 39,  217, 130,
                  49,  105, 236, 40,  21,  176, 12, 238, 238, 204, 141, 90,  27,  244, 61,  182, 125, 41,  97,  163,
                  233, 190, 161, 57,  127, 4,   62, 192, 116, 145, 112, 150, 73,  37,  47,  85,  101, 183, 86,  197},
    160, 32772, 1)
        .WithVersion(3, 4, 1, 0);

} // namespace trace

#endif // DD_PROFILER_CONSTANTS_H