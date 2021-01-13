#include "cor_profiler.h"

#include <corprof.h>
#include <string>
#include "corhlpr.h"

#include "version.h"
#include "clr_helpers.h"
#include "dd_profiler_constants.h"
#include "dllmain.h"
#include "environment_variables.h"
#include "il_rewriter.h"
#include "il_rewriter_wrapper.h"
#include "integration_loader.h"
#include "logging.h"
#include "metadata_builder.h"
#include "module_metadata.h"
#include "pal.h"
#include "sig_helpers.h"
#include "resource.h"
#include "util.h"

#ifdef MACOS
#include <mach-o/getsect.h>
#include <mach-o/dyld.h>
#endif

namespace trace {

CorProfiler* profiler = nullptr;

//
// ICorProfilerCallback methods
//
HRESULT STDMETHODCALLTYPE
CorProfiler::Initialize(IUnknown* cor_profiler_info_unknown) {
  // check if debug mode is enabled
  const auto debug_enabled_value =
      GetEnvironmentValue(environment::debug_enabled);

  if (debug_enabled_value == "1"_W || debug_enabled_value == "true"_W) {
    debug_logging_enabled = true;
  }

  // check if dump il rewrite is enabled
  const auto dump_il_rewrite_enabled_value = 
      GetEnvironmentValue(environment::dump_il_rewrite_enabled);

  if (dump_il_rewrite_enabled_value == "1"_W ||
      dump_il_rewrite_enabled_value == "true"_W) {
    dump_il_rewrite_enabled = true;
  }

  CorProfilerBase::Initialize(cor_profiler_info_unknown);

  // check if tracing is completely disabled
  const WSTRING tracing_enabled =
      GetEnvironmentValue(environment::tracing_enabled);

  if (tracing_enabled == "0"_W || tracing_enabled == "false"_W) {
    Info("DATADOG TRACER DIAGNOSTICS - Profiler disabled in ", environment::tracing_enabled);
    return E_FAIL;
  }

  const auto process_name = GetCurrentProcessName();
  const auto include_process_names =
      GetEnvironmentValues(environment::include_process_names);

  // if there is a process inclusion list, attach profiler only if this
  // process's name is on the list
  if (!include_process_names.empty() &&
      !Contains(include_process_names, process_name)) {
    Info("DATADOG TRACER DIAGNOSTICS - Profiler disabled: ", process_name, " not found in ",
         environment::include_process_names, ".");
    return E_FAIL;
  }

  const auto exclude_process_names =
      GetEnvironmentValues(environment::exclude_process_names);

  // attach profiler only if this process's name is NOT on the list
  if (Contains(exclude_process_names, process_name)) {
    Info("DATADOG TRACER DIAGNOSTICS - Profiler disabled: ", process_name, " found in ",
         environment::exclude_process_names, ".");
    return E_FAIL;
  }

  // get Profiler interface
  HRESULT hr = cor_profiler_info_unknown->QueryInterface<ICorProfilerInfo4>(
      &this->info_);
  if (FAILED(hr)) {
    Warn("DATADOG TRACER DIAGNOSTICS - Failed to attach profiler: interface ICorProfilerInfo4 not found.");
    return E_FAIL;
  }

  Info("Environment variables:");

  for (auto&& env_var : env_vars_to_display) {
    WSTRING env_var_value = GetEnvironmentValue(env_var);
    if (debug_logging_enabled || !env_var_value.empty()) {
      Info("  ", env_var, "=", env_var_value);
    }
  }

  const WSTRING azure_app_services_value =
      GetEnvironmentValue(environment::azure_app_services);

  if (azure_app_services_value == "1"_W) {
    Info("Profiler is operating within Azure App Services context.");
    in_azure_app_services = true;

    const auto app_pool_id_value =
        GetEnvironmentValue(environment::azure_app_services_app_pool_id);

    if (app_pool_id_value.size() > 1 && app_pool_id_value.at(0) == '~') {
      Info("DATADOG TRACER DIAGNOSTICS - Profiler disabled: ", environment::azure_app_services_app_pool_id,
           " ", app_pool_id_value,
           " is recognized as an Azure App Services infrastructure process.");
      return E_FAIL;
    }

    const auto cli_telemetry_profile_value = GetEnvironmentValue(
        environment::azure_app_services_cli_telemetry_profile_value);

    if (cli_telemetry_profile_value == "AzureKudu"_W) {
      Info("DATADOG TRACER DIAGNOSTICS - Profiler disabled: ", app_pool_id_value,
           " is recognized as Kudu, an Azure App Services reserved process.");
      return E_FAIL;
    }
  }

  // get path to integration definition JSON files
  const WSTRING integrations_paths =
      GetEnvironmentValue(environment::integrations_path);

  if (integrations_paths.empty()) {
    Warn("DATADOG TRACER DIAGNOSTICS - Profiler disabled: ", environment::integrations_path,
         " environment variable not set.");
    return E_FAIL;
  }

  const auto is_calltarget_enabled = IsCallTargetEnabled();

  // Initialize ReJIT handler and define the Rewriter Callback
  if (is_calltarget_enabled) {
      rejit_handler = new RejitHandler(this->info_, [this](RejitHandlerModule* mod, RejitHandlerModuleMethod* method) {
          return this->CallTarget_RewriterCallback(mod, method);
      });
  } else {
      rejit_handler = nullptr;
  }

  // load all available integrations from JSON files
  const std::vector<Integration> all_integrations =
      LoadIntegrationsFromEnvironment();

  // get list of disabled integration names
  const std::vector<WSTRING> disabled_integration_names =
      GetEnvironmentValues(environment::disabled_integrations);

  // remove disabled integrations
  const std::vector<Integration> integrations =
      FilterIntegrationsByName(all_integrations, disabled_integration_names);

  integration_methods_ =
      FlattenIntegrations(integrations, is_calltarget_enabled);

    // check if there are any enabled integrations left
  if (integration_methods_.empty()) {
    Warn("DATADOG TRACER DIAGNOSTICS - Profiler disabled: no enabled integrations found.");
    return E_FAIL;
  } else {
    Debug("Number of Integrations loaded: ", integration_methods_.size());
  }

  const WSTRING netstandard_enabled =
      GetEnvironmentValue(environment::netstandard_enabled);

  // temporarily skip the calls into netstandard.dll that were added in
  // https://github.com/DataDog/dd-trace-dotnet/pull/753.
  // users can opt-in to the additional instrumentation by setting environment
  // variable DD_TRACE_NETSTANDARD_ENABLED
  if (netstandard_enabled != "1"_W && netstandard_enabled != "true"_W) {
    integration_methods_ = FilterIntegrationsByTargetAssemblyName(
        integration_methods_, {"netstandard"_W});
  }

  DWORD event_mask = COR_PRF_MONITOR_JIT_COMPILATION |
                     COR_PRF_DISABLE_TRANSPARENCY_CHECKS_UNDER_FULL_TRUST |
                     COR_PRF_MONITOR_MODULE_LOADS |
                     COR_PRF_MONITOR_ASSEMBLY_LOADS |
                     COR_PRF_DISABLE_ALL_NGEN_IMAGES;

  if (is_calltarget_enabled) {
    Info("CallTarget instrumentation is enabled.");
    event_mask |= COR_PRF_ENABLE_REJIT;
  } else {
    Info("CallTarget instrumentation is disabled.");
  }
  
  if (!EnableInlining()) {
    Info("JIT Inlining is disabled.");
    event_mask |= COR_PRF_DISABLE_INLINING;
  } else {
    Info("JIT Inlining is enabled.");
  }

  if (DisableOptimizations()) {
    Info("Disabling all code optimizations.");
    event_mask |= COR_PRF_DISABLE_OPTIMIZATIONS;
  }

  const WSTRING domain_neutral_instrumentation =
      GetEnvironmentValue(environment::domain_neutral_instrumentation);

  if (domain_neutral_instrumentation == "1"_W || domain_neutral_instrumentation == "true"_W) {
    instrument_domain_neutral_assemblies = true;
  }

  // set event mask to subscribe to events and disable NGEN images
  // get ICorProfilerInfo6 for net452+
  ICorProfilerInfo6* info6;
  hr = cor_profiler_info_unknown->QueryInterface<ICorProfilerInfo6>(&info6);

  if (SUCCEEDED(hr)) {
    Debug("Interface ICorProfilerInfo6 found.");
    hr = info6->SetEventMask2(event_mask, COR_PRF_HIGH_ADD_ASSEMBLY_REFERENCES);

    if (instrument_domain_neutral_assemblies) {
      Info("Note: The ", environment::domain_neutral_instrumentation, " environment variable is not needed when running on .NET Framework 4.5.2 or higher, and will be ignored.");
    }
  } else {
    hr = this->info_->SetEventMask(event_mask);

    if (instrument_domain_neutral_assemblies) {
      Info("Detected environment variable ", environment::domain_neutral_instrumentation,
          "=", domain_neutral_instrumentation);
      Info("Enabling automatic instrumentation of methods called from domain-neutral assemblies. ",
          "Please ensure that there is only one AppDomain or, if applications are being hosted in IIS, ",
          "ensure that all Application Pools have at most one application each. ",
          "Otherwise, a sharing violation (HRESULT 0x80131401) may occur.");
    }
  }
  if (FAILED(hr)) {
    Warn("DATADOG TRACER DIAGNOSTICS - Failed to attach profiler: unable to set event mask.");
    return E_FAIL;
  }

  runtime_information_ = GetRuntimeInformation(this->info_);
  if (process_name == "w3wp.exe"_W  ||
      process_name == "iisexpress.exe"_W) {
    is_desktop_iis = runtime_information_.is_desktop();
  }

  // writing opcodes vector for the IL dumper
#define OPDEF(c, s, pop, push, args, type, l, s1, s2, flow) \
  opcodes_names.push_back(s);
#include "opcode.def"
#undef OPDEF
  opcodes_names.push_back("(count)"); // CEE_COUNT
  opcodes_names.push_back("->"); // CEE_SWITCH_ARG

  // we're in!
  Info("Profiler attached.");
  this->info_->AddRef();
  is_attached_.store(true);
  profiler = this;
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::AssemblyLoadFinished(AssemblyID assembly_id,
    HRESULT hr_status) {
  if (FAILED(hr_status)) {
    // if assembly failed to load, skip it entirely,
    // otherwise we can crash the process if module is not valid
    CorProfilerBase::AssemblyLoadFinished(assembly_id, hr_status);
    return S_OK;
  }

  if (!is_attached_) {
    return S_OK;
  }

  if (debug_logging_enabled) {
    Debug("AssemblyLoadFinished: ", assembly_id, " ", hr_status);
  }

  // keep this lock until we are done using the module,
  // to prevent it from unloading while in use
  std::lock_guard<std::mutex> guard(module_id_to_info_map_lock_);

  // double check if is_attached_ has changed to avoid possible race condition with shutdown function
  if (!is_attached_) {
    return S_OK;
  }

  const auto assembly_info = GetAssemblyInfo(this->info_, assembly_id);
  if (!assembly_info.IsValid()) {
    return S_OK;
  }

  ComPtr<IUnknown> metadata_interfaces;
  auto hr = this->info_->GetModuleMetaData(assembly_info.manifest_module_id, ofRead | ofWrite,
                                           IID_IMetaDataImport2,
                                           metadata_interfaces.GetAddressOf());

  if (FAILED(hr)) {
    Warn("AssemblyLoadFinished failed to get metadata interface for module id ", assembly_info.manifest_module_id,
         " from assembly ", assembly_info.name);
    return S_OK;
  }

  // Get the IMetaDataAssemblyImport interface to get metadata from the managed assembly
  const auto assembly_import = metadata_interfaces.As<IMetaDataAssemblyImport>(
      IID_IMetaDataAssemblyImport);
  const auto assembly_metadata = GetAssemblyImportMetadata(assembly_import);

  if (debug_logging_enabled) {
    Debug("AssemblyLoadFinished: AssemblyName=", assembly_info.name, " AssemblyVersion=", assembly_metadata.version.str());
  }

  if (assembly_info.name == "Datadog.Trace.ClrProfiler.Managed"_W) {
    // Configure a version string to compare with the profiler version
    std::stringstream ss;
    ss << assembly_metadata.version.major << '.'
       << assembly_metadata.version.minor << '.'
       << assembly_metadata.version.build;

    auto assembly_version = ToWSTRING(ss.str());

    // Check that Major.Minor.Build match the profiler version
    if (assembly_version == ToWSTRING(PROFILER_VERSION)) {
      Info("AssemblyLoadFinished: Datadog.Trace.ClrProfiler.Managed v", assembly_version, " matched profiler version v", PROFILER_VERSION);
      managed_profiler_loaded_app_domains.insert(assembly_info.app_domain_id);

      if (runtime_information_.is_desktop() && corlib_module_loaded) {
        // Set the managed_profiler_loaded_domain_neutral flag whenever the managed profiler is loaded shared
        if (assembly_info.app_domain_id == corlib_app_domain_id) {
          Info("AssemblyLoadFinished: Datadog.Trace.ClrProfiler.Managed was loaded domain-neutral");
          managed_profiler_loaded_domain_neutral = true;
        }
        else {
          Info("AssemblyLoadFinished: Datadog.Trace.ClrProfiler.Managed was not loaded domain-neutral");
        }
      }
    }
    else {
      Warn("AssemblyLoadFinished: Datadog.Trace.ClrProfiler.Managed v", assembly_version, " did not match profiler version v", PROFILER_VERSION);
    }
  }

  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleLoadFinished(ModuleID module_id,
                                                          HRESULT hr_status) {
  if (FAILED(hr_status)) {
    // if module failed to load, skip it entirely,
    // otherwise we can crash the process if module is not valid
    CorProfilerBase::ModuleLoadFinished(module_id, hr_status);
    return S_OK;
  }

  if (!is_attached_) {
    return S_OK;
  }

  // keep this lock until we are done using the module,
  // to prevent it from unloading while in use
  std::lock_guard<std::mutex> guard(module_id_to_info_map_lock_);

  // double check if is_attached_ has changed to avoid possible race condition with shutdown function
  if (!is_attached_) {
    return S_OK;
  }

  const auto module_info = GetModuleInfo(this->info_, module_id);
  if (!module_info.IsValid()) {
    return S_OK;
  }

  if (debug_logging_enabled) {
    Debug("ModuleLoadFinished: ", module_id, " ", module_info.assembly.name,
          " AppDomain ", module_info.assembly.app_domain_id, " ",
          module_info.assembly.app_domain_name);
  }

  AppDomainID app_domain_id = module_info.assembly.app_domain_id;

  // Identify the AppDomain ID of mscorlib which will be the Shared Domain
  // because mscorlib is always a domain-neutral assembly
  if (!corlib_module_loaded &&
      (module_info.assembly.name == "mscorlib"_W ||
       module_info.assembly.name == "System.Private.CoreLib"_W)) {
    corlib_module_loaded = true;
    corlib_app_domain_id = app_domain_id;
    
    ComPtr<IUnknown> metadata_interfaces;
    auto hr = this->info_->GetModuleMetaData(module_id, ofRead | ofWrite, IID_IMetaDataImport2, metadata_interfaces.GetAddressOf());
    
    // Get the IMetaDataAssemblyImport interface to get metadata from the
    // managed assembly
    const auto assembly_import = metadata_interfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport);
    const auto assembly_metadata = GetAssemblyImportMetadata(assembly_import);

    hr = assembly_import->GetAssemblyProps(
        assembly_metadata.assembly_token, &corAssemblyProperty.ppbPublicKey,
        &corAssemblyProperty.pcbPublicKey, &corAssemblyProperty.pulHashAlgId,
        NULL, 0, NULL, &corAssemblyProperty.pMetaData,
        &corAssemblyProperty.assemblyFlags);

    if (FAILED(hr)) {
      Warn("AssemblyLoadFinished failed to get properties for COR assembly ");
    }

    corAssemblyProperty.szName = module_info.assembly.name;

    Info("COR library: ", corAssemblyProperty.szName, " ",
         corAssemblyProperty.pMetaData.usMajorVersion, ".",
         corAssemblyProperty.pMetaData.usMinorVersion, ".",
         corAssemblyProperty.pMetaData.usRevisionNumber);

    return S_OK;
  }

  // In IIS, the startup hook will be inserted into a method in System.Web (which is domain-neutral)
  // but the Datadog.Trace.ClrProfiler.Managed.Loader assembly that the startup hook loads from a
  // byte array will be loaded into a non-shared AppDomain.
  // In this case, do not insert another startup hook into that non-shared AppDomain
  if (module_info.assembly.name == "Datadog.Trace.ClrProfiler.Managed.Loader"_W) {
    Info("ModuleLoadFinished: Datadog.Trace.ClrProfiler.Managed.Loader loaded into AppDomain ",
          app_domain_id, " ", module_info.assembly.app_domain_name);
    first_jit_compilation_app_domains.insert(app_domain_id);
    return S_OK;
  }

  if (module_info.IsWindowsRuntime()) {
    // We cannot obtain writable metadata interfaces on Windows Runtime modules
    // or instrument their IL.
    Debug("ModuleLoadFinished skipping Windows Metadata module: ", module_id,
          " ", module_info.assembly.name);
    return S_OK;
  }

  for (auto&& skip_assembly_pattern : skip_assembly_prefixes) {
    if (module_info.assembly.name.rfind(skip_assembly_pattern, 0) == 0) {
      Debug("ModuleLoadFinished skipping module by pattern: ", module_id, " ",
            module_info.assembly.name);
      return S_OK;
    }
  }

  for (auto&& skip_assembly : skip_assemblies) {
    if (module_info.assembly.name == skip_assembly) {
      Debug("ModuleLoadFinished skipping known module: ", module_id, " ",
            module_info.assembly.name);
      return S_OK;
    }
  }

  std::vector<IntegrationMethod> filtered_integrations =
      FilterIntegrationsByCaller(integration_methods_, module_info.assembly);

  if (filtered_integrations.empty()) {
    // we don't need to instrument anything in this module, skip it
    Debug("ModuleLoadFinished skipping module (filtered by caller): ",
          module_id, " ", module_info.assembly.name);
    return S_OK;
  }

  ComPtr<IUnknown> metadata_interfaces;
  auto hr = this->info_->GetModuleMetaData(module_id, ofRead | ofWrite,
                                           IID_IMetaDataImport2,
                                           metadata_interfaces.GetAddressOf());

  if (FAILED(hr)) {
    Warn("ModuleLoadFinished failed to get metadata interface for ", module_id,
         " ", module_info.assembly.name);
    return S_OK;
  }

  const auto metadata_import =
      metadata_interfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
  const auto metadata_emit =
      metadata_interfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
  const auto assembly_import = metadata_interfaces.As<IMetaDataAssemblyImport>(
      IID_IMetaDataAssemblyImport);
  const auto assembly_emit =
      metadata_interfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

  // don't skip Microsoft.AspNetCore.Hosting so we can run the startup hook and
  // subscribe to DiagnosticSource events.
  // don't skip Dapper: it makes ADO.NET calls even though it doesn't reference
  // System.Data or System.Data.Common
  if (module_info.assembly.name != "Microsoft.AspNetCore.Hosting"_W &&
      module_info.assembly.name != "Dapper"_W) {
    filtered_integrations =
        FilterIntegrationsByTarget(filtered_integrations, assembly_import);

    if (filtered_integrations.empty()) {
      // we don't need to instrument anything in this module, skip it
      Debug("ModuleLoadFinished skipping module (filtered by target): ",
            module_id, " ", module_info.assembly.name);
      return S_OK;
    }
  }

  mdModule module;
  hr = metadata_import->GetModuleFromScope(&module);
  if (FAILED(hr)) {
    Warn("ModuleLoadFinished failed to get module metadata token for ",
         module_id, " ", module_info.assembly.name);
    return S_OK;
  }

  GUID module_version_id;
  hr = metadata_import->GetScopeProps(nullptr, 0, nullptr, &module_version_id);
  if (FAILED(hr)) {
    Warn("ModuleLoadFinished failed to get module_version_id for ", module_id,
         " ", module_info.assembly.name);
    return S_OK;
  }

  ModuleMetadata* module_metadata = new ModuleMetadata(
      metadata_import, metadata_emit, assembly_import, assembly_emit,
      module_info.assembly.name, app_domain_id,
      module_version_id, filtered_integrations, &corAssemblyProperty);

  // store module info for later lookup
  module_id_to_info_map_[module_id] = module_metadata;

  Debug("ModuleLoadFinished stored metadata for ", module_id, " ",
        module_info.assembly.name, " AppDomain ",
        module_info.assembly.app_domain_id, " ",
        module_info.assembly.app_domain_name);

  // We call the function to analyze the module and request the ReJIT of integrations defined in this module.
  if (IsCallTargetEnabled()) {
    CallTarget_RequestRejitForModule(module_id, module_metadata, filtered_integrations);
  }

  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadStarted(ModuleID module_id) {
  if (!is_attached_) {
    return S_OK;
  }

  if (debug_logging_enabled) {
    const auto module_info = GetModuleInfo(this->info_, module_id);

    if (module_info.IsValid()) {
      Debug("ModuleUnloadStarted: ", module_id, " ", module_info.assembly.name,
            " AppDomain ", module_info.assembly.app_domain_id, " ",
            module_info.assembly.app_domain_name);
    } else {
      Debug("ModuleUnloadStarted: ", module_id);
    }
  }

  // take this lock so we block until the
  // module metadata is not longer being used
  std::lock_guard<std::mutex> guard(module_id_to_info_map_lock_);

  // double check if is_attached_ has changed to avoid possible race condition with shutdown function
  if (!is_attached_) {
    return S_OK;
  }

  // remove module metadata from map
  if (module_id_to_info_map_.count(module_id) > 0) {
    ModuleMetadata* metadata = module_id_to_info_map_[module_id];

    // remove appdomain id from managed_profiler_loaded_app_domains set
    if (managed_profiler_loaded_app_domains.find(metadata->app_domain_id) !=
        managed_profiler_loaded_app_domains.end()) {
      managed_profiler_loaded_app_domains.erase(metadata->app_domain_id);
    }

    module_id_to_info_map_.erase(module_id);
    delete metadata;
  }

  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::Shutdown() {
  CorProfilerBase::Shutdown();

  // keep this lock until we are done using the module,
  // to prevent it from unloading while in use
  std::lock_guard<std::mutex> guard(module_id_to_info_map_lock_);

  Warn("Exiting.");
  is_attached_.store(false);
  Logger::Shutdown();
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ProfilerDetachSucceeded() {
  if (!is_attached_) {
    return S_OK;
  }
  CorProfilerBase::ProfilerDetachSucceeded();

  // keep this lock until we are done using the module,
  // to prevent it from unloading while in use
  std::lock_guard<std::mutex> guard(module_id_to_info_map_lock_);

  // double check if is_attached_ has changed to avoid possible race condition with shutdown function
  if (!is_attached_) {
    return S_OK;
  }

  Warn("Detaching profiler.");
  Logger::Instance()->Flush();
  is_attached_.store(false);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::JITCompilationStarted(
    FunctionID function_id, BOOL is_safe_to_block) {
  if (!is_attached_ || !is_safe_to_block) {
    return S_OK;
  }

  // keep this lock until we are done using the module,
  // to prevent it from unloading while in use
  std::lock_guard<std::mutex> guard(module_id_to_info_map_lock_);

  // double check if is_attached_ has changed to avoid possible race condition with shutdown function
  if (!is_attached_) {
    return S_OK;
  }

  ModuleID module_id;
  mdToken function_token = mdTokenNil;

  HRESULT hr = this->info_->GetFunctionInfo(function_id, nullptr, &module_id,
                                            &function_token);

  if (FAILED(hr)) {
    Warn("JITCompilationStarted: Call to ICorProfilerInfo4.GetFunctionInfo() failed for ", function_id);
    return S_OK;
  }

  // Verify that we have the metadata for this module
  ModuleMetadata* module_metadata = nullptr;
  if (module_id_to_info_map_.count(module_id) > 0) {
    module_metadata = module_id_to_info_map_[module_id];
  }

  if (module_metadata == nullptr) {
    // we haven't stored a ModuleMetadata for this module,
    // so we can't modify its IL
    return S_OK;
  }

  // get function info
  const auto caller =
      GetFunctionInfo(module_metadata->metadata_import, function_token);
  if (!caller.IsValid()) {
    return S_OK;
  }

  if (debug_logging_enabled) {
    Debug("JITCompilationStarted: function_id=", function_id,
          " token=", function_token, " name=", caller.type.name, ".",
          caller.name, "()");
  }

  // IIS: Ensure that the startup hook is inserted into System.Web.Compilation.BuildManager.InvokePreStartInitMethods.
  // This will be the first call-site considered for the startup hook injection,
  // which correctly loads Datadog.Trace.ClrProfiler.Managed.Loader into the application's
  // own AppDomain because at this point in the code path, the ApplicationImpersonationContext
  // has been started.
  //
  // Note: This check must only run on desktop because it is possible (and the default) to host
  // ASP.NET Core in-process, so a new .NET Core runtime is instantiated and run in the same w3wp.exe process
  auto valid_startup_hook_callsite = true;
  if (is_desktop_iis) {
      valid_startup_hook_callsite =
            module_metadata->assemblyName == "System.Web"_W &&
            caller.type.name == "System.Web.Compilation.BuildManager"_W &&
            caller.name == "InvokePreStartInitMethods"_W;
  } else if (module_metadata->assemblyName == "System"_W ||
             module_metadata->assemblyName == "System.Net.Http"_W) {
    valid_startup_hook_callsite = false;
  }

  // The first time a method is JIT compiled in an AppDomain, insert our startup
  // hook which, at a minimum, must add an AssemblyResolve event so we can find
  // Datadog.Trace.ClrProfiler.Managed.dll and its dependencies on-disk since it
  // is no longer provided in a NuGet package
  if (valid_startup_hook_callsite &&
      first_jit_compilation_app_domains.find(module_metadata->app_domain_id) ==
      first_jit_compilation_app_domains.end()) {
    bool domain_neutral_assembly = runtime_information_.is_desktop() && corlib_module_loaded && module_metadata->app_domain_id == corlib_app_domain_id;
    Info("JITCompilationStarted: Startup hook registered in function_id=", function_id,
          " token=", function_token, " name=", caller.type.name, ".",
          caller.name, "(), assembly_name=", module_metadata->assemblyName,
          " app_domain_id=", module_metadata->app_domain_id,
          " domain_neutral=", domain_neutral_assembly);

    first_jit_compilation_app_domains.insert(module_metadata->app_domain_id);

    hr = RunILStartupHook(module_metadata->metadata_emit, module_id,
                          function_token);

    if (FAILED(hr)) {
      Warn("JITCompilationStarted: Call to RunILStartupHook() failed for ", module_id, " ", function_token);
      return S_OK;
    }

    if (is_desktop_iis) {
      hr = AddIISPreStartInitFlags(module_id,
                                   function_token);

      if (FAILED(hr)) {
        Warn("JITCompilationStarted: Call to AddIISPreStartInitFlags() failed for ",
             module_id, " ", function_token);
        return S_OK;
      }
    }
  }

  // we don't actually need to instrument anything in
  // Microsoft.AspNetCore.Hosting, it was included only to ensure the startup
  // hook is called for AspNetCore applications
  if (module_metadata->assemblyName == "Microsoft.AspNetCore.Hosting"_W) {
    return S_OK;
  }

  // Get valid method replacements for this caller method
  const auto method_replacements =
      module_metadata->GetMethodReplacementsForCaller(caller);
  if (method_replacements.empty()) {
    return S_OK;
  }

  // Perform method insertion calls
  hr = ProcessInsertionCalls(module_metadata,
                             function_id,
                             module_id,
                             function_token,
                             caller,
                             method_replacements);

  if (FAILED(hr)) {
    Warn("JITCompilationStarted: Call to ProcessInsertionCalls() failed for ", function_id, " ", module_id, " ", function_token);
    return S_OK;
  }

  // Perform method replacement calls
  hr = ProcessReplacementCalls(module_metadata,
                               function_id,
                               module_id,
                               function_token,
                               caller,
                               method_replacements);

  if (FAILED(hr)) {
    Warn("JITCompilationStarted: Call to ProcessReplacementCalls() failed for ", function_id, " ", module_id, " ", function_token);
    return S_OK;
  }

  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::JITInlining(FunctionID callerId, 
    FunctionID calleeId, BOOL* pfShouldInline) {
  if (!is_attached_) {
    return S_OK;
  }

  ModuleID calleeModuleId;
  mdToken calleFunctionToken = mdTokenNil;
  auto hr = this->info_->GetFunctionInfo(calleeId, NULL, &calleeModuleId,
                                         &calleFunctionToken);

  *pfShouldInline = true;

  if (FAILED(hr)) {
    Warn("*** JITInlining: Failed to get the function info of the calleId: ", calleeId);
    return S_OK;
  }

  if (rejit_handler == nullptr) {
    return S_OK;
  }

  RejitHandlerModule* handlerModule = nullptr;
  if (rejit_handler->TryGetModule(calleeModuleId, &handlerModule)) {
    RejitHandlerModuleMethod* handlerMethod = nullptr;
    if (handlerModule->TryGetMethod(calleFunctionToken, &handlerMethod)) {
      Debug("*** JITInlining: Inlining disabled for [ModuleId=", calleeModuleId,
           ", MethodDef=", TokenStr(&calleFunctionToken), "]");
      *pfShouldInline = false;
      return S_OK;
    } 
  }

  return S_OK;
}

//
// ICorProfilerCallback6 methods
//
HRESULT STDMETHODCALLTYPE CorProfiler::GetAssemblyReferences(
    const WCHAR* wszAssemblyPath,
    ICorProfilerAssemblyReferenceProvider* pAsmRefProvider) {
  if (in_azure_app_services) {
    Debug("GetAssemblyReferences skipping entire callback because this is running in Azure App Services, which isn't yet supported for this feature. AssemblyPath=", wszAssemblyPath);
    return S_OK;
  }

  // Convert the assembly path to the assembly name, assuming the assembly name
  // is either <assembly_name.ni.dll> or <assembly_name>.dll
  auto assemblyPathString = ToString(wszAssemblyPath);
  auto filename =
      assemblyPathString.substr(assemblyPathString.find_last_of("\\/") + 1);
  auto lastNiDllPeriodIndex = filename.rfind(".ni.dll");
  auto lastDllPeriodIndex = filename.rfind(".dll");
  if (lastNiDllPeriodIndex != std::string::npos) {
    filename.erase(lastNiDllPeriodIndex, 7);
  } else if (lastDllPeriodIndex != std::string::npos) {
    filename.erase(lastDllPeriodIndex, 4);
  }

  const WSTRING assembly_name = ToWSTRING(filename);

  // Skip known framework assemblies that we will not instrument and,
  // as a result, will not need an assembly reference to the
  // managed profiler
  for (auto&& skip_assembly_pattern : skip_assembly_prefixes) {
    if (assembly_name.rfind(skip_assembly_pattern, 0) == 0) {
      Debug("GetAssemblyReferences skipping module by pattern: Name=",
           assembly_name, " Path=", wszAssemblyPath);
      return S_OK;
    }
  }

  for (auto&& skip_assembly : skip_assemblies) {
    if (assembly_name == skip_assembly) {
      Debug("GetAssemblyReferences skipping known assembly: Name=",
          assembly_name, " Path=", wszAssemblyPath);
      return S_OK;
    }
  }

  // Construct an ASSEMBLYMETADATA structure for the managed profiler that can
  // be consumed by the runtime
  const AssemblyReference assemblyReference = trace::AssemblyReference(managed_profiler_full_assembly_version);
  ASSEMBLYMETADATA assembly_metadata{};

  assembly_metadata.usMajorVersion = assemblyReference.version.major;
  assembly_metadata.usMinorVersion = assemblyReference.version.minor;
  assembly_metadata.usBuildNumber = assemblyReference.version.build;
  assembly_metadata.usRevisionNumber = assemblyReference.version.revision;
  if (assemblyReference.locale == "neutral"_W) {
    assembly_metadata.szLocale = const_cast<WCHAR*>("\0"_W.c_str());
    assembly_metadata.cbLocale = 0;
  } else {
    assembly_metadata.szLocale =
        const_cast<WCHAR*>(assemblyReference.locale.c_str());
    assembly_metadata.cbLocale = (DWORD)(assemblyReference.locale.size());
  }

  DWORD public_key_size = 8;
  if (assemblyReference.public_key == trace::PublicKey()) {
    public_key_size = 0;
  }

  COR_PRF_ASSEMBLY_REFERENCE_INFO asmRefInfo;
  asmRefInfo.pbPublicKeyOrToken =
        (void*)&assemblyReference.public_key.data[0];
  asmRefInfo.cbPublicKeyOrToken = public_key_size;
  asmRefInfo.szName = assemblyReference.name.c_str();
  asmRefInfo.pMetaData = &assembly_metadata;
  asmRefInfo.pbHashValue = nullptr;
  asmRefInfo.cbHashValue = 0;
  asmRefInfo.dwAssemblyRefFlags = 0;

  // Attempt to extend the assembly closure of the provided assembly to include
  // the managed profiler
  auto hr = pAsmRefProvider->AddAssemblyReference(&asmRefInfo);
  if (FAILED(hr)) {
    Warn("GetAssemblyReferences failed for call from ", wszAssemblyPath);
    return S_OK;
  }

  Debug("GetAssemblyReferences extending assembly closure for ",
      assembly_name, " to include ", asmRefInfo.szName,
      ". Path=", wszAssemblyPath);
  instrument_domain_neutral_assemblies = true;

  return S_OK;
}

bool CorProfiler::IsAttached() const { return is_attached_; }

//
// Helper methods
//
HRESULT CorProfiler::ProcessReplacementCalls(
    ModuleMetadata* module_metadata,
    const FunctionID function_id,
    const ModuleID module_id,
    const mdToken function_token,
    const FunctionInfo& caller,
    const std::vector<MethodReplacement> method_replacements) {
  ILRewriter rewriter(this->info_, nullptr, module_id, function_token);
  bool modified = false;
  auto hr = rewriter.Import();

  if (FAILED(hr)) {
    Warn("ProcessReplacementCalls: Call to ILRewriter.Import() failed for ", module_id, " ", function_token);
    return hr;
  }

  std::string original_code;
  if (dump_il_rewrite_enabled) {
    original_code =
        GetILCodes("***   IL original code for caller: ", &rewriter, caller, module_metadata);
  }

  // Perform method call replacements
  for (auto& method_replacement : method_replacements) {
    // Exit early if the method replacement isn't actually doing a replacement
    if (method_replacement.wrapper_method.action != "ReplaceTargetMethod"_W) {
      continue;
    }

    const auto& wrapper_method_key =
        method_replacement.wrapper_method.get_method_cache_key();
    // Exit early if we previously failed to store the method ref for this wrapper_method
    if (module_metadata->IsFailedWrapperMemberKey(wrapper_method_key)) {
      continue;
    }

    // for each IL instruction
    for (ILInstr* pInstr = rewriter.GetILList()->m_pNext;
         pInstr != rewriter.GetILList(); pInstr = pInstr->m_pNext) {
      // only CALL or CALLVIRT
      if (pInstr->m_opcode != CEE_CALL && pInstr->m_opcode != CEE_CALLVIRT) {
        continue;
      }

      // get the target function info, continue if its invalid
      auto target =
          GetFunctionInfo(module_metadata->metadata_import, pInstr->m_Arg32);
      if (!target.IsValid()) {
        continue;
      }

      // make sure the type and method names match
      if (method_replacement.target_method.type_name != target.type.name ||
          method_replacement.target_method.method_name != target.name) {
        continue;
      }

      // we add 3 parameters to every wrapper method: opcode, mdToken, and
      // module_version_id
      const short added_parameters_count = 3;

      auto wrapper_method_signature_size =
          method_replacement.wrapper_method.method_signature.data.size();

      if (wrapper_method_signature_size < (added_parameters_count + 3)) {
        // wrapper signature must have at least 6 bytes
        // 0:{CallingConvention}|1:{ParamCount}|2:{ReturnType}|3:{OpCode}|4:{mdToken}|5:{ModuleVersionId}
        if (debug_logging_enabled) {
          Debug(
              "JITCompilationStarted skipping function call: wrapper signature "
              "too short. function_id=",
              function_id, " token=", function_token,
              " wrapper_method=", method_replacement.wrapper_method.type_name,
              ".", method_replacement.wrapper_method.method_name,
              "() wrapper_method_signature_size=",
              wrapper_method_signature_size);
        }

        continue;
      }

      auto expected_number_args = method_replacement.wrapper_method
                                      .method_signature.NumberOfArguments();

      // subtract the last arguments we add to every wrapper
      expected_number_args = expected_number_args - added_parameters_count;

      if (target.signature.IsInstanceMethod()) {
        // We always pass the instance as the first argument
        expected_number_args--;
      }

      auto target_arg_count = target.signature.NumberOfArguments();

      if (expected_number_args != target_arg_count) {
        // Number of arguments does not match our wrapper method
        if (debug_logging_enabled) {
          Debug(
              "JITCompilationStarted skipping function call: argument counts "
              "don't match. function_id=",
              function_id, " token=", function_token,
              " target_name=", target.type.name, ".", target.name,
              "() expected_number_args=", expected_number_args,
              " target_arg_count=", target_arg_count);
        }

        continue;
      }

      // Resolve the MethodRef now. If the method is generic, we'll need to use it
      // to define a MethodSpec
      // Generate a method ref token for the wrapper method
      mdMemberRef wrapper_method_ref = mdMemberRefNil;
      mdTypeRef wrapper_type_ref = mdTypeRefNil;
      auto generated_wrapper_method_ref = GetWrapperMethodRef(module_metadata,
                                                              module_id,
                                                              method_replacement,
                                                              wrapper_method_ref, 
                                                              wrapper_type_ref);
      if (!generated_wrapper_method_ref) {
        Warn(
          "JITCompilationStarted failed to obtain wrapper method ref for ",
          method_replacement.wrapper_method.type_name, ".", method_replacement.wrapper_method.method_name, "().",
          " function_id=", function_id, " function_token=", function_token,
          " name=", caller.type.name, ".", caller.name, "()");
        continue;
      }

      auto method_def_md_token = target.id;

      if (target.is_generic) {
        if (target.signature.NumberOfTypeArguments() !=
            method_replacement.wrapper_method.method_signature
                .NumberOfTypeArguments()) {
          // Number of generic arguments does not match our wrapper method
          continue;
        }

        // we need to emit a method spec to populate the generic arguments
        wrapper_method_ref =
            DefineMethodSpec(module_metadata->metadata_emit, wrapper_method_ref,
                             target.function_spec_signature);
        method_def_md_token = target.method_def_id;
      }

      std::vector<WSTRING> actual_sig;
      const auto successfully_parsed_signature = TryParseSignatureTypes(
          module_metadata->metadata_import, target, actual_sig);
      auto expected_sig =
          method_replacement.target_method.signature_types;

      if (!successfully_parsed_signature) {
        if (debug_logging_enabled) {
          Debug(
              "JITCompilationStarted skipping function call: failed to parse "
              "signature. function_id=",
              function_id, " token=", function_token,
              " target_name=", target.type.name, ".", target.name, "()",
              " successfully_parsed_signature=", successfully_parsed_signature,
              " sig_types.size()=", actual_sig.size(),
              " expected_sig_types.size()=", expected_sig.size());
        }

        continue;
      }

      if (actual_sig.size() != expected_sig.size()) {
        // we can't safely assume our wrapper methods handle the types
        if (debug_logging_enabled) {
          Debug(
              "JITCompilationStarted skipping function call: unexpected type "
              "count. function_id=",
              function_id, " token=", function_token,
              " target_name=", target.type.name, ".", target.name,
              "() successfully_parsed_signature=",
              successfully_parsed_signature,
              " sig_types.size()=", actual_sig.size(),
              " expected_sig_types.size()=", expected_sig.size());
        }

        continue;
      }

      auto is_match = true;
      for (size_t i = 0; i < expected_sig.size(); i++) {
        if (expected_sig[i] == "_"_W) {
          // We are supposed to ignore this index
          continue;
        }
        if (expected_sig[i] != actual_sig[i]) {
          // we have a type mismatch, drop out
          if (debug_logging_enabled) {
            Debug(
                "JITCompilationStarted skipping function call: types don't "
                "match. function_id=",
                function_id, " token=", function_token,
                " target_name=", target.type.name, ".", target.name,
                "() actual[", i, "]=", actual_sig[i], ", expected[",
                i, "]=", expected_sig[i]);
          }

          is_match = false;
          break;
        }
      }

      if (!is_match) {
        // signatures don't match
        continue;
      }

      // At this point we know we've hit a match. Error out if
      //   1) The managed profiler has not been loaded yet
      //   2) The caller is domain-neutral AND we do not want to instrument domain-neutral assemblies
      //   3) The target instruction is a constrained virtual method call (a constrained IL instruction followed by a callvirt IL instruction)

      //   1) The managed profiler has not been loaded yet
      if (!ProfilerAssemblyIsLoadedIntoAppDomain(module_metadata->app_domain_id)) {
        Warn(
            "JITCompilationStarted skipping method: Method replacement "
            "found but the managed profiler has not yet been loaded "
            "into AppDomain with id=", module_metadata->app_domain_id,
            " function_id=", function_id, " token=", function_token,
            " caller_name=", caller.type.name, ".", caller.name, "()",
            " target_name=", target.type.name, ".", target.name, "()");
        continue;
      }

      //   2) The caller is domain-neutral AND we do not want to instrument domain-neutral assemblies
      bool caller_assembly_is_domain_neutral =
          runtime_information_.is_desktop() && corlib_module_loaded &&
          module_metadata->app_domain_id == corlib_app_domain_id;

      if (caller_assembly_is_domain_neutral && !instrument_domain_neutral_assemblies) {
        Warn(
            "JITCompilationStarted skipping method: Method replacement",
            " found but the calling assembly ", module_metadata->assemblyName,
            " has been loaded domain-neutral so its code is being shared across AppDomains,"
            " making it unsafe for automatic instrumentation.",
            " function_id=", function_id, " token=", function_token,
            " caller_name=", caller.type.name, ".", caller.name, "()",
            " target_name=", target.type.name, ".", target.name, "()");
        continue;
      }

      //   3) The target instruction is a constrained virtual method call (a constrained IL instruction followed by a callvirt IL instruction)
      if (pInstr->m_opcode == CEE_CALLVIRT && pInstr->m_pPrev->m_opcode == CEE_CONSTRAINED) {
        Warn("JITCompilationStarted skipping method: Method replacement",
             " found but the target method call is a constrained virtual method call ",
             " (a 'constrained' IL instruction followed by a 'callvirt' IL instruction).",
             " This type of method call is not currently supported for automatic"
             " instrumentation.",
             " function_id=", function_id, " token=", function_token,
             " caller_name=", caller.type.name, ".", caller.name, "()",
             " target_name=", target.type.name, ".", target.name, "()");
        continue;
      }

      const auto original_argument = pInstr->m_Arg32;
      const void* module_version_id_ptr = &module_metadata->module_version_id;

      // Begin IL Modification
      ILRewriterWrapper rewriter_wrapper(&rewriter);
      rewriter_wrapper.SetILPosition(pInstr);

      // IL Modification #1: Replace original method call with a NOP, so that all original
      //                     jump targets resolve correctly and we correctly populate the
      //                     stack with additional arguments
      //
      // IMPORTANT: Conditional branches may jump to the original call instruction which
      // resulted in the InvalidProgramException seen in
      // https://github.com/DataDog/dd-trace-dotnet/pull/542. To avoid this, we'll do
      // the rest of our IL modifications AFTER this instruction.
      auto original_methodcall_opcode = pInstr->m_opcode;
      pInstr->m_opcode = CEE_NOP;
      pInstr = pInstr->m_pNext;
      rewriter_wrapper.SetILPosition(pInstr);

      // IL Modification #2: Conditionally box System.Threading.CancellationToken or System.ReadOnlyMemory<T>
      //                     if it is the last argument in the target method.
      //
      // System.Threading.CancellationToken:
      // If the last argument in the method signature is of the type
      // System.Threading.CancellationToken (a struct) then box it before calling our
      // integration method. This resolves https://github.com/DataDog/dd-trace-dotnet/issues/662,
      // in which we did not box the System.Threading.CancellationToken object, even though the
      // wrapper method expects an object. In that issue we observed some strange CLR behavior
      // when the target method was in System.Data and the environment was 32-bit .NET Framework:
      // the CLR swapped the values of the CancellationToken argument and the opCode argument.
      // For example, the VIRTCALL opCode is '0x6F' and this value would be placed at the memory
      // location assigned to the CancellationToken variable. Since we treat the CancellationToken
      // variable as an object, this '0x6F' would be dereference to access the underlying object,
      // and an invalid memory read would occur and crash the application.
      //
      // System.ReadOnlyMemory<T>:
      // If the last argument in the method signature is of the type
      // System.ReadOnlyMemory<T> (a generic valuetype) then box it before calling our
      // integration method. We need this modification for RabbitMQ.Client 6.x.x instrumentation
      // that uses System.ReadOnlyMemory<byte> instead of byte[] for the message body parameter.
      //
      // Currently, all integrations that use either of the two types
      // have the argument as the last argument in the signature (lucky us!).
      // For now, we'll do the following:
      //   1) Get the method signature of the original target method
      //   2) Read the signature until the final argument type
      //   3) Check for System.Threading.CancellationToken
      //      3a) If the type begins with `ELEMENT_TYPE_VALUETYPE`, uncompress the compressed type token that follows
      //      3b) If the type token represents System.Threading.CancellationToken, emit a 'box <type_token>' IL instruction before calling our wrapper method
      //   4) Check for System.ReadOnlyMemory<T>
      //      4a) If the type begins with `ELEMENT_TYPE_GENERICINST` and if the next byte is `ELEMENT_TYPE_VALUETYPE`, uncompress the compressed type token that follows
      //      4b) If the type token represents System.ReadOnlyMemory<T>, emit a 'box <type_token>' IL instruction before calling our wrapper method. The type token
      //          will be a TypeSpec representing the specific generic instantiation of System.ReadOnlyMemory<T>
      auto original_method_def = target.id;
      size_t argument_count = target.signature.NumberOfArguments();
      size_t return_type_index = target.signature.IndexOfReturnType();
      PCCOR_SIGNATURE pSigCurrent = PCCOR_SIGNATURE(&target.signature.data[return_type_index]); // index to the location of the return type
      bool signature_read_success = true;

      // iterate until the pointer is pointing at the last argument
      for (size_t signature_types_index = 0; signature_types_index < argument_count; signature_types_index++) {
        if (!ParseType(&pSigCurrent)) {
          signature_read_success = false;
          break;
        }
      }

      // read the last argument type
      if (signature_read_success && *pSigCurrent == ELEMENT_TYPE_VALUETYPE) {
        pSigCurrent++;
        mdToken valuetype_type_token = CorSigUncompressToken(pSigCurrent);

        // Currently, we only expect to see `System.Threading.CancellationToken` as a valuetype in this position
        // If we expand this to a general case, we would always perform the boxing regardless of type
        if (GetTypeInfo(module_metadata->metadata_import, valuetype_type_token).name == "System.Threading.CancellationToken"_W) {
          rewriter_wrapper.Box(valuetype_type_token);
        }
      }

      if (signature_read_success && *pSigCurrent == ELEMENT_TYPE_GENERICINST) {
        PCCOR_SIGNATURE p_start_byte = pSigCurrent;
        PCCOR_SIGNATURE p_end_byte = p_start_byte;

        pSigCurrent++;

        if (*pSigCurrent == ELEMENT_TYPE_VALUETYPE) {
          pSigCurrent++;
          mdToken valuetype_type_token = CorSigUncompressToken(pSigCurrent);

          // Currently, we only expect to see
          // `System.ReadOnlyMemory<T>` as a valuetype in this
          // position If we expand this to a general case, we would always
          // perform the boxing regardless of type
          if (GetTypeInfo(module_metadata->metadata_import, valuetype_type_token).name == "System.ReadOnlyMemory`1"_W
              && ParseType(&p_end_byte)) {
            size_t length = p_end_byte - p_start_byte;
            mdTypeSpec type_token;
            module_metadata->metadata_emit->GetTokenFromTypeSpec(
                p_start_byte, (ULONG)length, &type_token);
            rewriter_wrapper.Box(type_token);
          }
        }
      }

      // IL Modification #3: Insert a non-virtual call (CALL) to the instrumentation wrapper.
      //                     Always use CALL because the wrapper methods are all static.
      rewriter_wrapper.CallMember(wrapper_method_ref, false);
      rewriter_wrapper.SetILPosition(pInstr->m_pPrev); // Set ILPosition to method call

      // IL Modification #4: Push the following additional arguments on the evaluation stack in the
      //                     following order, which all integration wrapper methods expect:
      //                       1) [int32] original CALL/CALLVIRT opCode
      //                       2) [int32] mdToken for original method call target
      //                       3) [int64] pointer to MVID
      rewriter_wrapper.LoadInt32(original_methodcall_opcode);
      rewriter_wrapper.LoadInt32(method_def_md_token);
      rewriter_wrapper.LoadInt64(reinterpret_cast<INT64>(module_version_id_ptr));

      // IL Modification #5: Conditionally emit an unbox.any instruction on the return value
      //                     of the wrapper method if we return an object but the original
      //                     method call returned a valuetype or a generic type.
      //
      // This resolves https://github.com/DataDog/dd-trace-dotnet/pull/566, which raised a
      // System.EntryPointNotFoundException. This occurred because the return type of the
      // generic method was a generic type that evaluated to a value type at runtime. As a
      // result, this caller method expected an unboxed representation of the return value,
      // even though we can only return values of type object. So if we detect that the
      // expected return type is a valuetype or a generic type, issue an unbox.any
      // instruction that will unbox it.
      mdToken typeToken;
      if (method_replacement.wrapper_method.method_signature.ReturnTypeIsObject()
          && ReturnTypeIsValueTypeOrGeneric(module_metadata->metadata_import,
                              module_metadata->metadata_emit,
                              module_metadata->assembly_emit,
                              target.id,
                              target.signature,
                              &typeToken)) {
        if (debug_logging_enabled) {
          Debug(
              "JITCompilationStarted inserting 'unbox.any ", typeToken,
              "' instruction after calling target function."
              " function_id=", function_id,
              " token=", function_token,
              " target_name=", target.type.name, ".", target.name,"()");
        }
        rewriter_wrapper.UnboxAnyAfter(typeToken);
      }

      // End IL Modification
      modified = true;
      Info("*** JITCompilationStarted() replaced calls from ", caller.type.name,
           ".", caller.name, "() to ",
           method_replacement.target_method.type_name, ".",
           method_replacement.target_method.method_name, "() ",
           original_argument, " with calls to ",
           method_replacement.wrapper_method.type_name, ".",
           method_replacement.wrapper_method.method_name, "() ",
           wrapper_method_ref);
    }
  }

  if (modified) {
    hr = rewriter.Export();

    if (FAILED(hr)) {
      Warn("ProcessReplacementCalls: Call to ILRewriter.Export() failed for ModuleID=", module_id, " ", function_token);
      return hr;
    }

    if (dump_il_rewrite_enabled) {
      Info(original_code);
      Info(GetILCodes("***   IL modification  for caller: ", &rewriter, caller, module_metadata));
    }
  }

  return S_OK;
}

HRESULT CorProfiler::ProcessInsertionCalls(
    ModuleMetadata* module_metadata,
    const FunctionID function_id,
    const ModuleID module_id,
    const mdToken function_token,
    const FunctionInfo& caller,
    const std::vector<MethodReplacement> method_replacements) {

  ILRewriter rewriter(this->info_, nullptr, module_id, function_token);
  bool modified = false;

  auto hr = rewriter.Import();

  if (FAILED(hr)) {
    Warn("ProcessInsertionCalls: Call to ILRewriter.Import() failed for ", module_id, " ", function_token);
    return hr;
  }

  ILRewriterWrapper rewriter_wrapper(&rewriter);
  ILInstr* firstInstr = rewriter.GetILList()->m_pNext;
  ILInstr* lastInstr = rewriter.GetILList()->m_pPrev; // Should be a 'ret' instruction

  for (auto& method_replacement : method_replacements) {
    if (method_replacement.wrapper_method.action == "ReplaceTargetMethod"_W) {
      continue;
    }

    const auto& wrapper_method_key =
        method_replacement.wrapper_method.get_method_cache_key();

    // Exit early if we previously failed to store the method ref for this wrapper_method
    if (module_metadata->IsFailedWrapperMemberKey(wrapper_method_key)) {
      continue;
    }

    // Generate a method ref token for the wrapper method
    mdMemberRef wrapper_method_ref = mdMemberRefNil;
    mdTypeRef wrapper_type_ref = mdTypeRefNil;
    auto generated_wrapper_method_ref = GetWrapperMethodRef(module_metadata,
                                                            module_id,
                                                            method_replacement,
                                                            wrapper_method_ref, 
                                                            wrapper_type_ref);
    if (!generated_wrapper_method_ref) {
      Warn(
        "JITCompilationStarted failed to obtain wrapper method ref for ",
        method_replacement.wrapper_method.type_name, ".", method_replacement.wrapper_method.method_name, "().",
        " function_id=", function_id, " function_token=", function_token,
        " name=", caller.type.name, ".", caller.name, "()");
      continue;
    }

    // After successfully getting the method reference, insert a call to it
    if (method_replacement.wrapper_method.action == "InsertFirst"_W) {
      // Get first instruction and set the rewriter to that location
      rewriter_wrapper.SetILPosition(firstInstr);
      rewriter_wrapper.CallMember(wrapper_method_ref, false);
      firstInstr = firstInstr->m_pPrev;
      modified = true;

      Info("*** JITCompilationStarted() : InsertFirst inserted call to ",
        method_replacement.wrapper_method.type_name, ".",
        method_replacement.wrapper_method.method_name, "() ", wrapper_method_ref,
        " to the beginning of method",
        caller.type.name,".", caller.name, "()");
    }
  }

  if (modified) {
    hr = rewriter.Export();

    if (FAILED(hr)) {
      Warn("ProcessInsertionCalls: Call to ILRewriter.Export() failed for ModuleID=", module_id, " ", function_token);
      return hr;
    }
  }

  return S_OK;
}

bool CorProfiler::GetWrapperMethodRef(
    ModuleMetadata* module_metadata,
    ModuleID module_id,
    const MethodReplacement& method_replacement,
    mdMemberRef& wrapper_method_ref,
    mdTypeRef& wrapper_type_ref) {
  const auto& wrapper_method_key =
      method_replacement.wrapper_method.get_method_cache_key();
  const auto& wrapper_type_key =
      method_replacement.wrapper_method.get_type_cache_key();

  // Resolve the MethodRef now. If the method is generic, we'll need to use it
  // later to define a MethodSpec
  if (!module_metadata->TryGetWrapperMemberRef(wrapper_method_key,
                                                   wrapper_method_ref)) {
    const auto module_info = GetModuleInfo(this->info_, module_id);
    if (!module_info.IsValid()) {
      return false;
    }

    mdModule module;
    auto hr = module_metadata->metadata_import->GetModuleFromScope(&module);
    if (FAILED(hr)) {
      Warn(
          "JITCompilationStarted failed to get module metadata token for "
          "module_id=", module_id, " module_name=", module_info.assembly.name);
      return false;
    }

    const MetadataBuilder metadata_builder(
        *module_metadata, module, module_metadata->metadata_import,
        module_metadata->metadata_emit, module_metadata->assembly_import,
        module_metadata->assembly_emit);

    // for each wrapper assembly, emit an assembly reference
    hr = metadata_builder.EmitAssemblyRef(
        method_replacement.wrapper_method.assembly);
    if (FAILED(hr)) {
      Warn(
          "JITCompilationStarted failed to emit wrapper assembly ref for assembly=",
          method_replacement.wrapper_method.assembly.name,
          ", Version=", method_replacement.wrapper_method.assembly.version.str(),
          ", Culture=", method_replacement.wrapper_method.assembly.locale,
          " PublicKeyToken=", method_replacement.wrapper_method.assembly.public_key.str());
      return false;
    }

    // for each method replacement in each enabled integration,
    // emit a reference to the instrumentation wrapper methods
    hr = metadata_builder.StoreWrapperMethodRef(method_replacement);
    if (FAILED(hr)) {
      Warn(
        "JITCompilationStarted failed to obtain wrapper method ref for ",
        method_replacement.wrapper_method.type_name, ".", method_replacement.wrapper_method.method_name, "().");
      return false;
    } else {
      module_metadata->TryGetWrapperMemberRef(wrapper_method_key,
                                              wrapper_method_ref);
    }
  }
  module_metadata->TryGetWrapperParentTypeRef(wrapper_type_key,
                                              wrapper_type_ref);
  return true;
}

bool CorProfiler::ProfilerAssemblyIsLoadedIntoAppDomain(AppDomainID app_domain_id) {
  return managed_profiler_loaded_domain_neutral ||
         managed_profiler_loaded_app_domains.find(app_domain_id) !=
             managed_profiler_loaded_app_domains.end();
}

const std::string indent_values[] = {
    "",
    std::string(2 * 1, ' '),
    std::string(2 * 2, ' '),
    std::string(2 * 3, ' '),
    std::string(2 * 4, ' '),
    std::string(2 * 5, ' '),
    std::string(2 * 6, ' '),
    std::string(2 * 7, ' '),
    std::string(2 * 8, ' '),
    std::string(2 * 9, ' '),
    std::string(2 * 10, ' '),
};

std::string CorProfiler::GetILCodes(const std::string& title, ILRewriter* rewriter,
                                    const FunctionInfo& caller, ModuleMetadata* module_metadata) {
  std::stringstream orig_sstream;
  orig_sstream << title;
  orig_sstream << ToString(caller.type.name);
  orig_sstream << ".";
  orig_sstream << ToString(caller.name);
  orig_sstream << " => (max_stack: ";
  orig_sstream << rewriter->GetMaxStackValue();
  orig_sstream << ")" << std::endl;

  const auto ehCount = rewriter->GetEHCount();
  const auto ehPtr = rewriter->GetEHPointer();
  int indent = 1;

  PCCOR_SIGNATURE originalSignature = nullptr;
  ULONG originalSignatureSize = 0;
  mdToken localVarSig = rewriter->GetTkLocalVarSig();

  if (localVarSig != mdTokenNil) {
    auto hr = module_metadata->metadata_import->GetSigFromToken(localVarSig, &originalSignature, &originalSignatureSize);
    if (SUCCEEDED(hr)) {
      orig_sstream << std::endl
                   << ". Local Var Signature: "
                   << ToString(HexStr(originalSignature, originalSignatureSize)) 
                   << std::endl;
    }
  }

  orig_sstream << std::endl;
  for (ILInstr* cInstr = rewriter->GetILList()->m_pNext;
       cInstr != rewriter->GetILList(); cInstr = cInstr->m_pNext) {
    
    if (ehCount > 0) {
      for (unsigned int i = 0; i < ehCount; i++) {
        const auto currentEH = ehPtr[i];
        if (currentEH.m_Flags == COR_ILEXCEPTION_CLAUSE_FINALLY) {
          if (currentEH.m_pTryBegin == cInstr) {
            if (indent > 0) {
              orig_sstream << indent_values[indent];
            }
            orig_sstream << ".try {" << std::endl;
            indent++;
          }
          if (currentEH.m_pTryEnd == cInstr) {
            indent--;
            if (indent > 0) {
              orig_sstream << indent_values[indent];
            }
            orig_sstream << "}" << std::endl;
          }
          if (currentEH.m_pHandlerBegin == cInstr) {
            if (indent > 0) {
              orig_sstream << indent_values[indent];
            }
            orig_sstream <<  ".finally {" << std::endl;
            indent++;
          }
        }
      }
      for (unsigned int i = 0; i < ehCount; i++) {
        const auto currentEH = ehPtr[i];
        if (currentEH.m_Flags == COR_ILEXCEPTION_CLAUSE_NONE) {
          if (currentEH.m_pTryBegin == cInstr) {
            if (indent > 0) {
              orig_sstream << indent_values[indent];
            }
            orig_sstream << ".try {" << std::endl;
            indent++;
          }
          if (currentEH.m_pTryEnd == cInstr) {
            indent--;
            if (indent > 0) {
              orig_sstream << indent_values[indent];
            }
            orig_sstream << "}" << std::endl;
          }
          if (currentEH.m_pHandlerBegin == cInstr) {
            if (indent > 0) {
              orig_sstream << indent_values[indent];
            }
            orig_sstream << ".catch {" << std::endl;
            indent++;
          }
        }
      }
    }

    if (indent > 0) {
      orig_sstream << indent_values[indent];
    }
    orig_sstream << cInstr;
    orig_sstream << ": ";
    if (cInstr->m_opcode < opcodes_names.size()) {
      orig_sstream << std::setw(10) << opcodes_names[cInstr->m_opcode];
    } else {
       orig_sstream << "0x";
       orig_sstream << std::setfill('0') << std::setw(2) << std::hex
                   << cInstr->m_opcode;
    }
    if (cInstr->m_pTarget != NULL) {
      orig_sstream << "  ";
      orig_sstream << cInstr->m_pTarget;

      if (cInstr->m_opcode == CEE_CALL || cInstr->m_opcode == CEE_CALLVIRT || cInstr->m_opcode == CEE_NEWOBJ) {
        const auto memberInfo = GetFunctionInfo(module_metadata->metadata_import,
                                            (mdMemberRef)cInstr->m_Arg32);
        orig_sstream << "  | ";
        orig_sstream << ToString(memberInfo.type.name);
        orig_sstream << ".";
        orig_sstream << ToString(memberInfo.name);
        if (memberInfo.signature.NumberOfArguments() > 0) {
          orig_sstream << "(";
          orig_sstream << memberInfo.signature.NumberOfArguments();
          orig_sstream << " argument{s}";
          orig_sstream << ")";

        } else {
          orig_sstream << "()";
        }
      } else if (cInstr->m_opcode == CEE_CASTCLASS || cInstr->m_opcode == CEE_BOX ||
          cInstr->m_opcode == CEE_UNBOX_ANY || cInstr->m_opcode == CEE_NEWARR || 
          cInstr->m_opcode == CEE_INITOBJ) {
        const auto typeInfo = GetTypeInfo(module_metadata->metadata_import,
                                      (mdTypeRef)cInstr->m_Arg32);
        orig_sstream << "  | ";
        orig_sstream << ToString(typeInfo.name);
      } else if (cInstr->m_opcode == CEE_LDSTR) {
        LPWSTR szString = new WCHAR[1024];
        ULONG szStringLength;
        auto hr = module_metadata->metadata_import->GetUserString(
            (mdString)cInstr->m_Arg32, szString, 1024, &szStringLength);
        if (SUCCEEDED(hr)) {
          orig_sstream << "  | \"";
          orig_sstream << ToString(WSTRING(szString, szStringLength));
          orig_sstream << "\"";
        }
      }
    } else if (cInstr->m_Arg64 != 0) {
      orig_sstream << " ";
      orig_sstream << cInstr->m_Arg64;
    }
    orig_sstream << std::endl;

    if (ehCount > 0) {
      for (unsigned int i = 0; i < ehCount; i++) {
        const auto currentEH = ehPtr[i];
        if (currentEH.m_pHandlerEnd == cInstr) {
          indent--;
          if (indent > 0) {
            orig_sstream << indent_values[indent];
          }
          orig_sstream << "}" << std::endl;
        }
      }
    }
  }
  return orig_sstream.str();
}

//
// Startup methods
//
HRESULT CorProfiler::RunILStartupHook(
    const ComPtr<IMetaDataEmit2>& metadata_emit, const ModuleID module_id,
    const mdToken function_token) {
  mdMethodDef ret_method_token;
  auto hr = GenerateVoidILStartupMethod(module_id, &ret_method_token);

  if (FAILED(hr)) {
    Warn("RunILStartupHook: Call to GenerateVoidILStartupMethod failed for ", module_id);
    return hr;
  }

  ILRewriter rewriter(this->info_, nullptr, module_id, function_token);
  hr = rewriter.Import();

  if (FAILED(hr)) {
    Warn("RunILStartupHook: Call to ILRewriter.Import() failed for ", module_id, " ", function_token);
    return hr;
  }

  ILRewriterWrapper rewriter_wrapper(&rewriter);

  // Get first instruction and set the rewriter to that location
  ILInstr* pInstr = rewriter.GetILList()->m_pNext;
  rewriter_wrapper.SetILPosition(pInstr);
  rewriter_wrapper.CallMember(ret_method_token, false);
  hr = rewriter.Export();

  if (FAILED(hr)) {
    Warn("RunILStartupHook: Call to ILRewriter.Export() failed for ModuleID=", module_id, " ", function_token);
    return hr;
  }

  return S_OK;
}

HRESULT CorProfiler::GenerateVoidILStartupMethod(const ModuleID module_id,
                                                 mdMethodDef* ret_method_token) {
  ComPtr<IUnknown> metadata_interfaces;
  auto hr = this->info_->GetModuleMetaData(module_id, ofRead | ofWrite,
                                           IID_IMetaDataImport2,
                                           metadata_interfaces.GetAddressOf());
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: failed to get metadata interface for ", module_id);
    return hr;
  }

  const auto metadata_import =
      metadata_interfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
  const auto metadata_emit =
      metadata_interfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
  const auto assembly_import = metadata_interfaces.As<IMetaDataAssemblyImport>(
      IID_IMetaDataAssemblyImport);
  const auto assembly_emit =
      metadata_interfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

  mdModuleRef mscorlib_ref;
  hr = CreateAssemblyRefToMscorlib(assembly_emit, &mscorlib_ref);

  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: failed to define AssemblyRef to mscorlib");
    return hr;
  }

  // Define a TypeRef for System.Object
  mdTypeRef object_type_ref;
  hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, "System.Object"_W.c_str(),
                                          &object_type_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
    return hr;
  }

  // Define a new TypeDef __DDVoidMethodType__ that extends System.Object
  mdTypeDef new_type_def;
  hr = metadata_emit->DefineTypeDef("__DDVoidMethodType__"_W.c_str(), tdAbstract | tdSealed,
                               object_type_ref, NULL, &new_type_def);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineTypeDef failed");
    return hr;
  }

  // Define a new static method __DDVoidMethodCall__ on the new type that has a void return type and takes no arguments
  BYTE initialize_signature[] = {
    IMAGE_CEE_CS_CALLCONV_DEFAULT, // Calling convention
    0,                             // Number of parameters
    ELEMENT_TYPE_VOID,             // Return type
    ELEMENT_TYPE_OBJECT            // List of parameter types
  };
  hr = metadata_emit->DefineMethod(new_type_def,
                              "__DDVoidMethodCall__"_W.c_str(),
                              mdStatic,
                              initialize_signature,
                              sizeof(initialize_signature),
                              0,
                              0,
                              ret_method_token);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineMethod failed");
    return hr;
  }

  /////////////////////////////////////////////
  // Define a new static int field _isAssemblyLoaded on the new type.
  BYTE field_signature[] = {
      IMAGE_CEE_CS_CALLCONV_FIELD,
      ELEMENT_TYPE_I4
  };
  mdFieldDef isAssemblyLoadedFieldToken;
  hr = metadata_emit->DefineField(new_type_def, 
                          "_isAssemblyLoaded"_W.c_str(),
                          fdStatic | fdPrivate, 
                          field_signature, 
                          sizeof(field_signature),
                          0,
                          nullptr,
                          0,
                          &isAssemblyLoadedFieldToken);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineField _isAssemblyLoaded failed");
    return hr;
  }

  // Define a new static method IsAlreadyLoaded on the new type that has a bool return type and takes no arguments;
  BYTE already_loaded_signature[] = {
      IMAGE_CEE_CS_CALLCONV_DEFAULT,
      0,
      ELEMENT_TYPE_BOOLEAN,
      ELEMENT_TYPE_OBJECT
  };
  mdMethodDef alreadyLoadedMethodToken;
  hr = metadata_emit->DefineMethod(
      new_type_def, "IsAlreadyLoaded"_W.c_str(), mdStatic | mdPrivate,
      already_loaded_signature, sizeof(already_loaded_signature), 0, 0,
      &alreadyLoadedMethodToken);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineMethod IsAlreadyLoaded failed");
    return hr;
  }

