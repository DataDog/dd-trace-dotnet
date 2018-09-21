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
#include "macros.h"
#include "metadata_builder.h"
#include "module_metadata.h"
#include "util.h"

namespace trace {

CorProfiler* profiler = nullptr;

CorProfiler::CorProfiler() : integrations_(LoadIntegrationsFromEnvironment()) {}

HRESULT STDMETHODCALLTYPE
CorProfiler::Initialize(IUnknown* cor_profiler_info_unknown) {
  is_attached_ = FALSE;

  const auto process_name = GetCurrentProcessName();
  LOG_APPEND(L"Initialize() called for " << process_name);

  if (integrations_.empty()) {
    LOG_APPEND(L"Profiler disabled: " << kIntegrationsEnvironmentName
                                      << L" environment variable not set.");
    return E_FAIL;
  }

  const auto allowed_process_names =
      GetEnvironmentValues(kProcessesEnvironmentName);

  if (allowed_process_names.empty()) {
    LOG_APPEND(
        kProcessesEnvironmentName
        << L" environment variable not set. Attaching to any .NET process.");
  } else {
    LOG_APPEND(kProcessesEnvironmentName << L":");
    for (auto& name : allowed_process_names) {
      LOG_APPEND(L"  " + name);
    }

    if (std::find(allowed_process_names.begin(), allowed_process_names.end(),
                  process_name) == allowed_process_names.end()) {
      LOG_APPEND(L"Profiler disabled: module name \""
                 << process_name << "\" does not match "
                 << kProcessesEnvironmentName << " environment variable.");
      return E_FAIL;
    }
  }

  HRESULT hr = cor_profiler_info_unknown->QueryInterface<ICorProfilerInfo3>(
      &this->info_);
  LOG_IFFAILEDRET(hr,
                  L"Profiler disabled: interface ICorProfilerInfo3 or "
                  L"higher not found.");

  hr = this->info_->SetEventMask(kEventMask);
  LOG_IFFAILEDRET(hr, L"Failed to attach profiler: unable to set event mask.");

  // we're in!
  LOG_APPEND(L"Profiler attached to process " << process_name);
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
    LOG_APPEND(L"ModuleLoadFinished() called for "
               << module_info.assembly.name << ". Skipping instrumentation.");
    return S_OK;
  }

  std::vector<Integration> enabled_integrations =
      FilterIntegrationsByCaller(integrations_, module_info.assembly.name);
  if (enabled_integrations.empty()) {
    // we don't need to instrument anything in this module, skip it
    LOG_APPEND(L"ModuleLoadFinished() called for "
               << module_info.assembly.name
               << ". FilterIntegrationsByCaller() returned empty list. Nothing "
                  "to instrument here.");
    return S_OK;
  }

  ComPtr<IUnknown> metadata_interfaces;

  auto hr = this->info_->GetModuleMetaData(module_id, ofRead | ofWrite,
                                           IID_IMetaDataImport2,
                                           metadata_interfaces.GetAddressOf());

  LOG_IFFAILEDRET(hr, L"Failed to get metadata interface.");

  const auto metadata_import =
      metadata_interfaces.As<IMetaDataImport2>(IID_IMetaDataImport);
  const auto metadata_emit =
      metadata_interfaces.As<IMetaDataEmit>(IID_IMetaDataEmit);
  const auto assembly_import = metadata_interfaces.As<IMetaDataAssemblyImport>(
      IID_IMetaDataAssemblyImport);
  const auto assembly_emit =
      metadata_interfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

  enabled_integrations =
      FilterIntegrationsByTarget(enabled_integrations, assembly_import);
  if (enabled_integrations.empty()) {
    // we don't need to instrument anything in this module, skip it
    LOG_APPEND(L"ModuleLoadFinished() called for "
               << module_info.assembly.name
               << ". FilterIntegrationsByTarget() returned empty list. Nothing "
                  "to instrument here.");
    return S_OK;
  }

  LOG_APPEND(
      L"ModuleLoadFinished() will try to emit instrumentation metadata for "
      << module_info.assembly.name);

  mdModule module;
  hr = metadata_import->GetModuleFromScope(&module);
  LOG_IFFAILEDRET(hr, L"ModuleLoadFinished() failed to get module token.");

  ModuleMetadata* module_metadata = new ModuleMetadata(
      metadata_import, module_info.assembly.name, enabled_integrations);

  MetadataBuilder metadata_builder(*module_metadata, module, metadata_import,
                                   metadata_emit, assembly_import,
                                   assembly_emit);

  for (const auto& integration : enabled_integrations) {
    for (const auto& ma : integration.method_advice) {
      hr = metadata_builder.StoreMethodAdvice(ma);
      if (FAILED(hr)) {
        LOG_APPEND(L"failed to store method advice");
      }
    }

    for (const auto& method_replacement : integration.method_replacements) {
      // for each method replacement in each enabled integration,
      // emit a reference to the instrumentation wrapper methods
      hr = metadata_builder.StoreMethodReference(
          method_replacement.wrapper_method);
      RETURN_OK_IF_FAILED(hr);
    }
  }

