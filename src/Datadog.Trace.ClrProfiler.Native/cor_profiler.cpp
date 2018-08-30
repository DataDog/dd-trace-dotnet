// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full
// license information.

#include "cor_profiler.h"
#include <fstream>
#include <string>
#include <vector>
#include "ComPtr.h"
#include "ILRewriter.h"
#include "Macros.h"
#include "ModuleMetadata.h"
#include "clr_helpers.h"
#include "integration_loader.h"
#include "metadata_builder.h"
#include "util.h"

namespace trace {

CorProfiler* profiler = nullptr;

CorProfiler::CorProfiler() : integrations_(LoadIntegrationsFromEnvironment()) {}

HRESULT STDMETHODCALLTYPE
CorProfiler::Initialize(IUnknown* pICorProfilerInfoUnk) {
  is_attached_ = FALSE;

  auto process_name = GetCurrentProcessName();
  auto allowed_process_names = GetEnvironmentValues(kProcessesEnvironmentName);

  if (allowed_process_names.size() == 0) {
    LOG_APPEND(
        L"DATADOG_PROFILER_PROCESSES environment variable not set. Attaching "
        L"to any .NET process.");
  } else {
    LOG_APPEND(L"DATADOG_PROFILER_PROCESSES:");
    for (auto& name : allowed_process_names) {
      LOG_APPEND(L"  " + name);
    }

    if (std::find(allowed_process_names.begin(), allowed_process_names.end(),
                  process_name) == allowed_process_names.end()) {
      LOG_APPEND(L"CorProfiler disabled: module name \""
                 << process_name
                 << "\" does not match DATADOG_PROFILER_PROCESSES environment "
                    "variable.");
      return E_FAIL;
    }
  }

  HRESULT hr =
      pICorProfilerInfoUnk->QueryInterface<ICorProfilerInfo3>(&this->info_);
  LOG_IFFAILEDRET(hr,
                  L"CorProfiler disabled: interface ICorProfilerInfo3 or "
                  L"higher not found.");

  const DWORD eventMask =
      COR_PRF_MONITOR_JIT_COMPILATION |
      COR_PRF_DISABLE_TRANSPARENCY_CHECKS_UNDER_FULL_TRUST | /* helps the case
                                                                  where this
                                                                  profiler is
                                                                  used on Full
                                                                  CLR */
      // COR_PRF_DISABLE_INLINING |
      COR_PRF_MONITOR_MODULE_LOADS |
      // COR_PRF_MONITOR_ASSEMBLY_LOADS |
      // COR_PRF_MONITOR_APPDOMAIN_LOADS |
      // COR_PRF_ENABLE_REJIT |
      COR_PRF_DISABLE_ALL_NGEN_IMAGES;

  hr = this->info_->SetEventMask(eventMask);
  LOG_IFFAILEDRET(hr, L"Failed to attach profiler: unable to set event mask.");

  // we're in!
  LOG_APPEND(L"CorProfiler attached to process " << process_name);
  this->info_->AddRef();
  is_attached_ = true;
  profiler = this;
  return S_OK;
}

HRESULT STDMETHODCALLTYPE CorProfiler::ModuleLoadFinished(ModuleID moduleId,
                                                          HRESULT hrStatus) {
  LPCBYTE pbBaseLoadAddr;
  WCHAR wszModulePath[MAX_PATH];
  ULONG cchNameOut;
  AssemblyID assembly_id = 0;
  DWORD dwModuleFlags;

  HRESULT hr = this->info_->GetModuleInfo2(
      moduleId, &pbBaseLoadAddr, _countof(wszModulePath), &cchNameOut,
      wszModulePath, &assembly_id, &dwModuleFlags);

  LOG_IFFAILEDRET(hr,
                  L"GetModuleInfo2 failed for ModuleID = " << HEX(moduleId));

  if ((dwModuleFlags & COR_PRF_MODULE_WINDOWS_RUNTIME) != 0) {
    // Ignore any Windows Runtime modules.  We cannot obtain writeable
    // metadata interfaces on them or instrument their IL
    return S_OK;
  }

  auto assembly_name = GetAssemblyName(this->info_, assembly_id);

  std::vector<integration> enabledIntegrations;

  // check if we need to instrument anything in this assembly,
  // for each integration...
  for (const auto& integration : this->integrations_) {
    // TODO: check if integration is enabled in config
    for (const auto& method_replacement : integration.method_replacements) {
      if (method_replacement.caller_method.assembly.name.empty() ||
          method_replacement.caller_method.assembly.name == assembly_name) {
        enabledIntegrations.push_back(integration);
      }
    }
  }

  LOG_APPEND(L"ModuleLoadFinished for "
             << assembly_name << ". Emitting instrumentation metadata.");

  if (enabledIntegrations.empty()) {
    // we don't need to instrument anything in this module, skip it
    return S_OK;
  }

  ComPtr<IUnknown> metadataInterfaces;

  hr = this->info_->GetModuleMetaData(moduleId, ofRead | ofWrite,
                                      IID_IMetaDataImport,
                                      metadataInterfaces.GetAddressOf());

  LOG_IFFAILEDRET(hr, L"Failed to get metadata interface.");

  const auto metadataImport =
      metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport);
  const auto metadataEmit =
      metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit);
  const auto assemblyImport = metadataInterfaces.As<IMetaDataAssemblyImport>(
      IID_IMetaDataAssemblyImport);
  const auto assemblyEmit =
      metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit);

  mdModule module;
  hr = metadataImport->GetModuleFromScope(&module);
  LOG_IFFAILEDRET(hr, L"Failed to get module token.");

  ModuleMetadata* moduleMetadata =
      new ModuleMetadata(metadataImport, assembly_name, enabledIntegrations);

  MetadataBuilder metadataBuilder(*moduleMetadata, module, metadataImport,
                                  metadataEmit, assemblyImport, assemblyEmit);

  for (const auto& integration : enabledIntegrations) {
    for (const auto& method_replacement : integration.method_replacements) {
      // for each wrapper assembly, emit an assembly reference
      hr = metadataBuilder.EmitAssemblyRef(
          method_replacement.wrapper_method.assembly);
      RETURN_OK_IF_FAILED(hr);

      // for each method replacement in each enabled integration,
      // emit a reference to the instrumentation wrapper methods
      hr = metadataBuilder.StoreWrapperMethodRef(method_replacement);
      RETURN_OK_IF_FAILED(hr);
    }
  }

  // store module info for later lookup
  module_id_to_info_map_.Update(moduleId, moduleMetadata);
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

