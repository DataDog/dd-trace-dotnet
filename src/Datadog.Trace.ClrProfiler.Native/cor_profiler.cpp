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

  // load all available integrations from JSON files
  const std::vector<Integration> all_integrations =
      LoadIntegrationsFromEnvironment();

  // get list of disabled integration names
  const std::vector<WSTRING> disabled_integration_names =
      GetEnvironmentValues(environment::disabled_integrations);

  // remove disabled integrations
  const std::vector<Integration> integrations =
      FilterIntegrationsByName(all_integrations, disabled_integration_names);

  // check if there are any enabled integrations left
  if (integrations.empty()) {
    Warn("DATADOG TRACER DIAGNOSTICS - Profiler disabled: no enabled integrations found.");
    return E_FAIL;
  }

  integration_methods_ = FlattenIntegrations(integrations);

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
                     COR_PRF_DISABLE_INLINING | COR_PRF_MONITOR_MODULE_LOADS |
                     COR_PRF_MONITOR_ASSEMBLY_LOADS |
                     COR_PRF_DISABLE_ALL_NGEN_IMAGES;

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

  // Create the loader class
  loader_ = new Loader(this->info_);

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
  is_attached_ = true;
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

  const auto module_info = GetModuleInfo(this->info_, module_id);
  if (!module_info.IsValid()) {
    return S_OK;
  }

  if (debug_logging_enabled) {
    Debug("ModuleLoadFinished: ", module_id, " ", module_info.assembly.name,
          " AppDomain ", module_info.assembly.app_domain_id, " ",
          module_info.assembly.app_domain_name);
  }

  // Inject loader to the module initializer
  loader->InjectLoaderToModuleInitializer(module_id);

  AppDomainID app_domain_id = module_info.assembly.app_domain_id;

  // Identify the AppDomain ID of mscorlib which will be the Shared Domain
  // because mscorlib is always a domain-neutral assembly
  if (!corlib_module_loaded &&
      (module_info.assembly.name == "mscorlib"_W ||
       module_info.assembly.name == "System.Private.CoreLib"_W)) {
    corlib_module_loaded = true;
    corlib_app_domain_id = app_domain_id;
    return S_OK;
  }

  // In IIS, the startup hook will be inserted into a method in System.Web (which is domain-neutral)
  // but the Datadog.Trace.ClrProfiler.Managed.Loader assembly that the startup hook loads from a
  // byte array will be loaded into a non-shared AppDomain.
  // In this case, do not insert another startup hook into that non-shared AppDomain
  if (module_info.assembly.name == "Datadog.Trace.ClrProfiler.Managed.Loader"_W) {
    Info("ModuleLoadFinished: Datadog.Trace.ClrProfiler.Managed.Loader loaded into AppDomain ",
          app_domain_id, " ", module_info.assembly.app_domain_name);
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
      module_version_id, filtered_integrations);

  // store module info for later lookup
  module_id_to_info_map_[module_id] = module_metadata;

  Debug("ModuleLoadFinished stored metadata for ", module_id, " ",
        module_info.assembly.name, " AppDomain ",
        module_info.assembly.app_domain_id, " ",
        module_info.assembly.app_domain_name);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadStarted(ModuleID module_id) {
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

  is_attached_ = false;
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ProfilerDetachSucceeded() {
  Warn("Detaching profiler.");
  Logger::Shutdown();
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
    const trace::FunctionInfo& caller,
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
      auto generated_wrapper_method_ref = GetWrapperMethodRef(module_metadata,
                                                              module_id,
                                                              method_replacement,
                                                              wrapper_method_ref);
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

      // IL Modification #2: Conditionally box System.Threading.CancellationToken
      //                     if it is the last argument in the target method.
      //
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
      // Currently, all integrations that use System.Threading.CancellationToken (a struct)
      // have the argument as the last argument in the signature (lucky us!).
      // For now, we'll do the following:
      //   1) Get the method signature of the original target method
      //   2) Read the signature until the final argument type
      //   3) If the type begins with `ELEMENT_TYPE_VALUETYPE`, uncompress the compressed type token that follows
      //   4) If the type token represents System.Threading.CancellationToken, emit a 'box <type_token>' IL instruction before calling our wrapper method
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
    auto generated_wrapper_method_ref = GetWrapperMethodRef(module_metadata,
                                                            module_id,
                                                            method_replacement,
                                                            wrapper_method_ref);
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
    mdMemberRef& wrapper_method_ref) {
  const auto& wrapper_method_key =
      method_replacement.wrapper_method.get_method_cache_key();

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

  return true;
}

bool CorProfiler::ProfilerAssemblyIsLoadedIntoAppDomain(AppDomainID app_domain_id) {
  return managed_profiler_loaded_domain_neutral ||
         managed_profiler_loaded_app_domains.find(app_domain_id) !=
             managed_profiler_loaded_app_domains.end();
}

std::string CorProfiler::GetILCodes(std::string title, ILRewriter* rewriter,
                                    const FunctionInfo& caller, ModuleMetadata* module_metadata) {
  std::stringstream orig_sstream;
  orig_sstream << title;
  orig_sstream << ToString(caller.type.name);
  orig_sstream << ".";
  orig_sstream << ToString(caller.name.c_str());
  orig_sstream << " => (max_stack: ";
  orig_sstream << rewriter->GetMaxStackValue();
  orig_sstream << ")" << std::endl;
  for (ILInstr* cInstr = rewriter->GetILList()->m_pNext;
       cInstr != rewriter->GetILList(); cInstr = cInstr->m_pNext) {
    
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
          orig_sstream << "  | ";
          orig_sstream << "\"";
          orig_sstream << ToString(WSTRING(szString).substr(0, szStringLength));
          orig_sstream << "\"";
        }
      }
    } else if (cInstr->m_Arg64 != 0) {
      orig_sstream << " ";
      orig_sstream << cInstr->m_Arg64;
    }
    orig_sstream << std::endl;
  }
  return orig_sstream.str();
}

}  // namespace trace
