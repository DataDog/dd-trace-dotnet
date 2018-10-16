// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full
// license information.

#include <fstream>
#include <string>
#include <vector>

#include "clr_helpers.h"
#include "com_ptr.h"
#include "cor_profiler.h"
#include "il_rewriter.h"
#include "integration_loader.h"
#include "logging.h"
#include "macros.h"
#include "metadata_builder.h"
#include "module_metadata.h"
#include "util.h"

namespace trace {

CorProfiler* profiler = nullptr;

CorProfiler::CorProfiler() : integrations_(LoadIntegrationsFromEnvironment()) {
  Info("CorProfiler::CorProfiler");
}

HRESULT STDMETHODCALLTYPE
CorProfiler::Initialize(IUnknown* cor_profiler_info_unknown) {
  is_attached_ = FALSE;

  const auto process_name = GetCurrentProcessName();
  Info("Initialize() called for {}", process_name);

  if (integrations_.empty()) {
    Warn("Profiler disabled: {} environment variable not set.",
         kIntegrationsEnvironmentName);
    return E_FAIL;
  }

  const auto allowed_process_names =
      GetEnvironmentValues(kProcessesEnvironmentName);

  if (allowed_process_names.empty()) {
    Info("{} environment variable not set. Attaching to any .NET process.",
         kProcessesEnvironmentName);
  } else {
    Info("{}:", kProcessesEnvironmentName);
    for (auto& name : allowed_process_names) {
      Info("  {}", name);
    }

    if (std::find(allowed_process_names.begin(), allowed_process_names.end(),
                  process_name) == allowed_process_names.end()) {
      Info(
          "Profiler disabled: module name \"{}\" does not match {} environment "
          "variable",
          process_name, kProcessesEnvironmentName);
      return E_FAIL;
    }
  }

  HRESULT hr = cor_profiler_info_unknown->QueryInterface<ICorProfilerInfo3>(
      &this->info_);
  if (FAILED(hr)) {
    Warn("Profiler disabled: interface ICorProfilerInfo3 or higher not found.");
  }

  hr = this->info_->SetEventMask(kEventMask);
  if (FAILED(hr)) {
    Warn("Failed to attach profiler: unable to set event mask.");
  }

  // we're in!
  Info("Profiler attached to process {}", process_name);
  this->info_->AddRef();
  is_attached_ = true;
  profiler = this;
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleLoadFinished(ModuleID module_id,
                                                          HRESULT hrStatus) {
  auto module_info = GetModuleInfo(this->info_, module_id);
  if (!module_info.IsValid()) {
    return S_OK;
  }

  if (module_info.IsWindowsRuntime() ||
      module_info.assembly.name == L"mscorlib" ||
      module_info.assembly.name == L"netstandard") {
    // We cannot obtain writeable metadata interfaces on Windows Runtime modules
    // or instrument their IL. We must never try to add assembly references to
    // mscorlib or netstandard.
    Info("ModuleLoadFinished() called for {}. Skipping instrumentation.",
         module_info.assembly.name);
    return S_OK;
  }

  std::vector<Integration> enabled_integrations =
      FilterIntegrationsByCaller(integrations_, module_info.assembly.name);
  if (enabled_integrations.empty()) {
    // we don't need to instrument anything in this module, skip it
    Info(
        "ModuleLoadFinished() called for {}. FilterIntegrationsByCaller() "
        "returned empty list. Nothing to instrument here.",
        module_info.assembly.name);
    return S_OK;
  }

  ComPtr<IUnknown> metadata_interfaces;

  auto hr = this->info_->GetModuleMetaData(module_id, ofRead | ofWrite,
                                           IID_IMetaDataImport2,
                                           metadata_interfaces.GetAddressOf());

  if (FAILED(hr)) {
    Warn("failed to get metadata interface");
  }

  const auto metadata_import =
      metadata_interfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
  const auto metadata_emit =
      metadata_interfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit);
  const auto assembly_import = metadata_interfaces.As<IMetaDataAssemblyImport>(
      IID_IMetaDataAssemblyImport);
  const auto assembly_emit =
      metadata_interfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

  enabled_integrations =
      FilterIntegrationsByTarget(enabled_integrations, assembly_import);
  if (enabled_integrations.empty()) {
    // we don't need to instrument anything in this module, skip it
    Info(
        "ModuleLoadFinished() called for {}. FilterIntegrationsByTarget() "
        "returned empty list. Nothing to instrument here.",
        module_info.assembly.name);
    return S_OK;
  }

  Info("ModuleLoadFinished() will try to emit instrumentation metadata for {}",
       module_info.assembly.name);

  mdModule module;
  hr = metadata_import->GetModuleFromScope(&module);
  if (FAILED(hr)) {
    Warn("ModuleLoadFinished() failed to get module token.");
  }

  ModuleMetadata* module_metadata =
      new ModuleMetadata(metadata_import, metadata_emit,
                         module_info.assembly.name, enabled_integrations);

  MetadataBuilder metadata_builder(*module_metadata, module, metadata_import,
                                   metadata_emit, assembly_import,
                                   assembly_emit);

  for (const auto& integration : enabled_integrations) {
    for (const auto& method_replacement : integration.method_replacements) {
      // for each wrapper assembly, emit an assembly reference
      hr = metadata_builder.EmitAssemblyRef(
          method_replacement.wrapper_method.assembly);
      RETURN_OK_IF_FAILED(hr);

      // for each method replacement in each enabled integration,
      // emit a reference to the instrumentation wrapper methods
      hr = metadata_builder.StoreWrapperMethodRef(method_replacement);
      RETURN_OK_IF_FAILED(hr);
    }
  }

  // store module info for later lookup
  module_id_to_info_map_.Update(module_id, module_metadata);

  Info("ModuleLoadFinished() emitted instrumentation metadata for {}",
       module_info.assembly.name);
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleUnloadFinished(ModuleID moduleId,
                                                            HRESULT hrStatus) {
  ModuleMetadata* metadata;

  if (module_id_to_info_map_.LookupIfExists(moduleId, &metadata)) {
    module_id_to_info_map_.Erase(moduleId);
    delete metadata;
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

  if (!module_id_to_info_map_.LookupIfExists(module_id, &module_metadata)) {
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

      // make sure the calling convention matches
      if (!method_replacement.target_method.method_signature.data.empty() &&
          method_replacement.target_method.method_signature
                  .CallingConvention() !=
              target.signature.CallingConvention()) {
        continue;
      }

      // make sure the number of arguments match
      if (method_replacement.target_method.method_signature.data.size() > 1 &&
          method_replacement.target_method.method_signature
                  .NumberOfArguments() !=
              target.signature.NumberOfArguments()) {
        continue;
      }

      // we need to emit a method spec to populate the generic arguments
      if (method_replacement.wrapper_method.method_signature
              .NumberOfTypeArguments() > 0) {
        wrapper_method_ref =
            DefineMethodSpec(module_metadata->metadata_emit, wrapper_method_ref,
                             target.signature);
      }

      // replace with a call to the instrumentation wrapper
      const auto original_argument = pInstr->m_Arg32;
      pInstr->m_opcode = CEE_CALL;
      pInstr->m_Arg32 = wrapper_method_ref;

      modified = true;

      Info(
          "JITCompilationStarted() replaced calls from {}.{}() to {}.{}() {:x} "
          "with calls to {}.{}() {:x}.",
          caller.type.name, caller.name,
          method_replacement.target_method.type_name,
          method_replacement.target_method.method_name,
          int32_t(original_argument),
          method_replacement.wrapper_method.type_name,
          method_replacement.wrapper_method.method_name,
          uint32_t(wrapper_method_ref));
    }
  }

  if (modified) {
    hr = rewriter.Export();
    RETURN_OK_IF_FAILED(hr);
  }

  return S_OK;
}  // namespace trace

bool CorProfiler::IsAttached() const { return is_attached_; }

}  // namespace trace