  // Get a TypeRef for System.Threading.Interlocked
  mdTypeRef interlocked_type_ref;
  hr = metadata_emit->DefineTypeRefByName(mscorlib_ref, "System.Threading.Interlocked"_W.c_str(), &interlocked_type_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineTypeRefByName interlocked_type_ref failed");
    return hr;
  }

  // Create method signature for System.Threading.Interlocked::CompareExchange(int32&, int32, int32)
  COR_SIGNATURE interlocked_compare_exchange_signature[] = {
      IMAGE_CEE_CS_CALLCONV_DEFAULT,
      3,
      ELEMENT_TYPE_I4,
      ELEMENT_TYPE_BYREF,
      ELEMENT_TYPE_I4,
      ELEMENT_TYPE_I4,
      ELEMENT_TYPE_I4
  };

  mdMemberRef interlocked_compare_member_ref;
  hr = metadata_emit->DefineMemberRef(
      interlocked_type_ref, "CompareExchange"_W.c_str(),
      interlocked_compare_exchange_signature,
      sizeof(interlocked_compare_exchange_signature),
      &interlocked_compare_member_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineMemberRef CompareExchange failed");
    return hr;
  }

  /////////////////////////////////////////////
  // Add IL instructions into the IsAlreadyLoaded method
  //
  //  static int _isAssemblyLoaded = 0;
  //  
  //  public static bool IsAlreadyLoaded() {
  //      return Interlocked.CompareExchange(ref _isAssemblyLoaded, 1, 0) == 1;
  //  }
  //
  ILRewriter rewriter_already_loaded(this->info_, nullptr, module_id, alreadyLoadedMethodToken);
  rewriter_already_loaded.InitializeTiny();

