#ifndef DD_CLR_PROFILER_COR_PROFILER_H_
#define DD_CLR_PROFILER_COR_PROFILER_H_

#include <atomic>
#include <mutex>
#include <string>
#include <unordered_map>
#include "cor.h"
#include "corprof.h"

#include "cor_profiler_base.h"
#include "environment_variables.h"
#include "integration.h"
#include "module_metadata.h"
#include "pal.h"

namespace trace {

class CorProfiler : public CorProfilerBase {
 private:
  bool is_attached_ = false;
  bool dot_net_assembly_is_loaded = false;
  bool entry_assembly_is_loaded = false;
  bool managed_assembly_is_loaded_ = false;
  bool attempted_pre_load_managed_assembly_ = false;

  ModuleMetadata* dot_net_metadata_;
  const WSTRING datadog_managed_assembly_name_ =
      "Datadog.Trace.ClrProfiler.Managed"_W;

  std::vector<Integration> integrations_;

  std::mutex module_id_to_info_map_lock_;
  std::unordered_map<ModuleID, ModuleMetadata*> module_id_to_info_map_;

 public:
  CorProfiler() = default;

  bool IsAttached() const;

  HRESULT STDMETHODCALLTYPE
  Initialize(IUnknown* cor_profiler_info_unknown) override;

  HRESULT STDMETHODCALLTYPE ModuleLoadFinished(ModuleID module_id,
                                               HRESULT hr_status) override;

  HRESULT STDMETHODCALLTYPE ModuleUnloadStarted(ModuleID module_id) override;

  HRESULT STDMETHODCALLTYPE
  JITCompilationStarted(FunctionID function_id, BOOL is_safe_to_block) override;

  HRESULT STDMETHODCALLTYPE Shutdown() override;
};

// Note: Generally you should not have a single, global callback implementation,
// as that prevents your profiler from analyzing multiply loaded in-process
// side-by-side CLRs. However, this profiler implements the "profile-first"
// alternative of dealing with multiple in-process side-by-side CLR instances.
// First CLR to try to load us into this process wins; so there can only be one
// callback implementation created. (See ProfilerCallback::CreateObject.)
extern CorProfiler* profiler;  // global reference to callback object

}  // namespace trace

#endif  // DD_CLR_PROFILER_COR_PROFILER_H_