  // store module info for later lookup
  module_id_to_info_map_.Update(module_id, module_metadata);

  LOG_APPEND(L"ModuleLoadFinished() emitted instrumentation metadata for "
             << module_info.assembly.name);
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
  auto function_info =
      GetFunctionInfo(module_metadata->metadata_import, function_token);
  if (!function_info.IsValid()) {
    return S_OK;
  }

  hr = InstrumentMethodAdvice(module_id, module_metadata, function_info);
  if (FAILED(hr)) {
    LOG_APPEND(L"failed to instrument method advice");
    return hr;
  }

  hr = InstrumentMethodReplacements(module_id, module_metadata, function_info);
  if (FAILED(hr)) {
    LOG_APPEND(L"failed to replace method calls");
    return hr;
  }

  return S_OK;
}

bool CorProfiler::IsAttached() const { return is_attached_; }

HRESULT CorProfiler::InstrumentMethodAdvice(
    const ModuleID& module_id, ModuleMetadata* module_metadata,
    const FunctionInfo& function_info) const {
  auto method_advice = module_metadata->GetMethodAdvice(function_info);
  if (method_advice.empty()) {
    // nothing to do
    return S_OK;
  }

  ILRewriter rewriter(this->info_, nullptr, module_id, function_info.id);
  auto hr = rewriter.Import();
  if (FAILED(hr)) {
    LOG_APPEND("failed to import method il instructions for method advice");
    return hr;
  }

  std::vector<ILInstr*> prelude;

  // emit a call to OnMethodEnter
  for (auto& ma : method_advice) {
    mdMemberRef method_token = mdMemberRefNil;
    if (!module_metadata->TryGetWrapperMemberRef(
            ma.OnMethodEnterReference().get_method_cache_key(), method_token)) {
      // no method ref token found for wrapper method, we can't do the
      // replacement, this should never happen because we always try to
      // add the method ref in ModuleLoadFinished()
      // TODO: log this
      return S_OK;
    }

    LOG_APPEND(L">>> adding call to"
               << ma.OnMethodEnterReference().get_method_cache_key() << L" in "
               << function_info.type.name << L"." << function_info.name << L": "
               << HEX(method_token));

    auto ld_instr = rewriter.NewILInstr();
    ld_instr->m_opcode = CEE_LDNULL;
    prelude.push_back(ld_instr);

    auto call_instr = rewriter.NewILInstr();
    call_instr->m_opcode = CEE_CALL;
    call_instr->m_Arg32 = method_token;
    prelude.push_back(call_instr);

    auto pop_instr = rewriter.NewILInstr();
    pop_instr->m_opcode = CEE_POP;
    prelude.push_back(pop_instr);
  }

  auto modified = false;

  auto front = rewriter.GetILList()->m_pNext;
  for (auto& instr : prelude) {
    rewriter.InsertBefore(front, instr);
    modified = true;
  }

  if (modified) {
    hr = rewriter.Export();
    if (FAILED(hr)) {
      LOG_APPEND("failed to export method il instructions for method advice");
      return hr;
    }
  }

  return S_OK;
}

HRESULT CorProfiler::InstrumentMethodReplacements(
    const ModuleID& module_id, ModuleMetadata* module_metadata,
    const FunctionInfo& function_info) const {
  auto method_replacements =
      module_metadata->GetMethodReplacementsForCaller(function_info);
  if (method_replacements.empty()) {
    return S_OK;
  }

  ILRewriter rewriter(this->info_, nullptr, module_id, function_info.id);
  bool modified = false;

  // hr = rewriter.Initialize();
  auto hr = rewriter.Import();
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
      if (method_replacement.target_method.type_reference.type_name !=
              target.type.name ||
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

      // replace with a call to the instrumentation wrapper
      const auto original_argument = pInstr->m_Arg32;
      pInstr->m_opcode = CEE_CALL;
      pInstr->m_Arg32 = wrapper_method_ref;

      modified = true;

      LOG_APPEND(L"JITCompilationStarted() replaced calls from "
                 << function_info.type.name << "." << function_info.name
                 << "() to "
                 << method_replacement.target_method.type_reference.type_name
                 << "." << method_replacement.target_method.method_name << "() "
                 << HEX(original_argument) << " with calls to "
                 << method_replacement.wrapper_method.type_reference.type_name
                 << "." << method_replacement.wrapper_method.method_name
                 << "() " << HEX(wrapper_method_ref) << ".");
    }
  }

  if (modified) {
    hr = rewriter.Export();
    RETURN_OK_IF_FAILED(hr);
  }

  return S_OK;
}

}  // namespace trace