  ILInstr* pALFirstInstr = rewriter_already_loaded.GetILList()->m_pNext;
  ILInstr* pALNewInstr = NULL;

  // ldsflda _isAssemblyLoaded : Load the address of the "_isAssemblyLoaded" static var
  pALNewInstr = rewriter_already_loaded.NewILInstr();
  pALNewInstr->m_opcode = CEE_LDSFLDA;
  pALNewInstr->m_Arg32 = isAssemblyLoadedFieldToken;
  rewriter_already_loaded.InsertBefore(pALFirstInstr, pALNewInstr);
  
  // ldc.i4.1 : Load the constant 1 (int) to the stack
  pALNewInstr = rewriter_already_loaded.NewILInstr();
  pALNewInstr->m_opcode = CEE_LDC_I4_1;
  rewriter_already_loaded.InsertBefore(pALFirstInstr, pALNewInstr);

  // ldc.i4.0 : Load the constant 0 (int) to the stack
  pALNewInstr = rewriter_already_loaded.NewILInstr();
  pALNewInstr->m_opcode = CEE_LDC_I4_0;
  rewriter_already_loaded.InsertBefore(pALFirstInstr, pALNewInstr);

  // call int Interlocked.CompareExchange(ref int, int, int) method
  pALNewInstr = rewriter_already_loaded.NewILInstr();
  pALNewInstr->m_opcode = CEE_CALL;
  pALNewInstr->m_Arg32 = interlocked_compare_member_ref;
  rewriter_already_loaded.InsertBefore(pALFirstInstr, pALNewInstr);