HRESULT STDMETHODCALLTYPE
CorProfiler::JITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock) {
  ClassID class_id;
  ModuleID module_id;
  mdToken functionToken = mdTokenNil;

  HRESULT hr = this->info_->GetFunctionInfo(functionId, &class_id, &module_id,
                                            &functionToken);
  RETURN_OK_IF_FAILED(hr);

  ModuleMetadata* module_metadata = nullptr;

  if (!module_id_to_info_map_.LookupIfExists(module_id, &module_metadata)) {
    // we haven't stored a ModuleInfo for this module, so we can't modify its
    // IL
    return S_OK;
  }

  // get function info
  auto caller =
      GetFunctionInfo(module_metadata->metadata_import, functionToken);
  if (!caller.isvalid()) {
    return S_OK;
  }

  auto method_replacements =
      module_metadata->GetMethodReplacementsForCaller(caller);
  if (method_replacements.empty()) {
    return S_OK;
  }

  ILRewriter rewriter(this->info_, nullptr, module_id, functionToken);
  bool modified = false;

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

    // hr = rewriter.Initialize();
    hr = rewriter.Import();
    RETURN_OK_IF_FAILED(hr);

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
      if (!target.isvalid()) {
        continue;
      }

      // if the target matches by type name and method name
      if (method_replacement.target_method.type_name == target.type.name &&
          method_replacement.target_method.method_name == target.name) {
        // replace with a call to the instrumentation wrapper
        pInstr->m_opcode = CEE_CALL;
        pInstr->m_Arg32 = wrapper_method_ref;

        modified = true;
      }
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
