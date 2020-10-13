#ifndef DD_CLR_PROFILER_REJIT_HANDLER_H_
#define DD_CLR_PROFILER_REJIT_HANDLER_H_

#include <atomic>
#include <mutex>
#include <string>
#include <unordered_map>
#include <vector>

#include "cor.h"
#include "corprof.h"
#include "logging.h"
#include "module_metadata.h"

namespace trace {

class RejitHandlerModuleMethod {
 private:
  mdMethodDef methodDef;
  ICorProfilerFunctionControl* pFunctionControl;
  std::mutex functionsIds_lock;
  std::unordered_set<FunctionID> functionsIds;

 public:
  RejitHandlerModuleMethod(mdMethodDef methodDef,
                           ICorProfilerFunctionControl* pFunctionControl) {
    this->methodDef = methodDef;
    this->pFunctionControl = pFunctionControl;
  }
  inline mdMethodDef GetMethodDef() { return this->methodDef; }
  inline ICorProfilerFunctionControl* GetFunctionControl() {
    return this->pFunctionControl;
  }
  void AddFunctionId(FunctionID functionId);
  bool ExistFunctionId(FunctionID functionId);
  void Dump();
};

class RejitHandlerModule {
 private:
  ModuleID moduleId;
  ModuleMetadata* metadata;
  std::mutex methods_lock;
  std::unordered_map<mdMethodDef, RejitHandlerModuleMethod*> methods;

 public:
  RejitHandlerModule(ModuleID moduleId, ModuleMetadata* metadata) {
    this->moduleId = moduleId;
    this->metadata = metadata;
  }
  inline ModuleID GetModuleId() { return this->moduleId; }
  inline ModuleMetadata* GetModuleMetadata() { return this->metadata; }
  RejitHandlerModuleMethod* GetOrAddMethod(
      mdMethodDef methodDef, ICorProfilerFunctionControl* pFunctionControl);
  void Dump();
};

class RejitHandler {
 private:
  std::mutex modules_lock;
  std::unordered_map<ModuleID, RejitHandlerModule*> modules;
  ICorProfilerInfo4* profilerInfo;

 public:
  RejitHandler(ICorProfilerInfo4* pInfo) { profilerInfo = pInfo; }
  RejitHandlerModule* GetOrAddModule(ModuleID moduleId,
                                     ModuleMetadata* metadata);

  void SetReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                          ICorProfilerFunctionControl* pFunctionControl,
                          ModuleMetadata* metadata);
  void ReJITCompilationStarted(FunctionID functionId, ReJITID rejitId);
  void Dump();
};

}  // namespace trace

#endif  // DD_CLR_PROFILER_REJIT_HANDLER_H_