  // ldc.i4.1 : Load the constant 1 (int) to the stack
  pALNewInstr = rewriter_already_loaded.NewILInstr();
  pALNewInstr->m_opcode = CEE_LDC_I4_1;
  rewriter_already_loaded.InsertBefore(pALFirstInstr, pALNewInstr);

  // ceq : Compare equality from two values from the stack
  pALNewInstr = rewriter_already_loaded.NewILInstr();
  pALNewInstr->m_opcode = CEE_CEQ;
  rewriter_already_loaded.InsertBefore(pALFirstInstr, pALNewInstr);

  // ret : Return the value of the comparison
  pALNewInstr = rewriter_already_loaded.NewILInstr();
  pALNewInstr->m_opcode = CEE_RET;
  rewriter_already_loaded.InsertBefore(pALFirstInstr, pALNewInstr);
  
  hr = rewriter_already_loaded.Export();
  if (FAILED(hr)) {
    Warn(
        "GenerateVoidILStartupMethod: Call to ILRewriter.Export() failed for "
        "ModuleID=",
        module_id);
    return hr;
  }

  // Define a method on the managed side that will PInvoke into the profiler method:
  // C++: void GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int* assemblySize, BYTE** pSymbolsArray, int* symbolsSize)
  // C#: static extern void GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int assemblySize, out IntPtr symbolsPtr, out int symbolsSize)
  mdMethodDef pinvoke_method_def;
  COR_SIGNATURE get_assembly_bytes_signature[] = {
      IMAGE_CEE_CS_CALLCONV_DEFAULT, // Calling convention
      4,                             // Number of parameters
      ELEMENT_TYPE_VOID,             // Return type
      ELEMENT_TYPE_BYREF,            // List of parameter types
      ELEMENT_TYPE_I,
      ELEMENT_TYPE_BYREF,
      ELEMENT_TYPE_I4,
      ELEMENT_TYPE_BYREF,
      ELEMENT_TYPE_I,
      ELEMENT_TYPE_BYREF,
      ELEMENT_TYPE_I4,
  };
  hr = metadata_emit->DefineMethod(
      new_type_def, "GetAssemblyAndSymbolsBytes"_W.c_str(), mdStatic | mdPinvokeImpl | mdHideBySig,
      get_assembly_bytes_signature, sizeof(get_assembly_bytes_signature), 0, 0,
      &pinvoke_method_def);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineMethod failed");
    return hr;
  }

  metadata_emit->SetMethodImplFlags(pinvoke_method_def, miPreserveSig);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: SetMethodImplFlags failed");
    return hr;
  }

