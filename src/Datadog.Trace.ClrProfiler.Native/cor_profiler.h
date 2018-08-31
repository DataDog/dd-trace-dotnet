#ifndef DD_CLR_PROFILER_COR_PROFILER_H_
#define DD_CLR_PROFILER_COR_PROFILER_H_

#include <vector>

#include <corhlpr.h>
#include <corprof.h>
#include "IDToInfoMap.h"
#include "ModuleMetadata.h"
#include "cor_profiler_base.h"
#include "integration.h"

namespace trace {

const std::wstring kProcessesEnvironmentName = L"DATADOG_PROFILER_PROCESSES";
const unsigned long kEventMask =
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

class CorProfiler : public CorProfilerBase {
 private:
  bool is_attached_ = false;
  IDToInfoMap<ModuleID, ModuleMetadata*> module_id_to_info_map_;
  const std::vector<Integration> integrations_;

 public:
  CorProfiler();

  bool IsAttached() const;

  HRESULT STDMETHODCALLTYPE Initialize(IUnknown* pICorProfilerInfoUnk) override;
  HRESULT STDMETHODCALLTYPE JITCompilationStarted(FunctionID functionId,
                                                  BOOL fIsSafeToBlock) override;
  HRESULT STDMETHODCALLTYPE ModuleLoadFinished(ModuleID moduleId,
                                               HRESULT hrStatus) override;
  HRESULT STDMETHODCALLTYPE ModuleUnloadFinished(ModuleID moduleId,
                                                 HRESULT hrStatus) override;
};

// Note: Generally you should not have a single, global callback implementation,
// as that prevents your profiler from analyzing multiply loaded in-process
// side-by-side CLRs. However, this profiler implements the "profile-first"
// alternative of dealing with multiple in-process side-by-side CLR instances.
// First CLR to try to load us into this process wins; so there can only be one
// callback implementation created. (See ProfilerCallback::CreateObject.)
extern CorProfiler* profiler;  // global reference to callback object

namespace {

// FilterIntegrationsByCaller removes any integrations which have a caller and
// its not set to the module
std::vector<Integration> FilterIntegrationsByCaller(
    const std::vector<Integration>& integrations,
    const std::wstring& assembly_name);

// FilterIntegrationsByTarget removes any integrations which have a target not
// referenced by the module's assembly import
std::vector<Integration> FilterIntegrationsByTarget(
    const std::vector<Integration>& integrations,
    const ComPtr<IMetaDataAssemblyImport>& assembly_import);

}  // namespace

}  // namespace trace

#endif  // DD_CLR_PROFILER_COR_PROFILER_H_
