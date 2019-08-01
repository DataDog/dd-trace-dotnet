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

HRESULT STDMETHODCALLTYPE
CorProfiler::Initialize(IUnknown* cor_profiler_info_unknown) {
  // check if debug mode is enabled
  const auto debug_enabled_value =
      GetEnvironmentValue(environment::debug_enabled);

  if (debug_enabled_value == "1"_W || debug_enabled_value == "true"_W) {
    debug_logging_enabled = true;
  }

  CorProfilerBase::Initialize(cor_profiler_info_unknown);

  // check if tracing is completely disabled
  const WSTRING tracing_enabled =
      GetEnvironmentValue(environment::tracing_enabled);

  if (tracing_enabled == "0"_W || tracing_enabled == "false"_W) {
    Info("Profiler disabled in ", environment::tracing_enabled);
    return E_FAIL;
  }

  const auto process_name = GetCurrentProcessName();
  const auto include_process_names =
      GetEnvironmentValues(environment::include_process_names);

  // if there is a process inclusion list, attach profiler only if this
  // process's name is on the list
  if (!include_process_names.empty() &&
      !Contains(include_process_names, process_name)) {
    Info("Profiler disabled: ", process_name, " not found in ",
         environment::include_process_names, ".");
    return E_FAIL;
  }

  const auto exclude_process_names =
      GetEnvironmentValues(environment::exclude_process_names);

  // attach profiler only if this process's name is NOT on the list
  if (Contains(exclude_process_names, process_name)) {
    Info("Profiler disabled: ", process_name, " found in ",
         environment::exclude_process_names, ".");
    return E_FAIL;
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
                     environment::include_process_names,
                     environment::exclude_process_names,
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

FunctionInfo FindMethodDefinition(
    const ComPtr<IMetaDataImport2>& assembly_import, WSTRING type_name,
    WSTRING method_name, std::vector<WSTRING> signature_types) {
  auto type_def_tokens = EnumTypeDefs(assembly_import);

  for (auto token : type_def_tokens) {
    auto type_info = GetTypeInfo(assembly_import, token);

    if (type_info.name != type_name) {
      continue;
    }

    auto method_tokens = EnumMethods(assembly_import, token);

    for (auto method_token : method_tokens) {
      auto method_info = GetFunctionInfo(assembly_import, method_token);

      auto sig_type_count = signature_types.size();

      if (method_info.signature.NumberOfArguments() != sig_type_count - 1) {
        continue;
      }

      if (method_info.name != method_name) {
        continue;
      }

      std::vector<WSTRING> sig_types;
      const auto parsed_load_assembly_candidate =
          TryParseSignatureTypes(assembly_import, method_info, sig_types);

      if (!parsed_load_assembly_candidate) {
        continue;
      }

      auto full_match = true;

      for (auto i = sig_type_count - 1; i > 0; i--) {
        if (sig_types[i] != signature_types[i]) {
          full_match = false;
          break;
        }
      }

      if (!full_match) {
        continue;
      }

      return method_info;
    }
  }

  return {};
}

HRESULT RegisterMethodReferenceIfMissing(
    ModuleMetadata* module_metadata, mdAssemblyRef source_assembly_ref_token,
    const FunctionInfo& method_to_register, mdTypeRef& type_ref_token,
    mdMemberRef& method_member_ref_token) {
  const auto method_name_c_str = method_to_register.name.c_str();
  const auto type_name_c_str = method_to_register.type.name.c_str();

  type_ref_token = mdTypeRefNil;
  HRESULT hr = module_metadata->metadata_import->FindTypeRef(
      source_assembly_ref_token, type_name_c_str, &type_ref_token);

  const auto record_not_found_hr =
      HRESULT(0x80131130); /* record not found on lookup */

  if (hr == record_not_found_hr) {
    hr = module_metadata->metadata_emit->DefineTypeRefByName(
        source_assembly_ref_token, type_name_c_str, &type_ref_token);
  }

  if (FAILED(hr)) {
    Warn("[RegisterMethodReferenceIfMissing] Failed to define TypeRef for ",
         method_to_register.type.name);
    return S_FALSE;
  }

  method_member_ref_token = mdMemberRefNil;

  hr = module_metadata->metadata_import->FindMemberRef(
      type_ref_token, method_name_c_str,
      method_to_register.signature.original_signature,
      (DWORD)(method_to_register.signature.data.size()),
      &method_member_ref_token);

  if (hr == record_not_found_hr) {
    hr = module_metadata->metadata_emit->DefineMemberRef(
        type_ref_token, method_name_c_str,
        method_to_register.signature.original_signature,
        (DWORD)(method_to_register.signature.data.size()),
        &method_member_ref_token);
  }

  if (FAILED(hr)) {
    Warn("ModuleLoadFinished failed to define MemberRef for ",
         method_to_register.name, " for ", method_to_register.type.name);
    return S_FALSE;
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

  if (module_info.IsWindowsRuntime()) {
    // We cannot obtain writable metadata interfaces on Windows Runtime modules
    // or instrument their IL.
    Debug("[ModuleLoadFinished] skipping Windows Metadata module: ", module_id,
          " ", module_info.assembly.name);
    return S_OK;
  }

  auto is_dot_net_assembly = false;
  if (!dot_net_assembly_is_loaded) {
    Info("[ModuleLoadFinished] .NET assembly has been loaded - ", module_id,
         " ", module_info.assembly.name);
    is_dot_net_assembly = true;
    dot_net_assembly_is_loaded = true;
  }

  auto is_entry_assembly = false;
  if (!is_dot_net_assembly && !entry_assembly_is_loaded) {
    Info("[ModuleLoadFinished] Entry assembly has been loaded - ", module_id,
         " ", module_info.assembly.name);
    entry_assembly_is_loaded = true;
    is_entry_assembly = true;
  }

  if (module_info.assembly.name == datadog_managed_assembly_short_name_) {
    Info(
        "[ModuleLoadFinished] Required managed assembly has finished loading: ",
        datadog_managed_assembly_short_name_);
    managed_assembly_is_loaded_ = true;
    return S_OK;
  }

  if (!managed_assembly_is_loaded_ && !is_entry_assembly &&
      !is_dot_net_assembly) {
    Info("[ModuleLoadFinished] Requirements not pre-loaded - skipping: ",
         module_id, " ", module_info.assembly.name);
    return S_OK;
  }

  // We must never try to add assembly references to
  // mscorlib or netstandard. Skip other known assemblies.
  WSTRING skip_assemblies[]{
      "Datadog.Trace"_W,
      "MsgPack"_W,
      "MsgPack.Serialization.EmittingSerializers.GeneratedSerealizers0"_W,
      "MsgPack.Serialization.EmittingSerializers.GeneratedSerealizers1"_W,
      "MsgPack.Serialization.EmittingSerializers.GeneratedSerealizers2"_W,
      "Sigil"_W,
      "Sigil.Emit.DynamicAssembly"_W,
      "System.Core"_W,
      "System.Runtime"_W,
      "System.IO.FileSystem"_W,
      "System.Collections"_W,
      "System.EnterpriseServices"_W,
      "System.Runtime.Extensions"_W,
      "System.Threading.Tasks"_W,
      "System.Runtime.InteropServices"_W,
      "System.Runtime.InteropServices.RuntimeInformation"_W,
      "System.ComponentModel"_W,
      "System.ComponentModel.DataAnnotations"_W,
      "System.Console"_W,
      "System.Diagnostics.DiagnosticSource"_W,
      "Microsoft.Extensions.Options"_W,
      "Microsoft.Extensions.ObjectPool"_W,
      "System.Configuration"_W,
      "System.Xml"_W,
      "System.Xml.Linq"_W,
      "System.ValueTuple"_W,
      "Microsoft.AspNetCore.Razor.Language"_W,
      "Microsoft.AspNetCore.Mvc.RazorPages"_W,
      "Microsoft.CSharp"_W,
      "Newtonsoft.Json"_W,
      "Remotion.Linq"_W,
      "Anonymously Hosted DynamicMethods Assembly"_W,
      "ISymWrapper"_W};

  for (auto&& skip_assembly : skip_assemblies) {
    if (module_info.assembly.name == skip_assembly) {
      Debug("[ModuleLoadFinished] skipping known module: ", module_id, " ",
            module_info.assembly.name);
      return S_OK;
    }
  }

  std::vector<IntegrationMethod> filtered_integrations =
      FlattenIntegrations(integrations_);

  filtered_integrations =
      FilterIntegrationsByCaller(filtered_integrations, module_info.assembly);
  if (filtered_integrations.empty()) {
    // we don't need to instrument anything in this module, skip it
    Debug("[ModuleLoadFinished] skipping module (filtered by caller): ",
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

  mdModule module;
  hr = metadata_import->GetModuleFromScope(&module);
  if (FAILED(hr)) {
    Warn("ModuleLoadFinished failed to get module metadata token for ",
         module_id, " ", module_info.assembly.name);
    return S_OK;
  }

  if (is_dot_net_assembly) {
    // Save the metadata and exit out
    dotnet_module_metadata_ =
        new ModuleMetadata(metadata_import, metadata_emit,
                           module_info.assembly.name, filtered_integrations);
    auto assembly_metadata = GetAssemblyImportMetadata(assembly_import);
    dotnet_assembly_strong_name_ = assembly_metadata.long_name();
    dotnet_assembly_short_name_ = module_info.assembly.name;
    dotnet_assembly_public_key_ = assembly_metadata.public_key;
    dotnet_assembly_version_ = assembly_metadata.version;
    return S_OK;
  }

  filtered_integrations =
      FilterIntegrationsByTarget(filtered_integrations, assembly_import);
  if (filtered_integrations.empty()) {
    // we don't need to instrument anything in this module, skip it
    Debug("[ModuleLoadFinished] skipping module (filtered by target): ",
          module_id, " ", module_info.assembly.name);
    return S_OK;
  }

  if (filtered_integrations.empty()) {
    // we don't need to instrument anything in this module, skip it
    Debug(
        "ModuleLoadFinished skipping module (filtered by available "
        "assemblies): ",
        module_id, " ", module_info.assembly.name);
    return S_OK;
  }

  ModuleMetadata* module_metadata =
      new ModuleMetadata(metadata_import, metadata_emit,
                         module_info.assembly.name, filtered_integrations);

  const MetadataBuilder metadata_builder(*module_metadata, module,
                                         metadata_import, metadata_emit,
                                         assembly_import, assembly_emit);

  if (is_entry_assembly && entry_load_assembly_member_ref_ == mdMemberRefNil) {
    mdAssemblyRef dotnet_assembly_ref_token =
        FindAssemblyRef(assembly_import, dotnet_assembly_short_name_);

    if (dotnet_assembly_ref_token == mdAssemblyRefNil) {
      // Does this ever even happen?
      hr = metadata_builder.EmitAssemblyRef(dotnet_assembly_long_name_,
                                            &dotnet_assembly_ref_token);
    }

    if (FAILED(hr)) {
      Warn("ModuleLoadFinished failed to emit assembly ref",
           dotnet_assembly_long_name_, " ref for ", module_id, " ",
           module_info.assembly.name);
      return S_OK;
    }

    const auto console_write_line_method_def = FindMethodDefinition(
        dotnet_module_metadata_->metadata_import, "System.Console"_W,
        "WriteLine"_W, {"System.Void"_W, "System.String"_W});

    const auto assembly_load_method_def = FindMethodDefinition(
        dotnet_module_metadata_->metadata_import,
        "System.Reflection.Assembly"_W, "Load"_W,
        {"System.Reflection.Assembly"_W, "System.String"_W});

    if (!assembly_load_method_def.IsValid()) {
      Warn(
          "ModuleLoadFinished failed to find required Assembly.Load method "
          "definition within ",
          dotnet_assembly_short_name_);
      return S_OK;
    }

    mdTypeRef console_type_token = mdTypeRefNil;
    mdMemberRef console_write_line_method_token = mdMemberRefNil;

    HRESULT register_hr = RegisterMethodReferenceIfMissing(
        module_metadata, dotnet_assembly_ref_token,
        console_write_line_method_def, console_type_token,
        console_write_line_method_token);

    mdTypeRef assembly_type_ref = mdTypeRefNil;
    mdMemberRef assembly_load_method_member_ref = mdMemberRefNil;

    // register_hr = RegisterMethodReferenceIfMissing(
    //     module_metadata, dotnet_assembly_ref_token, assembly_load_method_def,
    //     assembly_type_ref, assembly_load_method_member_ref);

    if (FAILED(register_hr)) {
      Warn(
          "[ModuleLoadFinished] failed to register required MemberRef for "
          "method ",
          assembly_load_method_def.type.name, ".",
          assembly_load_method_def.name);
      return S_OK;
    }

    // mdToken from c#: 100680220
    // mdToken from dotPeek: 167772186
    console_write_line_member_ref_ = console_write_line_method_token;
    entry_load_assembly_member_ref_ = assembly_load_method_member_ref;
    if (entry_load_assembly_member_ref_ != 167772186) {
      entry_load_assembly_member_ref_ = 167772186;
    }
    entry_load_assembly_type_ref_ = assembly_type_ref;
  }

  for (const auto& integration : filtered_integrations) {
    // for each wrapper assembly, emit an assembly reference
    mdAssemblyRef datadog_managed_assembly_ref_token;
    hr = metadata_builder.EmitAssemblyRef(
        integration.replacement.wrapper_method.assembly,
        &datadog_managed_assembly_ref_token);
    if (FAILED(hr)) {
      Warn("ModuleLoadFinished failed to emit wrapper assembly ref for ",
           module_id, " ", module_info.assembly.name);
      return S_OK;
    }

    // for each method replacement in each enabled integration,
    // emit a reference to the instrumentation wrapper methods
    hr = metadata_builder.StoreWrapperMethodRef(integration.replacement);
    if (FAILED(hr)) {
      Warn("ModuleLoadFinished failed to emit or store wrapper method ref for ",
           module_id, " ", module_info.assembly.name);
      return S_OK;
    }
  }

  // store module info for later lookup
  module_id_to_info_map_[module_id] = module_metadata;

  Debug("[ModuleLoadFinished] emitted new metadata into ", module_id, " ",
        module_info.assembly.name, " AppDomain ",
        module_info.assembly.app_domain_id, " ",
        module_info.assembly.app_domain_name, ". .");
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
  RETURN_OK_IF_FAILED(hr);

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
  auto caller =
      GetFunctionInfo(module_metadata->metadata_import, function_token);
  if (!caller.IsValid()) {
    return S_OK;
  }

  if (debug_logging_enabled) {
    Debug("JITCompilationStarted: function_id=", function_id,
          " token=", function_token, " name=", caller.type.name, ".",
          caller.name, "()");
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

  if (!attempted_pre_load_managed_assembly_) {
    if (entry_load_assembly_member_ref_ != mdMemberRefNil) {
      // Time to inject a call to this method wrapped in a try catch
      LPCWSTR lpcstr_assembly_name_arg =
          datadog_managed_assembly_long_name_.c_str();
      const auto assembly_name_length =
          datadog_managed_assembly_long_name_.size();

      mdString assembly_name_token;
      hr = module_metadata->metadata_emit->DefineUserString(
          lpcstr_assembly_name_arg, assembly_name_length, &assembly_name_token);

      if (FAILED(hr)) {
        Warn("Unable to define user string for managed assembly ",
             datadog_managed_assembly_long_name_);
        return S_OK;
      }

      auto starting = "[ICorProfiler] Starting forced assembly load."_W;
      mdString assembly_load_start_log;
      hr = module_metadata->metadata_emit->DefineUserString(
          starting.c_str(), starting.size(), &assembly_load_start_log);

      auto finished = "[ICorProfiler] Finished forced assembly load call."_W;
      mdString assembly_load_end_log;
      hr = module_metadata->metadata_emit->DefineUserString(
          finished.c_str(), finished.size(), &assembly_load_end_log);

      auto exception =
          "[ICorProfiler] Exception when loading managed assembly."_W;
      mdString assembly_load_exception_log;
      hr = module_metadata->metadata_emit->DefineUserString(
          exception.c_str(), exception.size(), &assembly_load_exception_log);

      ILInstr* load_exception_string = rewriter.NewILInstr();
      load_exception_string->m_opcode = CEE_LDSTR;
      load_exception_string->m_Arg32 = assembly_load_exception_log;

      ILInstr* call_console_exception_start = rewriter.NewILInstr();
      call_console_exception_start->m_opcode = CEE_CALL;
      call_console_exception_start->m_Arg32 = console_write_line_member_ref_;

      ILInstr* load_start_string = rewriter.NewILInstr();
      load_start_string->m_opcode = CEE_LDSTR;
      load_start_string->m_Arg32 = assembly_load_start_log;

      ILInstr* call_console_log_start = rewriter.NewILInstr();
      call_console_log_start->m_opcode = CEE_CALL;
      call_console_log_start->m_Arg32 = console_write_line_member_ref_;

      ILInstr* load_end_string = rewriter.NewILInstr();
      load_end_string->m_opcode = CEE_LDSTR;
      load_end_string->m_Arg32 = assembly_load_end_log;

      ILInstr* call_console_log_end = rewriter.NewILInstr();
      call_console_log_end->m_opcode = CEE_CALL;
      call_console_log_end->m_Arg32 = console_write_line_member_ref_;

      ILInstr* load_assembly_name_str = rewriter.NewILInstr();
      load_assembly_name_str->m_opcode = CEE_LDSTR;
      load_assembly_name_str->m_Arg32 = assembly_name_token;

      ILInstr* call_assembly_load = rewriter.NewILInstr();
      call_assembly_load->m_opcode = CEE_CALL;
      call_assembly_load->m_Arg32 = entry_load_assembly_member_ref_;

      ILInstr* nop_instruction = rewriter.NewILInstr();
      nop_instruction->m_opcode = CEE_NOP;
      nop_instruction->m_Arg32 = 0;

      ILInstr* pop_instruction = rewriter.NewILInstr();
      pop_instruction->m_opcode = CEE_POP;
      pop_instruction->m_Arg32 = 0;

      const auto first_instruction = rewriter.GetILList()->m_pNext;

      auto eh_clause = EHClause();

      eh_clause.m_pTryBegin = load_start_string;
      eh_clause.m_pTryEnd = call_console_log_end;
      eh_clause.m_pHandlerBegin = load_exception_string;
      eh_clause.m_pHandlerEnd = call_console_exception_start;

      std::vector<ILInstr*> main_instructions_to_inject = {

          load_start_string, call_console_log_start,

          load_assembly_name_str, call_assembly_load, pop_instruction,

          load_end_string, call_console_log_end,

          load_exception_string, call_console_exception_start};

      ILInstr* previous = nullptr;
      for (auto instr : main_instructions_to_inject) {
        // if (previous != nullptr) {
        //   previous->m_pNext = instr;
        //   instr->m_pPrev = previous;
        // }
        rewriter.InsertBefore(first_instruction, instr);
        previous = instr;
      }

      rewriter.AddNewEHClause(eh_clause);

      hr = rewriter.Export();

      if (FAILED(hr)) {
        Warn("All is lost!");
      }
    }
    attempted_pre_load_managed_assembly_ = true;
  }

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

      auto wrapper_method_signature_size =
          method_replacement.wrapper_method.method_signature.data.size();

      if (wrapper_method_signature_size < 5) {
        // This is invalid, we should always have the wrapper fully defined
        // Minimum:
        // 0:{CallingConvention}|1:{ParamCount}|2:{ReturnType}|3:{OpCode}|4:{mdToken}
        // Drop out for safety
        if (debug_logging_enabled) {
          Debug(
              "JITCompilationStarted skipping method: signature too short. "
              "function_id=",
              function_id, " token=", function_token,
              " name=", caller.type.name, ".", caller.name, "()",
              " wrapper_method_signature_size=", wrapper_method_signature_size);
        }

        continue;
      }

      auto expected_number_args = method_replacement.wrapper_method
                                      .method_signature.NumberOfArguments();

      // We pass the opcode and mdToken as the last arguments to every wrapper
      // method
      expected_number_args = expected_number_args - 2;

      if (target.signature.IsInstanceMethod()) {
        // We always pass the instance as the first argument
        expected_number_args--;
      }

      auto target_arg_count = target.signature.NumberOfArguments();

      if (expected_number_args != target_arg_count) {
        // Number of arguments does not match our wrapper method
        if (debug_logging_enabled) {
          Debug(
              "JITCompilationStarted skipping method: argument counts don't "
              "match. function_id=",
              function_id, " token=", function_token,
              " name=", caller.type.name, ".", caller.name, "()",
              " expected_number_args=", expected_number_args,
              " target_arg_count=", target_arg_count);
        }

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

      std::vector<WSTRING> sig_types;
      const auto successfully_parsed_signature = TryParseSignatureTypes(
          module_metadata->metadata_import, target, sig_types);
      auto expected_sig_types =
          method_replacement.target_method.signature_types;

      if (!successfully_parsed_signature) {
        if (debug_logging_enabled) {
          Debug(
              "JITCompilationStarted skipping method: failed to parse "
              "signature. function_id=",
              function_id, " token=", function_token,
              " name=", caller.type.name, ".", caller.name, "()",
              " successfully_parsed_signature=", successfully_parsed_signature,
              " sig_types.size()=", sig_types.size(),
              " expected_sig_types.size()=", expected_sig_types.size());
        }

        continue;
      }

      if (sig_types.size() != expected_sig_types.size()) {
        // we can't safely assume our wrapper methods handle the types
        if (debug_logging_enabled) {
          Debug(
              "JITCompilationStarted skipping method: unexpected type count. "
              "function_id=",
              function_id, " token=", function_token,
              " name=", caller.type.name, ".", caller.name, "()",
              " successfully_parsed_signature=", successfully_parsed_signature,
              " sig_types.size()=", sig_types.size(),
              " expected_sig_types.size()=", expected_sig_types.size());
        }

        continue;
      }

      auto is_match = true;
      for (size_t i = 0; i < expected_sig_types.size(); i++) {
        if (expected_sig_types[i] == "_"_W) {
          // We are supposed to ignore this index
          continue;
        }
        if (expected_sig_types[i] != sig_types[i]) {
          // we have a type mismatch, drop out
          if (debug_logging_enabled) {
            Debug(
                "JITCompilationStarted skipping method: types don't match. "
                "function_id=",
                function_id, " token=", function_token,
                " name=", caller.type.name, ".", caller.name, "()",
                " expected_sig_types[", i, "]=", expected_sig_types[i],
                " sig_types[", i, "]=", sig_types[i]);
          }

          is_match = false;
          break;
        }
      }

      if (!is_match) {
        // signatures don't match
        continue;
      }

      const auto original_argument = pInstr->m_Arg32;

      // insert the opcode and signature token as
      // additional arguments for the wrapper method
      ILRewriterWrapper rewriter_wrapper(&rewriter);
      rewriter_wrapper.SetILPosition(pInstr);
      rewriter_wrapper.LoadInt32(pInstr->m_opcode);
      rewriter_wrapper.LoadInt32(method_def_md_token);

      // always use CALL because the wrappers methods are all static
      pInstr->m_opcode = CEE_CALL;
      // replace with a call to the instrumentation wrapper
      pInstr->m_Arg32 = wrapper_method_ref;

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
    RETURN_OK_IF_FAILED(hr);
  }

  return S_OK;
}

bool CorProfiler::IsAttached() const { return is_attached_; }

}  // namespace trace