#ifdef _WIN32
  WSTRING native_profiler_file = "DATADOG.TRACE.CLRPROFILER.NATIVE.DLL"_W;
#else // _WIN32

#ifdef BIT64
  WSTRING native_profiler_file = GetEnvironmentValue("CORECLR_PROFILER_PATH_64"_W);
  Debug("GenerateVoidILStartupMethod: Linux: CORECLR_PROFILER_PATH_64 defined as: ", native_profiler_file);
  if (native_profiler_file == ""_W) {
    native_profiler_file = GetEnvironmentValue("CORECLR_PROFILER_PATH"_W);
    Debug("GenerateVoidILStartupMethod: Linux: CORECLR_PROFILER_PATH defined as: ", native_profiler_file);
  }
#else // BIT64
  WSTRING native_profiler_file = GetEnvironmentValue("CORECLR_PROFILER_PATH_32"_W);
  Debug("GenerateVoidILStartupMethod: Linux: CORECLR_PROFILER_PATH_32 defined as: ", native_profiler_file);
  if (native_profiler_file == ""_W) {
    native_profiler_file = GetEnvironmentValue("CORECLR_PROFILER_PATH"_W);
    Debug("GenerateVoidILStartupMethod: Linux: CORECLR_PROFILER_PATH defined as: ", native_profiler_file);
  }
#endif // BIT64
Debug("GenerateVoidILStartupMethod: Linux: Setting the PInvoke native profiler library path to ", native_profiler_file);

#endif // _WIN32

  mdModuleRef profiler_ref;
  hr = metadata_emit->DefineModuleRef(native_profiler_file.c_str(),
                                      &profiler_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineModuleRef failed");
    return hr;
  }

  hr = metadata_emit->DefinePinvokeMap(pinvoke_method_def,
                                       0,
                                       "GetAssemblyAndSymbolsBytes"_W.c_str(),
                                       profiler_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefinePinvokeMap failed");
    return hr;
  }

  // Get a TypeRef for System.Byte
  mdTypeRef byte_type_ref;
  hr = metadata_emit->DefineTypeRefByName(mscorlib_ref,
                                          "System.Byte"_W.c_str(),
                                          &byte_type_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
    return hr;
  }

  // Get a TypeRef for System.Runtime.InteropServices.Marshal
  mdTypeRef marshal_type_ref;
  hr = metadata_emit->DefineTypeRefByName(mscorlib_ref,
                                          "System.Runtime.InteropServices.Marshal"_W.c_str(),
                                          &marshal_type_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
    return hr;
  }

  // Get a MemberRef for System.Runtime.InteropServices.Marshal.Copy(IntPtr, Byte[], int, int)
  mdMemberRef marshal_copy_member_ref;
  COR_SIGNATURE marshal_copy_signature[] = {
      IMAGE_CEE_CS_CALLCONV_DEFAULT, // Calling convention
      4,                             // Number of parameters
      ELEMENT_TYPE_VOID,             // Return type
      ELEMENT_TYPE_I,                // List of parameter types
      ELEMENT_TYPE_SZARRAY,
      ELEMENT_TYPE_U1,
      ELEMENT_TYPE_I4,
      ELEMENT_TYPE_I4
  };
  hr = metadata_emit->DefineMemberRef(
      marshal_type_ref, "Copy"_W.c_str(), marshal_copy_signature,
      sizeof(marshal_copy_signature), &marshal_copy_member_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineMemberRef failed");
    return hr;
  }

  // Get a TypeRef for System.Reflection.Assembly
  mdTypeRef system_reflection_assembly_type_ref;
  hr = metadata_emit->DefineTypeRefByName(mscorlib_ref,
                                          "System.Reflection.Assembly"_W.c_str(),
                                          &system_reflection_assembly_type_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
    return hr;
  }

  // Get a MemberRef for System.Object.ToString()
  mdTypeRef system_object_type_ref;
  hr = metadata_emit->DefineTypeRefByName(mscorlib_ref,
                                          "System.Object"_W.c_str(),
                                          &system_object_type_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
    return hr;
  }

  // Get a TypeRef for System.AppDomain
  mdTypeRef system_appdomain_type_ref;
  hr = metadata_emit->DefineTypeRefByName(mscorlib_ref,
                                          "System.AppDomain"_W.c_str(),
                                          &system_appdomain_type_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineTypeRefByName failed");
    return hr;
  }

  // Get a MemberRef for System.AppDomain.get_CurrentDomain()
  // and System.AppDomain.Assembly.Load(byte[], byte[])

  // Create method signature for AppDomain.CurrentDomain property
  COR_SIGNATURE appdomain_get_current_domain_signature_start[] = {
      IMAGE_CEE_CS_CALLCONV_DEFAULT,
      0,
      ELEMENT_TYPE_CLASS, // ret = System.AppDomain
      // insert compressed token for System.AppDomain TypeRef here
  };
  ULONG start_length = sizeof(appdomain_get_current_domain_signature_start);

  BYTE system_appdomain_type_ref_compressed_token[4];
  ULONG token_length = CorSigCompressToken(system_appdomain_type_ref, system_appdomain_type_ref_compressed_token);

  COR_SIGNATURE* appdomain_get_current_domain_signature = new COR_SIGNATURE[start_length + token_length];
  memcpy(appdomain_get_current_domain_signature,
         appdomain_get_current_domain_signature_start,
         start_length);
  memcpy(&appdomain_get_current_domain_signature[start_length],
         system_appdomain_type_ref_compressed_token,
         token_length);

  mdMemberRef appdomain_get_current_domain_member_ref;
  hr = metadata_emit->DefineMemberRef(
      system_appdomain_type_ref,
      "get_CurrentDomain"_W.c_str(),
      appdomain_get_current_domain_signature,
      start_length + token_length,
      &appdomain_get_current_domain_member_ref);
  delete[] appdomain_get_current_domain_signature;

  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineMemberRef failed");
    return hr;
  }

  // Create method signature for AppDomain.Load(byte[], byte[])
  COR_SIGNATURE appdomain_load_signature_start[] = {
      IMAGE_CEE_CS_CALLCONV_HASTHIS,
      2,
      ELEMENT_TYPE_CLASS  // ret = System.Reflection.Assembly
      // insert compressed token for System.Reflection.Assembly TypeRef here
  };
  COR_SIGNATURE appdomain_load_signature_end[] = {
      ELEMENT_TYPE_SZARRAY,
      ELEMENT_TYPE_U1,
      ELEMENT_TYPE_SZARRAY,
      ELEMENT_TYPE_U1
  };
  start_length = sizeof(appdomain_load_signature_start);
  ULONG end_length = sizeof(appdomain_load_signature_end);

  BYTE system_reflection_assembly_type_ref_compressed_token[4];
  token_length = CorSigCompressToken(system_reflection_assembly_type_ref, system_reflection_assembly_type_ref_compressed_token);

  COR_SIGNATURE* appdomain_load_signature = new COR_SIGNATURE[start_length + token_length + end_length];
  memcpy(appdomain_load_signature,
         appdomain_load_signature_start,
         start_length);
  memcpy(&appdomain_load_signature[start_length],
         system_reflection_assembly_type_ref_compressed_token,
         token_length);
  memcpy(&appdomain_load_signature[start_length + token_length],
         appdomain_load_signature_end,
         end_length);

  mdMemberRef appdomain_load_member_ref;
  hr = metadata_emit->DefineMemberRef(
      system_appdomain_type_ref, "Load"_W.c_str(),
      appdomain_load_signature,
      start_length + token_length + end_length,
      &appdomain_load_member_ref);
  delete[] appdomain_load_signature;

  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineMemberRef failed");
    return hr;
  }

  // Create method signature for Assembly.CreateInstance(string)
  COR_SIGNATURE assembly_create_instance_signature[] = {
      IMAGE_CEE_CS_CALLCONV_HASTHIS,
      1,
      ELEMENT_TYPE_OBJECT,  // ret = System.Object
      ELEMENT_TYPE_STRING
  };

  mdMemberRef assembly_create_instance_member_ref;
  hr = metadata_emit->DefineMemberRef(
      system_reflection_assembly_type_ref, "CreateInstance"_W.c_str(),
      assembly_create_instance_signature,
      sizeof(assembly_create_instance_signature),
      &assembly_create_instance_member_ref);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineMemberRef failed");
    return hr;
  }

  // Create a string representing "Datadog.Trace.ClrProfiler.Managed.Loader.Startup"
  // Create OS-specific implementations because on Windows, creating the string via
  // "Datadog.Trace.ClrProfiler.Managed.Loader.Startup"_W.c_str() does not create the
  // proper string for CreateInstance to successfully call
