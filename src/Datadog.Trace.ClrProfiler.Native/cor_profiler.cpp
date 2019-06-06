#include "cor_profiler.h"

#include <corprof.h>
#include <string>
#include "corhlpr.h"

#include "clr_helpers.h"
#include "environment_variables.h"
#include "il_rewriter.h"
#include "il_rewriter_wrapper.h"
#include "integration_loader.h"
#include "logging.h"
#include "metadata_builder.h"
#include "module_metadata.h"
#include "pal.h"
#include "util.h"

namespace trace {

CorProfiler* profiler = nullptr;

CorProfiler::CorProfiler() { Debug("CorProfiler::CorProfiler"); }

HRESULT STDMETHODCALLTYPE
CorProfiler::Initialize(IUnknown* cor_profiler_info_unknown) {
  // check if debug mode is enabled
  const auto debug_enabled_value =
      GetEnvironmentValue(environment::debug_enabled);

  if (debug_enabled_value == "1"_W || debug_enabled_value == "true"_W) {
    debug_logging_enabled = true;
    Debug("Debug mode enabled in ", environment::debug_enabled);
  }

  Debug("CorProfiler::Initialize");

  // check if tracing is completely disabled
  const WSTRING tracing_enabled =
      GetEnvironmentValue(environment::tracing_enabled);

  if (tracing_enabled == "0"_W || tracing_enabled == "false"_W) {
    Info("Profiler disabled in ", environment::tracing_enabled);
    return E_FAIL;
  }

  // check if there is a whitelist of process names
  const auto allowed_process_names =
      GetEnvironmentValues(environment::process_names);

  if (!allowed_process_names.empty()) {
    const auto process_name = GetCurrentProcessName();

    if (std::find(allowed_process_names.begin(), allowed_process_names.end(),
                  process_name) == allowed_process_names.end()) {
      Info("Profiler disabled: ", process_name, " not found in ",
           environment::process_names, ".");
      return E_FAIL;
    }
  }

  // get Profiler interface
  HRESULT hr = cor_profiler_info_unknown->QueryInterface<ICorProfilerInfo3>(
      &this->info_);
  if (FAILED(hr)) {
    Warn("Failed to attach profiler: interface ICorProfilerInfo3 not found.");
    return E_FAIL;
  }

  Info("Environment variables:");

  WSTRING env_vars[]{environment::tracing_enabled,
                     environment::debug_enabled,
                     environment::integrations_path,
                     environment::process_names,
                     environment::agent_host,
                     environment::agent_port,
                     environment::env,
                     environment::service_name,
                     environment::disabled_integrations,
                     environment::clr_disable_optimizations};

  for (auto&& env_var : env_vars) {
    Info("  ", env_var, "=", GetEnvironmentValue(env_var));
  }

  // get path to integration definition JSON files
  const WSTRING integrations_paths =
      GetEnvironmentValue(environment::integrations_path);

  if (integrations_paths.empty()) {
    Warn("Profiler disabled: ", environment::integrations_path,
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
  integrations_ =
      FilterIntegrationsByName(all_integrations, disabled_integration_names);

  // check if there are any enabled integrations left
  if (integrations_.empty()) {
    Warn("Profiler disabled: no enabled integrations found.");
    return E_FAIL;
  }

  DWORD event_mask = COR_PRF_MONITOR_JIT_COMPILATION |
                     COR_PRF_DISABLE_TRANSPARENCY_CHECKS_UNDER_FULL_TRUST |
                     COR_PRF_DISABLE_INLINING | COR_PRF_MONITOR_MODULE_LOADS |
                     COR_PRF_DISABLE_ALL_NGEN_IMAGES;

  if (DisableOptimizations()) {
    Info("Disabling all code optimizations.");
    event_mask |= COR_PRF_DISABLE_OPTIMIZATIONS;
  }

  // set event mask to subscribe to events and disable NGEN images
  hr = this->info_->SetEventMask(event_mask);
  if (FAILED(hr)) {
    Warn("Failed to attach profiler: unable to set event mask.");
    return E_FAIL;
  }

  // we're in!
  Info("Profiler attached.");
  this->info_->AddRef();
  is_attached_ = true;
  profiler = this;
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleLoadFinished(ModuleID module_id,
                                                          HRESULT hrStatus) {
  if (FAILED(hrStatus)) {
    // if module failed to load, skip it entirely,
    // otherwise we can crash the process if module is not valid
    return S_OK;
  }

  const auto module_info = GetModuleInfo(this->info_, module_id);
  if (!module_info.IsValid()) {
    return S_OK;
  }

  if (module_info.IsWindowsRuntime() ||
      module_info.assembly.name == "mscorlib"_W ||
      module_info.assembly.name == "netstandard"_W ||
      module_info.assembly.name == "Datadog.Trace"_W ||
      module_info.assembly.name == "Datadog.Trace.ClrProfiler.Managed"_W ||
      module_info.assembly.name == "Sigil.Emit.DynamicAssembly"_W ||
      module_info.assembly.name ==
          "Anonymously Hosted DynamicMethods Assembly"_W) {
    // We cannot obtain writable metadata interfaces on Windows Runtime modules
    // or instrument their IL. We must never try to add assembly references to
    // mscorlib or netstandard.
    Debug("CorProfiler::ModuleLoadFinished: ", module_info.assembly.name,
          ". Skipping (known module).");
    return S_OK;
  }

  std::vector<IntegrationMethod> filtered_integrations =
      FlattenIntegrations(integrations_);

  filtered_integrations =
      FilterIntegrationsByCaller(filtered_integrations, module_info.assembly);
  if (filtered_integrations.empty()) {
    // we don't need to instrument anything in this module, skip it
    Debug("CorProfiler::ModuleLoadFinished: ", module_info.assembly.name,
          ". Skipping (filtered by caller).");
    return S_OK;
  }

  ComPtr<IUnknown> metadata_interfaces;

  auto hr = this->info_->GetModuleMetaData(module_id, ofRead | ofWrite,
                                           IID_IMetaDataImport2,
                                           metadata_interfaces.GetAddressOf());

  if (FAILED(hr)) {
    Warn("CorProfiler::ModuleLoadFinished: Failed to get metadata interface");
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

  filtered_integrations =
      FilterIntegrationsByTarget(filtered_integrations, assembly_import);
  if (filtered_integrations.empty()) {
    // we don't need to instrument anything in this module, skip it
    Debug("CorProfiler::ModuleLoadFinished: ", module_info.assembly.name,
          ". Skipping (filtered by target).");
    return S_OK;
  }

  mdModule module;
  hr = metadata_import->GetModuleFromScope(&module);
  if (FAILED(hr)) {
    Warn("CorProfiler::ModuleLoadFinished: ", module_info.assembly.name,
         ". Failed to get module token.");
    return S_OK;
  }

  ModuleMetadata* module_metadata =
      new ModuleMetadata(metadata_import, metadata_emit,
                         module_info.assembly.name, filtered_integrations);

  MetadataBuilder metadata_builder(*module_metadata, module, metadata_import,
                                   metadata_emit, assembly_import,
                                   assembly_emit);

  for (const auto& integration : filtered_integrations) {
    // for each wrapper assembly, emit an assembly reference
    hr = metadata_builder.EmitAssemblyRef(
        integration.replacement.wrapper_method.assembly);
    if (FAILED(hr)) {
      Warn("CorProfiler::ModuleLoadFinished: ", module_info.assembly.name,
           ". Failed to emit wrapper assembly ref.");
      return S_OK;
    }

    // for each method replacement in each enabled integration,
    // emit a reference to the instrumentation wrapper methods
    hr = metadata_builder.StoreWrapperMethodRef(integration.replacement);
    if (FAILED(hr)) {
      Warn("CorProfiler::ModuleLoadFinished: ", module_info.assembly.name,
           ". Failed to emit and store wrapper method ref.");
      return S_OK;
    }
  }

  // store module info for later lookup
  {
    std::lock_guard<std::mutex> guard(module_id_to_info_map_lock_);
    module_id_to_info_map_[module_id] = module_metadata;
  }

  Debug("CorProfiler::ModuleLoadFinished: ", module_info.assembly.name,
        ". Emitted instrumentation metadata.");
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadFinished(ModuleID module_id,
                                                            HRESULT hrStatus) {
  Debug("CorProfiler::ModuleUnloadFinished: ", uint64_t(module_id));
  {
    std::lock_guard<std::mutex> guard(module_id_to_info_map_lock_);
    if (module_id_to_info_map_.count(module_id) > 0) {
      const auto metadata = module_id_to_info_map_[module_id];
      delete metadata;
      module_id_to_info_map_.erase(module_id);
    }
  }

  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::JITCompilationStarted(
    FunctionID function_id, BOOL is_safe_to_block) {
  ClassID class_id;
  ModuleID module_id;
  mdToken function_token = mdTokenNil;

  HRESULT hr = this->info_->GetFunctionInfo(function_id, &class_id, &module_id,
                                            &function_token);
  RETURN_OK_IF_FAILED(hr);

  ModuleMetadata* module_metadata = nullptr;
  {
    std::lock_guard<std::mutex> guard(module_id_to_info_map_lock_);
    if (module_id_to_info_map_.count(module_id) > 0) {
      module_metadata = module_id_to_info_map_[module_id];
    }
  }

  if (module_metadata == nullptr) {
    // we haven't stored a ModuleInfo for this module, so we can't modify its
    // IL
    return S_OK;
  }

  // get function info
  auto caller =
      GetFunctionInfo(module_metadata->metadata_import, function_token);
  if (!caller.IsValid()) {
    return S_OK;
  }

  auto method_replacements =
      module_metadata->GetMethodReplacementsForCaller(caller);
  if (method_replacements.empty()) {
    return S_OK;
  }

  ILRewriter rewriter(this->info_, nullptr, module_id, function_token);
  bool modified = false;

  // hr = rewriter.Initialize();
  hr = rewriter.Import();
  RETURN_OK_IF_FAILED(hr);

  for (auto& method_replacement : method_replacements) {
    const auto& wrapper_method_key =
        method_replacement.wrapper_method.get_method_cache_key();
    mdMemberRef wrapper_method_ref = mdMemberRefNil;

    if (!module_metadata->TryGetWrapperMemberRef(wrapper_method_key,
                                                 wrapper_method_ref)) {
      // no method ref token found for wrapper method, we can't do the
      // replacement, this should never happen because we always try to
      // add the method ref in ModuleLoadFinished()
      // TODO: log this
      return S_OK;
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

      if (method_replacement.wrapper_method.method_signature.data.size() < 3) {
        // This is invalid, we should always have the wrapper fully defined
        // Minimum: 0:{CallingConvention}|1:{ParamCount}|2:{ReturnType}
        // Drop out for safety
        continue;
      }

      auto expected_number_args = method_replacement.wrapper_method
                                      .method_signature.NumberOfArguments();

      // We pass the op-code as the last argument to every wrapper method
      expected_number_args--;

      if (target.signature.IsInstanceMethod()) {
        // We always pass the instance as the first argument
        expected_number_args--;
      }

      auto target_arg_count = target.signature.NumberOfArguments();

      if (expected_number_args != target_arg_count) {
        // Number of arguments does not match our wrapper method
        continue;
      }

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
      }

      const auto original_argument = pInstr->m_Arg32;

      // insert the opcode and signature token as
      // additional arguments for the wrapper method
      ILRewriterWrapper rewriter_wrapper(&rewriter);
      rewriter_wrapper.SetILPosition(pInstr);
      rewriter_wrapper.LoadInt32(pInstr->m_opcode);

      // always use CALL because the wrappers methods are all static
      pInstr->m_opcode = CEE_CALL;
      // replace with a call to the instrumentation wrapper
      pInstr->m_Arg32 = wrapper_method_ref;

      modified = true;

      Info("CorProfiler::JITCompilationStarted() replaced calls from ",
           caller.type.name, ".", caller.name, "() to ",
           method_replacement.target_method.type_name, ".",
           method_replacement.target_method.method_name, "() ",
           int32_t(original_argument), " with calls to ",
           method_replacement.wrapper_method.type_name, ".",
           method_replacement.wrapper_method.method_name, "() ",
           uint32_t(wrapper_method_ref));
    }
  }

  if (modified) {
    hr = rewriter.Export();
    RETURN_OK_IF_FAILED(hr);
  }

  return S_OK;
}

bool CorProfiler::IsAttached() const { return is_attached_; }

}  // namespace trace