#ifdef _WIN32
  LPCWSTR load_helper_str =
      L"Datadog.Trace.ClrProfiler.Managed.Loader.Startup";
  auto load_helper_str_size = wcslen(load_helper_str);
#else
  char16_t load_helper_str[] =
      u"Datadog.Trace.ClrProfiler.Managed.Loader.Startup";
  auto load_helper_str_size = std::char_traits<char16_t>::length(load_helper_str);
#endif

  mdString load_helper_token;
  hr = metadata_emit->DefineUserString(load_helper_str, (ULONG) load_helper_str_size,
                                  &load_helper_token);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineUserString failed");
    return hr;
  }

  // Generate a locals signature defined in the following way:
  //   [0] System.IntPtr ("assemblyPtr" - address of assembly bytes)
  //   [1] System.Int32  ("assemblySize" - size of assembly bytes)
  //   [2] System.IntPtr ("symbolsPtr" - address of symbols bytes)
  //   [3] System.Int32  ("symbolsSize" - size of symbols bytes)
  //   [4] System.Byte[] ("assemblyBytes" - managed byte array for assembly)
  //   [5] System.Byte[] ("symbolsBytes" - managed byte array for symbols)
  //   [6] class System.Reflection.Assembly ("loadedAssembly" - assembly instance to save loaded assembly)
  mdSignature locals_signature_token;
  COR_SIGNATURE locals_signature[15] = {
      IMAGE_CEE_CS_CALLCONV_LOCAL_SIG, // Calling convention
      7,                               // Number of variables
      ELEMENT_TYPE_I,                  // List of variable types
      ELEMENT_TYPE_I4,
      ELEMENT_TYPE_I,
      ELEMENT_TYPE_I4,
      ELEMENT_TYPE_SZARRAY,
      ELEMENT_TYPE_U1,
      ELEMENT_TYPE_SZARRAY,
      ELEMENT_TYPE_U1,
      ELEMENT_TYPE_CLASS
      // insert compressed token for System.Reflection.Assembly TypeRef here
  };
  CorSigCompressToken(system_reflection_assembly_type_ref,
                      &locals_signature[11]);
  hr = metadata_emit->GetTokenFromSig(locals_signature, sizeof(locals_signature),
                                 &locals_signature_token);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: Unable to generate locals signature. ModuleID=", module_id);
    return hr;
  }

  /////////////////////////////////////////////
  // Add IL instructions into the void method
  ILRewriter rewriter_void(this->info_, nullptr, module_id, *ret_method_token);
  rewriter_void.InitializeTiny();
  rewriter_void.SetTkLocalVarSig(locals_signature_token);

  ILInstr* pFirstInstr = rewriter_void.GetILList()->m_pNext;
  ILInstr* pNewInstr = NULL;

  // Step 0) Check if the assembly was already loaded

  // call bool IsAlreadyLoaded()
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_CALL;
  pNewInstr->m_Arg32 = alreadyLoadedMethodToken;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // check if the return of the method call is true or false
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_BRFALSE_S;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);
  ILInstr* pBranchFalseInstr = pNewInstr;
  
  // return if IsAlreadyLoaded is true
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_RET;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // Step 1) Call void GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int assemblySize, out IntPtr symbolsPtr, out int symbolsSize)

  // ldloca.s 0 : Load the address of the "assemblyPtr" variable (locals index 0)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOCA_S;
  pNewInstr->m_Arg32 = 0;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // Set the false branch target
  pBranchFalseInstr->m_pTarget = pNewInstr;

  // ldloca.s 1 : Load the address of the "assemblySize" variable (locals index 1)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOCA_S;
  pNewInstr->m_Arg32 = 1;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // ldloca.s 2 : Load the address of the "symbolsPtr" variable (locals index 2)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOCA_S;
  pNewInstr->m_Arg32 = 2;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // ldloca.s 3 : Load the address of the "symbolsSize" variable (locals index 3)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOCA_S;
  pNewInstr->m_Arg32 = 3;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // call void GetAssemblyAndSymbolsBytes(out IntPtr assemblyPtr, out int assemblySize, out IntPtr symbolsPtr, out int symbolsSize)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_CALL;
  pNewInstr->m_Arg32 = pinvoke_method_def;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // Step 2) Call void Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length) to populate the managed assembly bytes

  // ldloc.1 : Load the "assemblySize" variable (locals index 1)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOC_1;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // newarr System.Byte : Create a new Byte[] to hold a managed copy of the assembly data
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_NEWARR;
  pNewInstr->m_Arg32 = byte_type_ref;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // stloc.s 4 : Assign the Byte[] to the "assemblyBytes" variable (locals index 4)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_STLOC_S;
  pNewInstr->m_Arg8 = 4;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // ldloc.0 : Load the "assemblyPtr" variable (locals index 0)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOC_0;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // ldloc.s 4 : Load the "assemblyBytes" variable (locals index 4)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOC_S;
  pNewInstr->m_Arg8 = 4;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // ldc.i4.0 : Load the integer 0 for the Marshal.Copy startIndex parameter
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDC_I4_0;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // ldloc.1 : Load the "assemblySize" variable (locals index 1) for the Marshal.Copy length parameter
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOC_1;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // call Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_CALL;
  pNewInstr->m_Arg32 = marshal_copy_member_ref;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // Step 3) Call void Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length) to populate the symbols bytes

  // ldloc.3 : Load the "symbolsSize" variable (locals index 3)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOC_3;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // newarr System.Byte : Create a new Byte[] to hold a managed copy of the symbols data
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_NEWARR;
  pNewInstr->m_Arg32 = byte_type_ref;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // stloc.s 5 : Assign the Byte[] to the "symbolsBytes" variable (locals index 5)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_STLOC_S;
  pNewInstr->m_Arg8 = 5;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // ldloc.2 : Load the "symbolsPtr" variables (locals index 2)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOC_2;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // ldloc.s 5 : Load the "symbolsBytes" variable (locals index 5)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOC_S;
  pNewInstr->m_Arg8 = 5;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // ldc.i4.0 : Load the integer 0 for the Marshal.Copy startIndex parameter
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDC_I4_0;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // ldloc.3 : Load the "symbolsSize" variable (locals index 3) for the Marshal.Copy length parameter
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOC_3;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // call void Marshal.Copy(IntPtr source, byte[] destination, int startIndex, int length)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_CALL;
  pNewInstr->m_Arg32 = marshal_copy_member_ref;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // Step 4) Call System.Reflection.Assembly System.AppDomain.CurrentDomain.Load(byte[], byte[]))

  // call System.AppDomain System.AppDomain.CurrentDomain property
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_CALL;
  pNewInstr->m_Arg32 = appdomain_get_current_domain_member_ref;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // ldloc.s 4 : Load the "assemblyBytes" variable (locals index 4) for the first byte[] parameter of AppDomain.Load(byte[], byte[])
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOC_S;
  pNewInstr->m_Arg8 = 4;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // ldloc.s 5 : Load the "symbolsBytes" variable (locals index 5) for the second byte[] parameter of AppDomain.Load(byte[], byte[])
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOC_S;
  pNewInstr->m_Arg8 = 5;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // callvirt System.Reflection.Assembly System.AppDomain.Load(uint8[], uint8[])
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_CALLVIRT;
  pNewInstr->m_Arg32 = appdomain_load_member_ref;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // stloc.s 6 : Assign the System.Reflection.Assembly object to the "loadedAssembly" variable (locals index 6)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_STLOC_S;
  pNewInstr->m_Arg8 = 6;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // Step 4) Call instance method Assembly.CreateInstance("Datadog.Trace.ClrProfiler.Managed.Loader.Startup")

  // ldloc.s 6 : Load the "loadedAssembly" variable (locals index 6) to call Assembly.CreateInstance
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDLOC_S;
  pNewInstr->m_Arg8 = 6;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // ldstr "Datadog.Trace.ClrProfiler.Managed.Loader.Startup"
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_LDSTR;
  pNewInstr->m_Arg32 = load_helper_token;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // callvirt System.Object System.Reflection.Assembly.CreateInstance(string)
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_CALLVIRT;
  pNewInstr->m_Arg32 = assembly_create_instance_member_ref;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // pop the returned object
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_POP;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  // return
  pNewInstr = rewriter_void.NewILInstr();
  pNewInstr->m_opcode = CEE_RET;
  rewriter_void.InsertBefore(pFirstInstr, pNewInstr);

  hr = rewriter_void.Export();
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: Call to ILRewriter.Export() failed for ModuleID=", module_id);
    return hr;
  }

  return S_OK;
}

HRESULT CorProfiler::AddIISPreStartInitFlags(
    const ModuleID module_id,
    const mdToken function_token) {
  ComPtr<IUnknown> metadata_interfaces;
  auto hr = this->info_->GetModuleMetaData(module_id, ofRead | ofWrite,
                                           IID_IMetaDataImport2,
                                           metadata_interfaces.GetAddressOf());
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: failed to get metadata interface for ",
         module_id);
    return hr;
  }

  const auto metadata_import =
      metadata_interfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
  const auto metadata_emit =
      metadata_interfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
  const auto assembly_import = metadata_interfaces.As<IMetaDataAssemblyImport>(
      IID_IMetaDataAssemblyImport);
  const auto assembly_emit =
      metadata_interfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

  ILRewriter rewriter(this->info_, nullptr, module_id, function_token);
  hr = rewriter.Import();

  if (FAILED(hr)) {
    Warn("RunILStartupHook: Call to ILRewriter.Import() failed for ", module_id,
         " ", function_token);
    return hr;
  }

  ILRewriterWrapper rewriter_wrapper(&rewriter);

  // Get mscorlib assembly ref
  mdModuleRef mscorlib_ref;
  hr = CreateAssemblyRefToMscorlib(assembly_emit, &mscorlib_ref);

  // Get System.Boolean type token
  mdToken boolToken;
  metadata_emit->DefineTypeRefByName(mscorlib_ref, SystemBoolean.data(),
                                     &boolToken);

  // Get System.AppDomain type ref
  mdTypeRef system_appdomain_type_ref;
  hr = metadata_emit->DefineTypeRefByName(
      mscorlib_ref, "System.AppDomain"_W.c_str(), &system_appdomain_type_ref);
  if (FAILED(hr)) {
    Warn("Wrapper objectTypeRef could not be defined.");
    return hr;
  }

  // Get a MemberRef for System.AppDomain.get_CurrentDomain()
  COR_SIGNATURE appdomain_get_current_domain_signature_start[] = {
      IMAGE_CEE_CS_CALLCONV_DEFAULT,
      0,
      ELEMENT_TYPE_CLASS,  // ret = System.AppDomain
      // insert compressed token for System.AppDomain TypeRef here
  };
  ULONG start_length = sizeof(appdomain_get_current_domain_signature_start);

  BYTE system_appdomain_type_ref_compressed_token[4];
  ULONG token_length = CorSigCompressToken(
      system_appdomain_type_ref, system_appdomain_type_ref_compressed_token);

  COR_SIGNATURE* appdomain_get_current_domain_signature =
      new COR_SIGNATURE[start_length + token_length];
  memcpy(appdomain_get_current_domain_signature,
         appdomain_get_current_domain_signature_start, start_length);
  memcpy(&appdomain_get_current_domain_signature[start_length],
         system_appdomain_type_ref_compressed_token, token_length);

  mdMemberRef appdomain_get_current_domain_member_ref;
  hr = metadata_emit->DefineMemberRef(
      system_appdomain_type_ref, "get_CurrentDomain"_W.c_str(),
      appdomain_get_current_domain_signature, start_length + token_length,
      &appdomain_get_current_domain_member_ref);
  delete[] appdomain_get_current_domain_signature;

  // Get AppDomain.SetData
  COR_SIGNATURE appdomain_set_data_signature[] = {
      IMAGE_CEE_CS_CALLCONV_DEFAULT | IMAGE_CEE_CS_CALLCONV_HASTHIS,  // Calling convention
      2,                              // Number of parameters
      ELEMENT_TYPE_VOID,              // Return type
      ELEMENT_TYPE_STRING,             // List of parameter types
      ELEMENT_TYPE_OBJECT
  };
  mdMemberRef appdomain_set_data_member_ref;
  hr = metadata_emit->DefineMemberRef(
      system_appdomain_type_ref, "SetData"_W.c_str(),
      appdomain_set_data_signature,
      sizeof(appdomain_set_data_signature),
      &appdomain_set_data_member_ref);

  // Define "Datadog_IISPreInitStart" string
  // Create a string representing
  // "Datadog.Trace.ClrProfiler.Managed.Loader.Startup" Create OS-specific
  // implementations because on Windows, creating the string via
  // "Datadog.Trace.ClrProfiler.Managed.Loader.Startup"_W.c_str() does not
  // create the proper string for CreateInstance to successfully call
#ifdef _WIN32
  LPCWSTR pre_init_start_str = L"Datadog_IISPreInitStart";
  auto pre_init_start_str_size = wcslen(pre_init_start_str);
#else
  char16_t load_helper_str[] =
      u"Datadog_IISPreInitStart";
  auto pre_init_start_str_size =
      std::char_traits<char16_t>::length(pre_init_start_str);
#endif

  mdString pre_init_start_string_token;
  hr = metadata_emit->DefineUserString(pre_init_start_str,
                                       (ULONG)pre_init_start_str_size,
                                       &pre_init_start_string_token);
  if (FAILED(hr)) {
    Warn("GenerateVoidILStartupMethod: DefineUserString failed");
    return hr;
  }

  // Get first instruction and set the rewriter to that location
  ILInstr* pInstr = rewriter.GetILList()->m_pNext;
  rewriter_wrapper.SetILPosition(pInstr);
  ILInstr* pCurrentInstr = NULL;
  ILInstr* pNewInstr = NULL;

  //////////////////////////////////////////////////
  // At the beginning of the method, call
  // AppDomain.CurrentDomain.SetData(string, true)

  // Call AppDomain.get_CurrentDomain
  rewriter_wrapper.CallMember(appdomain_get_current_domain_member_ref, false);

  // ldstr "Datadog_IISPreInitStart"
  pCurrentInstr = rewriter_wrapper.GetCurrentILInstr();
  pNewInstr = rewriter.NewILInstr();
  pNewInstr->m_opcode = CEE_LDSTR;
  pNewInstr->m_Arg32 = pre_init_start_string_token;
  rewriter.InsertBefore(pCurrentInstr, pNewInstr);

  // load a boxed version of the boolean true
  rewriter_wrapper.LoadInt32(1);
  rewriter_wrapper.Box(boolToken);

  // Call AppDomain.SetData(string, object)
  rewriter_wrapper.CallMember(appdomain_set_data_member_ref, true);

  //////////////////////////////////////////////////
  // At the end of the method, call
  // AppDomain.CurrentDomain.SetData(string, false)
  pInstr = rewriter.GetILList()->m_pPrev;  // The last instruction should be a 'ret' instruction

  // Append a ret instruction so we can use the existing ret as the first instruction for our rewriting
  pNewInstr = rewriter.NewILInstr();
  pNewInstr->m_opcode = CEE_RET;
  rewriter.InsertAfter(pInstr, pNewInstr);
  rewriter_wrapper.SetILPosition(pNewInstr);

  // Call AppDomain.get_CurrentDomain
  // Special case: rewrite the previous ret instruction with this call
  pInstr->m_opcode = CEE_CALL;
  pInstr->m_Arg32 = appdomain_get_current_domain_member_ref;

  // ldstr "Datadog_IISPreInitStart"
  pCurrentInstr = rewriter_wrapper.GetCurrentILInstr();
  pNewInstr = rewriter.NewILInstr();
  pNewInstr->m_opcode = CEE_LDSTR;
  pNewInstr->m_Arg32 = pre_init_start_string_token;
  rewriter.InsertBefore(pCurrentInstr, pNewInstr);

  // load a boxed version of the boolean false
  rewriter_wrapper.LoadInt32(0);
  rewriter_wrapper.Box(boolToken);

  // Call AppDomain.SetData(string, object)
  rewriter_wrapper.CallMember(appdomain_set_data_member_ref, true);

  //////////////////////////////////////////////////
  // Finished with the IL rewriting, save the result
  hr = rewriter.Export();

  if (FAILED(hr)) {
    Warn("RunILStartupHook: Call to ILRewriter.Export() failed for ModuleID=",
         module_id, " ", function_token);
    return hr;
  }

  return S_OK;
}

#ifdef LINUX
extern uint8_t dll_start[] asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_dll_start");
extern uint8_t dll_end[] asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_dll_end");

extern uint8_t pdb_start[] asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_pdb_start");
extern uint8_t pdb_end[] asm("_binary_Datadog_Trace_ClrProfiler_Managed_Loader_pdb_end");
#endif

void CorProfiler::GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int* assemblySize, BYTE** pSymbolsArray, int* symbolsSize) const {
#ifdef _WIN32
  HINSTANCE hInstance = DllHandle;
  LPCWSTR dllLpName;
  LPCWSTR symbolsLpName;

  if (runtime_information_.is_desktop()) {
    dllLpName = MAKEINTRESOURCE(NET45_MANAGED_ENTRYPOINT_DLL);
    symbolsLpName = MAKEINTRESOURCE(NET45_MANAGED_ENTRYPOINT_SYMBOLS);
  } else {
    dllLpName = MAKEINTRESOURCE(NETCOREAPP20_MANAGED_ENTRYPOINT_DLL);
    symbolsLpName = MAKEINTRESOURCE(NETCOREAPP20_MANAGED_ENTRYPOINT_SYMBOLS);
  }

  HRSRC hResAssemblyInfo = FindResource(hInstance, dllLpName, L"ASSEMBLY");
  HGLOBAL hResAssembly = LoadResource(hInstance, hResAssemblyInfo);
  *assemblySize = SizeofResource(hInstance, hResAssemblyInfo);
  *pAssemblyArray = (LPBYTE)LockResource(hResAssembly);

  HRSRC hResSymbolsInfo = FindResource(hInstance, symbolsLpName, L"SYMBOLS");
  HGLOBAL hResSymbols = LoadResource(hInstance, hResSymbolsInfo);
  *symbolsSize = SizeofResource(hInstance, hResSymbolsInfo);
  *pSymbolsArray = (LPBYTE)LockResource(hResSymbols);
#elif LINUX
  *assemblySize = dll_end - dll_start;
  *pAssemblyArray = (BYTE*)dll_start;

  *symbolsSize = pdb_end - pdb_start;
  *pSymbolsArray = (BYTE*)pdb_start;
#else
    const unsigned int imgCount = _dyld_image_count();

    for(auto i = 0; i < imgCount; i++) {
        const std::string name = std::string(_dyld_get_image_name(i));

        if (name.rfind("Datadog.Trace.ClrProfiler.Native.dylib") != std::string::npos) {
            const mach_header_64* header = (const struct mach_header_64 *) _dyld_get_image_header(i);

            unsigned long dllSize;
            const auto dllData = getsectiondata(header, "binary", "dll", &dllSize);
            *assemblySize = dllSize;
            *pAssemblyArray = (BYTE*)dllData;

            unsigned long pdbSize;
            const auto pdbData = getsectiondata(header, "binary", "pdb", &pdbSize);
            *symbolsSize = pdbSize;
            *pSymbolsArray = (BYTE*)pdbData;
            break;
        }
    }
#endif
}


// ***
// * ReJIT Methods
// ***

HRESULT STDMETHODCALLTYPE CorProfiler::ReJITCompilationStarted(FunctionID functionId, ReJITID rejitId, BOOL fIsSafeToBlock) {
  if (!is_attached_) {
    return S_OK;
  }
  Debug("ReJITCompilationStarted: [functionId: ", functionId, ", rejitId: ", rejitId, ", safeToBlock: ", fIsSafeToBlock, "]");
  // we notify the reJIT handler of this event
  return rejit_handler->NotifyReJITCompilationStarted(functionId, rejitId);
}

HRESULT STDMETHODCALLTYPE CorProfiler::GetReJITParameters(ModuleID moduleId, mdMethodDef methodId, ICorProfilerFunctionControl* pFunctionControl) {
  if (!is_attached_) {
    return S_OK;
  }

  Debug("GetReJITParameters: [moduleId: ", moduleId, ", methodId: ", methodId, "]");

  // we get the module_metadata from the moduleId. 
  ModuleMetadata* module_metadata = nullptr;
  {
    std::lock_guard<std::mutex> guard(module_id_to_info_map_lock_);
    if (module_id_to_info_map_.count(moduleId) > 0) {
      module_metadata = module_id_to_info_map_[moduleId];
    } else {
      return S_OK;
    }
  }

  // we notify the reJIT handler of this event and pass the module_metadata.
  return rejit_handler->NotifyReJITParameters(moduleId, methodId, pFunctionControl, module_metadata);
}

HRESULT STDMETHODCALLTYPE CorProfiler::ReJITCompilationFinished(FunctionID functionId, ReJITID rejitId, HRESULT hrStatus, BOOL fIsSafeToBlock) {
  if (!is_attached_) {
    return S_OK;
  }

  Debug("ReJITCompilationFinished: [functionId: ", functionId, ", rejitId: ", rejitId, ", hrStatus: ", hrStatus, ", safeToBlock: ", fIsSafeToBlock, "]");
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ReJITError(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId, HRESULT hrStatus) {
  if (!is_attached_) {
    return S_OK;
  }

  Warn("ReJITError: [functionId: ", functionId, ", moduleId: ", moduleId, ", methodId: ", methodId, ", hrStatus: ", hrStatus, "]");
  return S_OK;
}

// ***
// * CallTarget Methods
// ***

/// <summary>
/// Search for methods to instrument in a module and request a ReJIT to them for a CallTarget instrumentation
/// </summary>
/// <param name="module_id">Module id</param>
/// <param name="module_metadata">Module metadata for the module</param>
/// <param name="filtered_integrations">Filtered vector of integrations to be applied</param>
/// <returns>Number of ReJIT requests made</returns>
size_t CorProfiler::CallTarget_RequestRejitForModule(ModuleID module_id, ModuleMetadata* module_metadata, const std::vector<IntegrationMethod> &filtered_integrations) {
  auto metadata_import = module_metadata->metadata_import;
  std::vector<ModuleID> vtModules;
  std::vector<mdMethodDef> vtMethodDefs;

  for (const IntegrationMethod& integration : filtered_integrations) {

    // If the integration is not for the current assembly we skip.
    if (integration.replacement.target_method.assembly.name != module_metadata->assemblyName) {
      continue;
    }

    // If the integration mode is not CallTarget we skip.
    if (integration.replacement.wrapper_method.action != calltarget_modification_action) {
      continue;
    }

    // We are in the right module, so we try to load the mdTypeDef from the integration target type name.
    mdTypeDef typeDef = mdTypeDefNil;
    auto hr = metadata_import->FindTypeDefByName(integration.replacement.target_method.type_name.c_str(), mdTokenNil, &typeDef);
    if (FAILED(hr)) {
      Warn("Can't load the TypeDef for: ", integration.replacement.target_method.type_name, ", Module: ", module_metadata->assemblyName);
      continue;
    }

    // Now we enumerate all methods with the same target method name. (All overloads of the method)
    auto enumMethods = Enumerator<mdMethodDef>(
        [metadata_import, integration, typeDef](HCORENUM* ptr, mdMethodDef arr[], ULONG max, ULONG* cnt) -> HRESULT {
          return metadata_import->EnumMethodsWithName(ptr, typeDef, integration.replacement.target_method.method_name.c_str(), arr, max, cnt);
        },
        [metadata_import](HCORENUM ptr) -> void {
          metadata_import->CloseEnum(ptr);
        });

    auto enumIterator = enumMethods.begin();
    while (enumIterator != enumMethods.end()) {
      auto methodDef = *enumIterator;

      // Extract the function info from the mdMethodDef
      const auto caller = GetFunctionInfo(module_metadata->metadata_import, methodDef);
      if (!caller.IsValid()) {
        Warn("The caller for the methoddef: ", TokenStr(&methodDef), " is not valid!");
        enumIterator = ++enumIterator;
        continue;
      }

      // We create a new function info into the heap from the caller functionInfo in the stack, to be used later in the ReJIT process
      auto functionInfo = new FunctionInfo(caller);
      hr = functionInfo->method_signature.TryParse();
      if (FAILED(hr)) {
        Warn("The method signature: ", functionInfo->method_signature.str(), " cannot be parsed.");
        delete functionInfo;
        enumIterator = ++enumIterator;
        continue;
      }

      // Compare if the current mdMethodDef contains the same number of arguments as the instrumentation target
      const auto numOfArgs = functionInfo->method_signature.NumberOfArguments();
      if (numOfArgs != integration.replacement.target_method.signature_types.size() - 1) {
        Debug("The caller for the methoddef: ", integration.replacement.target_method.method_name, " doesn't have the right number of arguments.");
        delete functionInfo;
        enumIterator = ++enumIterator;
        continue;
      }

      // Compare each mdMethodDef argument type to the instrumentation target
      bool argumentsMismatch = false;
      const auto methodArguments = functionInfo->method_signature.GetMethodArguments();
      Debug("Comparing signature for method: ", integration.replacement.target_method.type_name, ".", integration.replacement.target_method.method_name);
      for (unsigned int i = 0; i < numOfArgs; i++) {
        const auto argumentTypeName = methodArguments[i].GetTypeTokName(metadata_import);
        const auto integrationArgumentTypeName = integration.replacement.target_method.signature_types[i + 1];
        Debug("  -> ", argumentTypeName, " = ", integrationArgumentTypeName);
        if (argumentTypeName != integrationArgumentTypeName && integrationArgumentTypeName != "_"_W) {
          argumentsMismatch = true;
          break;
        }
      }
      if (argumentsMismatch) {
        Debug("The caller for the methoddef: ", integration.replacement.target_method.method_name, " doesn't have the right type of arguments.");
        delete functionInfo;
        enumIterator = ++enumIterator;
        continue;
      }

      // As we are in the right method, we gather all information we need and stored it in to the ReJIT handler.
      auto moduleHandler = rejit_handler->GetOrAddModule(module_id);
      moduleHandler->SetModuleMetadata(module_metadata);
      auto methodHandler = moduleHandler->GetOrAddMethod(methodDef);
      methodHandler->SetFunctionInfo(functionInfo);
      methodHandler->SetMethodReplacement(new MethodReplacement(integration.replacement));

      // Store module_id and methodDef to request the ReJIT after analyzing all integrations.
      vtModules.push_back(module_id);
      vtMethodDefs.push_back(methodDef);
      
      bool caller_assembly_is_domain_neutral = runtime_information_.is_desktop() && corlib_module_loaded && module_metadata->app_domain_id == corlib_app_domain_id;

      Info("Enqueue for ReJIT [ModuleId=", module_id,
           ", MethodDef=", TokenStr(&methodDef), 
           ", AppDomainId=", module_metadata->app_domain_id,
           ", IsDomainNeutral=", caller_assembly_is_domain_neutral,
           ", Assembly=", module_metadata->assemblyName, 
           ", Type=", caller.type.name, 
           ", Method=", caller.name, 
           ", Signature=", caller.signature.str(),
           "]");
      
      enumIterator = ++enumIterator;
    }
  }

  // Request the ReJIT for all integrations found in the module.
  if (!vtMethodDefs.empty()) {
    Info("Requesting ReJIT for ", vtMethodDefs.size(), " methods.");
    this->info_->RequestReJIT((ULONG)vtMethodDefs.size(), vtModules.data(), vtMethodDefs.data());
  }

  // We return the number of ReJIT requests
  return vtMethodDefs.size();
}

/// <summary>
/// Rewrite the target method body with the calltarget implementation. (This is function is triggered by the ReJIT handler)
/// Resulting code structure:
/// 
/// - Add locals for TReturn (if non-void method), CallTargetState, CallTargetReturn/CallTargetReturn<TReturn>, Exception
/// - Initialize locals
///
/// try
/// {
///   try
///   {
///     try
///     {
///       - Invoke BeginMethod with object instance (or null if static method) and original method arguments
///       - Store result into CallTargetState local
///     }
///     catch
///     {
///       - Invoke LogException(Exception)
///     }
///
///     - Execute original method instructions
///       * All RET instructions are replaced with a LEAVE_S. If non-void method, the value on the stack is first stored in the TReturn local.
///   }
///   catch (Exception)
///   {
///     - Store exception into Exception local
///     - throw
///   }
/// }
/// finally
/// {
///   try
///   {
///     - Invoke EndMethod with object instance (or null if static method), TReturn local (if non-void method), CallTargetState local, and Exception local
///     - Store result into CallTargetReturn/CallTargetReturn<TReturn> local
///     - If non-void method, store CallTargetReturn<TReturn>.GetReturnValue() into TReturn local
///   }
///   catch
///   {
///     - Invoke LogException(Exception)
///   }
/// }
///
/// - If non-void method, load TReturn local
/// - RET
/// </summary>
/// <param name="moduleHandler">Module ReJIT handler representation</param>
/// <param name="methodHandler">Method ReJIT handler representation</param>
/// <returns>Result of the rewriting</returns>
HRESULT CorProfiler::CallTarget_RewriterCallback(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) {
  ModuleID module_id = moduleHandler->GetModuleId();
  ModuleMetadata* module_metadata = moduleHandler->GetModuleMetadata();
  FunctionInfo* caller = methodHandler->GetFunctionInfo();
  CallTargetTokens* callTargetTokens = module_metadata->GetCallTargetTokens();
  mdToken function_token = caller->id;
  FunctionMethodArgument retFuncArg = caller->method_signature.GetRet();
  MethodReplacement* method_replacement = methodHandler->GetMethodReplacement();
  unsigned int retFuncElementType;
  int retTypeFlags = retFuncArg.GetTypeFlags(retFuncElementType);
  bool isVoid = (retTypeFlags & TypeFlagVoid) > 0;
  bool isStatic = !(caller->method_signature.CallingConvention() & IMAGE_CEE_CS_CALLCONV_HASTHIS);
  std::vector<FunctionMethodArgument> methodArguments = caller->method_signature.GetMethodArguments();
  int numArgs = caller->method_signature.NumberOfArguments();
  auto metaEmit = module_metadata->metadata_emit;
  auto metaImport = module_metadata->metadata_import;

  // *** Get all references to the wrapper type
  mdMemberRef wrapper_method_ref = mdMemberRefNil;
  mdTypeRef wrapper_type_ref = mdTypeRefNil;
  GetWrapperMethodRef(module_metadata, module_id, *method_replacement, wrapper_method_ref, wrapper_type_ref);

  Debug("*** CallTarget_RewriterCallback() Start: ", caller->type.name, ".", caller->name, 
       "() [IsVoid=", isVoid, 
       ", IsStatic=", isStatic, 
       ", IntegrationType=", method_replacement->wrapper_method.type_name,
       ", Arguments=", numArgs, 
       "]");

  // *** Create rewriter
  ILRewriter rewriter(this->info_, methodHandler->GetFunctionControl(), module_id, function_token);
  bool modified = false;
  auto hr = rewriter.Import();
  if (FAILED(hr)) {
    Warn("*** CallTarget_RewriterCallback(): Call to ILRewriter.Import() failed for ", module_id, " ", function_token);
    return hr;
  }

  // *** Store the original il code text if the dump_il option is enabled.
  std::string original_code;
  if (dump_il_rewrite_enabled) {
    original_code = GetILCodes(
        "*** CallTarget_RewriterCallback(): Original Code: ", &rewriter,
        *caller, module_metadata);
  }

  // *** Create the rewriter wrapper helper
  ILRewriterWrapper reWriterWrapper(&rewriter);
  reWriterWrapper.SetILPosition(rewriter.GetILList()->m_pNext);

  // *** Modify the Local Var Signature of the method and initialize the new local vars
  ULONG callTargetStateIndex = static_cast<ULONG>(ULONG_MAX);
  ULONG exceptionIndex = static_cast<ULONG>(ULONG_MAX);
  ULONG callTargetReturnIndex = static_cast<ULONG>(ULONG_MAX);
  ULONG returnValueIndex = static_cast<ULONG>(ULONG_MAX);
  mdToken callTargetStateToken = mdTokenNil;
  mdToken exceptionToken = mdTokenNil;
  mdToken callTargetReturnToken = mdTokenNil;
  ILInstr* firstInstruction;
  callTargetTokens->ModifyLocalSigAndInitialize(&reWriterWrapper, caller, 
      &callTargetStateIndex, &exceptionIndex, 
      &callTargetReturnIndex, &returnValueIndex, 
      &callTargetStateToken,
      &exceptionToken, &callTargetReturnToken, &firstInstruction);

  // ***
  // BEGIN METHOD PART
  // ***

  // *** Load instance into the stack (if not static)
  if (isStatic) {
    if (caller->type.valueType) {
      // Static methods in a ValueType can't be instrumented. 
      // In the future this can be supported by adding a local for the valuetype and initialize it to the default value.
      // After the signature modification we need to emit the following IL to initialize and load into the stack.
      //    ldloca.s [localIndex]
      //    initobj [valueType]
      //    ldloc.s [localIndex]
      Warn("*** CallTarget_RewriterCallback(): Static methods in a ValueType cannot be instrumented. ");
      return E_FAIL;
    }
    reWriterWrapper.LoadNull();
  } else {
    reWriterWrapper.LoadArgument(0);
    if (caller->type.valueType) {
      if (caller->type.type_spec != mdTypeSpecNil) {
        reWriterWrapper.LoadObj(caller->type.type_spec);
      } 
      else if (!caller->type.isGeneric) {
        reWriterWrapper.LoadObj(caller->type.id);
      } else {
        // Generic struct instrumentation is not supported
        // IMetaDataImport::GetMemberProps and IMetaDataImport::GetMemberRefProps returns 
        // The parent token as mdTypeDef and not as a mdTypeSpec
        // that's because the method definition is stored in the mdTypeDef
        // The problem is that we don't have the exact Spec of that generic
        // We can't emit LoadObj or Box because that would result in an invalid IL.
        // This problem doesn't occur on a class type because we can always relay in the
        // object type.
        return E_FAIL;
      }
    }
  }

  // *** Load the method arguments to the stack
  unsigned elementType;
  if (numArgs <= 6) {
    // Load the arguments directly (FastPath)
    for (int i = 0; i < numArgs; i++) {
      reWriterWrapper.LoadArgument(i + (isStatic ? 0 : 1));
      auto argTypeFlags = methodArguments[i].GetTypeFlags(elementType);
      if (argTypeFlags & TypeFlagByRef) {
        Warn(
            "*** CallTarget_RewriterCallback(): Methods with ref parameters "
            "cannot be instrumented. ");
        return E_FAIL;
      }
    }
  } else {
    // Load the arguments inside an object array (SlowPath)
    reWriterWrapper.CreateArray(callTargetTokens->GetObjectTypeRef(), numArgs);
    for (int i = 0; i < numArgs; i++) {
      reWriterWrapper.BeginLoadValueIntoArray(i);
      reWriterWrapper.LoadArgument(i + (isStatic ? 0 : 1));
      auto argTypeFlags = methodArguments[i].GetTypeFlags(elementType);
      if (argTypeFlags & TypeFlagByRef) {
        Warn(
            "*** CallTarget_RewriterCallback(): Methods with ref parameters "
            "cannot be instrumented. ");
        return E_FAIL;
      }
      if (argTypeFlags & TypeFlagBoxedType) {
        auto tok = methodArguments[i].GetTypeTok(
            metaEmit, callTargetTokens->GetCorLibAssemblyRef());
        if (tok == mdTokenNil) {
          return E_FAIL;
        }
        reWriterWrapper.Box(tok);
      }
      reWriterWrapper.EndLoadValueIntoArray();
    }
  }

  // *** Emit BeginMethod call
  ILInstr* beginCallInstruction;
  switch (numArgs) { 
    case 0: {
      IfFailRet(callTargetTokens->WriteBeginMethodWithoutArguments(
          &reWriterWrapper, wrapper_type_ref, &caller->type, 
          &beginCallInstruction));
      break;
    }
    case 1: {
      IfFailRet(callTargetTokens->WriteBeginMethodWithArguments(
          &reWriterWrapper, wrapper_type_ref, &caller->type,
          &methodArguments[0], &beginCallInstruction));
      break;
    }
    case 2: {
      IfFailRet(callTargetTokens->WriteBeginMethodWithArguments(
          &reWriterWrapper, wrapper_type_ref, &caller->type,
          &methodArguments[0], &methodArguments[1],
          &beginCallInstruction));
      break;
    }
    case 3: {
      IfFailRet(callTargetTokens->WriteBeginMethodWithArguments(
          &reWriterWrapper, wrapper_type_ref, &caller->type,
          &methodArguments[0], &methodArguments[1], &methodArguments[2],
          &beginCallInstruction));
      break;
    }
    case 4: {
      IfFailRet(callTargetTokens->WriteBeginMethodWithArguments(
          &reWriterWrapper, wrapper_type_ref, &caller->type,
          &methodArguments[0], &methodArguments[1], &methodArguments[2],
          &methodArguments[3], &beginCallInstruction));
      break;
    }
    case 5: {
      IfFailRet(callTargetTokens->WriteBeginMethodWithArguments(
          &reWriterWrapper, wrapper_type_ref, &caller->type,
          &methodArguments[0], &methodArguments[1], &methodArguments[2],
          &methodArguments[3], &methodArguments[4], &beginCallInstruction));
      break;
    }
    case 6: {
      IfFailRet(callTargetTokens->WriteBeginMethodWithArguments(
          &reWriterWrapper, wrapper_type_ref, &caller->type,
          &methodArguments[0], &methodArguments[1], &methodArguments[2],
          &methodArguments[3], &methodArguments[4], &methodArguments[5],
          &beginCallInstruction));
      break;
    }
    default: {
      IfFailRet(callTargetTokens->WriteBeginMethodWithArgumentsArray(
          &reWriterWrapper, wrapper_type_ref, &caller->type,
          &beginCallInstruction));
      break;
    }
  }
  reWriterWrapper.StLocal(callTargetStateIndex);
  ILInstr* pStateLeaveToBeginOriginalMethodInstr = reWriterWrapper.CreateInstr(CEE_LEAVE_S);

  // *** BeginMethod call catch
  ILInstr* beginMethodCatchFirstInstr = nullptr;
  callTargetTokens->WriteLogException(&reWriterWrapper, wrapper_type_ref,
                                      &caller->type,
                                      &beginMethodCatchFirstInstr);
  ILInstr* beginMethodCatchLeaveInstr = reWriterWrapper.CreateInstr(CEE_LEAVE_S);

  // *** BeginMethod exception handling clause
  EHClause beginMethodExClause{};
  beginMethodExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
  beginMethodExClause.m_pTryBegin = firstInstruction;
  beginMethodExClause.m_pTryEnd = beginMethodCatchFirstInstr;
  beginMethodExClause.m_pHandlerBegin = beginMethodCatchFirstInstr;
  beginMethodExClause.m_pHandlerEnd = beginMethodCatchLeaveInstr;
  beginMethodExClause.m_ClassToken = callTargetTokens->GetExceptionTypeRef();

  // ***
  // METHOD EXECUTION
  // ***
  ILInstr* beginOriginalMethodInstr = reWriterWrapper.GetCurrentILInstr();
  pStateLeaveToBeginOriginalMethodInstr->m_pTarget = beginOriginalMethodInstr;
  beginMethodCatchLeaveInstr->m_pTarget = beginOriginalMethodInstr;

  // ***
  // ENDING OF THE METHOD EXECUTION
  // ***

  // *** Create return instruction and insert it at the end
  ILInstr* methodReturnInstr = rewriter.NewILInstr();
  methodReturnInstr->m_opcode = CEE_RET;
  rewriter.InsertAfter(rewriter.GetILList()->m_pPrev, methodReturnInstr);
  reWriterWrapper.SetILPosition(methodReturnInstr);
  
  // ***
  // EXCEPTION CATCH
  // ***
  ILInstr* startExceptionCatch = reWriterWrapper.StLocal(exceptionIndex);
  reWriterWrapper.SetILPosition(methodReturnInstr);
  reWriterWrapper.Rethrow();
  ILInstr* methodCatchLeaveInstr = reWriterWrapper.CreateInstr(CEE_LEAVE_S);

  // ***
  // EXCEPTION FINALLY / END METHOD PART
  // ***
  ILInstr* endMethodTryStartInstr;

  // *** Load instance into the stack (if not static)
  if (isStatic) {
    if (caller->type.valueType) {
      // Static methods in a ValueType can't be instrumented.
      // In the future this can be supported by adding a local for the valuetype
      // and initialize it to the default value. After the signature
      // modification we need to emit the following IL to initialize and load
      // into the stack.
      //    ldloca.s [localIndex]
      //    initobj [valueType]
      //    ldloc.s [localIndex]
      Warn(
          "CallTarget_RewriterCallback: Static methods in a ValueType cannot "
          "be instrumented. ");
      return E_FAIL;
    }
    endMethodTryStartInstr = reWriterWrapper.LoadNull();
  } else {
    endMethodTryStartInstr = reWriterWrapper.LoadArgument(0);
    if (caller->type.valueType) {
      if (caller->type.type_spec != mdTypeSpecNil) {
        reWriterWrapper.LoadObj(caller->type.type_spec);
      } else if (!caller->type.isGeneric) {
        reWriterWrapper.LoadObj(caller->type.id);
      } else {
        // Generic struct instrumentation is not supported
        // IMetaDataImport::GetMemberProps and IMetaDataImport::GetMemberRefProps returns 
        // The parent token as mdTypeDef and not as a mdTypeSpec
        // that's because the method definition is stored in the mdTypeDef
        // The problem is that we don't have the exact Spec of that generic
        // We can't emit LoadObj or Box because that would result in an invalid IL.
        // This problem doesn't occur on a class type because we can always relay in the
        // object type.
        return E_FAIL;
      }
    }
  }

  // *** Load the return value is is not void
  if (!isVoid) {
    reWriterWrapper.LoadLocal(returnValueIndex);
  }

  reWriterWrapper.LoadLocal(exceptionIndex);
  reWriterWrapper.LoadLocal(callTargetStateIndex);
  
  ILInstr* endMethodCallInstr;
  if (isVoid) {
    callTargetTokens->WriteEndVoidReturnMemberRef(
        &reWriterWrapper, wrapper_type_ref, &caller->type, &endMethodCallInstr);
  } else {
    callTargetTokens->WriteEndReturnMemberRef(&reWriterWrapper,
                                              wrapper_type_ref, &caller->type,
                                              &retFuncArg, &endMethodCallInstr);
  }
  reWriterWrapper.StLocal(callTargetReturnIndex);

  if (!isVoid) {
    ILInstr* callTargetReturnGetReturnInstr;
    reWriterWrapper.LoadLocalAddress(callTargetReturnIndex);
    callTargetTokens->WriteCallTargetReturnGetReturnValue(&reWriterWrapper, callTargetReturnToken, &callTargetReturnGetReturnInstr);
    reWriterWrapper.StLocal(returnValueIndex);
  }

  ILInstr* endMethodTryLeave = reWriterWrapper.CreateInstr(CEE_LEAVE_S);

  // *** EndMethod call catch
  ILInstr* endMethodCatchFirstInstr = nullptr;
  callTargetTokens->WriteLogException(&reWriterWrapper, wrapper_type_ref,
                                      &caller->type, &endMethodCatchFirstInstr);
  ILInstr* endMethodCatchLeaveInstr = reWriterWrapper.CreateInstr(CEE_LEAVE_S);

  // *** EndMethod exception handling clause
  EHClause endMethodExClause{};
  endMethodExClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
  endMethodExClause.m_pTryBegin = endMethodTryStartInstr;
  endMethodExClause.m_pTryEnd = endMethodCatchFirstInstr;
  endMethodExClause.m_pHandlerBegin = endMethodCatchFirstInstr;
  endMethodExClause.m_pHandlerEnd = endMethodCatchLeaveInstr;
  endMethodExClause.m_ClassToken = callTargetTokens->GetExceptionTypeRef();

  // *** EndMethod leave to finally
  ILInstr* endFinallyInstr = reWriterWrapper.EndFinally();
  endMethodTryLeave->m_pTarget = endFinallyInstr;
  endMethodCatchLeaveInstr->m_pTarget = endFinallyInstr;

  // ***
  // METHOD RETURN
  // ***

  // Load the current return value from the local var
  if (!isVoid) {
    reWriterWrapper.LoadLocal(returnValueIndex);
  }

  // Resolving branching to the end of the method
  methodCatchLeaveInstr->m_pTarget = endFinallyInstr->m_pNext;
  
  // Changes all returns to a LEAVE.S
  for (ILInstr* pInstr = rewriter.GetILList()->m_pNext;
       pInstr != rewriter.GetILList(); pInstr = pInstr->m_pNext) {
    switch (pInstr->m_opcode) {
      case CEE_RET: {
        if (pInstr != methodReturnInstr) {
          if (!isVoid) {
            reWriterWrapper.SetILPosition(pInstr);
            reWriterWrapper.StLocal(returnValueIndex);
          }
          pInstr->m_opcode = CEE_LEAVE_S;
          pInstr->m_pTarget = endFinallyInstr->m_pNext;
        }
        break;
      }
      default:
        break;
    }
  }

  // Exception handling clauses
  EHClause exClause{};
  exClause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
  exClause.m_pTryBegin = firstInstruction;
  exClause.m_pTryEnd = startExceptionCatch;
  exClause.m_pHandlerBegin = startExceptionCatch;
  exClause.m_pHandlerEnd = methodCatchLeaveInstr;
  exClause.m_ClassToken = callTargetTokens->GetExceptionTypeRef();

  EHClause finallyClause{};
  finallyClause.m_Flags = COR_ILEXCEPTION_CLAUSE_FINALLY;
  finallyClause.m_pTryBegin = firstInstruction;
  finallyClause.m_pTryEnd = methodCatchLeaveInstr->m_pNext;
  finallyClause.m_pHandlerBegin = methodCatchLeaveInstr->m_pNext;
  finallyClause.m_pHandlerEnd = endFinallyInstr;

  // ***
  // Update and Add exception clauses
  // ***
  auto ehCount = rewriter.GetEHCount();
  auto newEHClauses = new EHClause[ehCount + 4];
  for (unsigned i = 0; i < ehCount; i++) {
    newEHClauses[i] = rewriter.GetEHPointer()[i];
  }

  // *** Add the new EH clauses
  ehCount += 4;
  newEHClauses[ehCount - 4] = beginMethodExClause;
  newEHClauses[ehCount - 3] = endMethodExClause;
  newEHClauses[ehCount - 2] = exClause;
  newEHClauses[ehCount - 1] = finallyClause;
  rewriter.SetEHClause(newEHClauses, ehCount);
  
  if (dump_il_rewrite_enabled) {
    Info(original_code);
    Info(GetILCodes("*** CallTarget_RewriterCallback(): Modified Code: ",
                    &rewriter, *caller, module_metadata));
  }

  hr = rewriter.Export();

  if (FAILED(hr)) {
    Warn(
        "*** CallTarget_RewriterCallback(): Call to ILRewriter.Export() failed for "
        "ModuleID=",
        module_id, " ", function_token);
    return hr;
  }

  Info("*** CallTarget_RewriterCallback() Finished: ", caller->type.name, ".",
        caller->name, "() [IsVoid=", isVoid, ", IsStatic=", isStatic,
        ", IntegrationType=", method_replacement->wrapper_method.type_name,
        ", Arguments=", numArgs, "]");
  return S_OK;
}

}  // namespace trace
