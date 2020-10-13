#include "rejit_handler.h"

namespace trace {

void RejitHandlerModuleMethod::AddFunctionId(FunctionID functionId) {
  std::lock_guard<std::mutex> guard(functionsIds_lock);
  functionsIds.insert(functionId);
}
bool RejitHandlerModuleMethod::ExistFunctionId(FunctionID functionId) {
  std::lock_guard<std::mutex> guard(functionsIds_lock);
  return functionsIds.find(functionId) != functionsIds.end();
}

void RejitHandlerModuleMethod::Dump() {
  Info("   RejitHandlerModuleMethod [MethodDef = ", methodDef,
       ", FunctionControl = ", pFunctionControl != nullptr, "]");
  for (auto functionId : functionsIds) {
    Info("      FunctionID: ", functionId);
  }
}

RejitHandlerModuleMethod* RejitHandlerModule::GetOrAddMethod(
    mdMethodDef methodDef, ICorProfilerFunctionControl* pFunctionControl) {
  std::lock_guard<std::mutex> guard(methods_lock);

  if (methods.count(methodDef) > 0) {
    return methods[methodDef];
  }

  RejitHandlerModuleMethod* methodHandler =
      new RejitHandlerModuleMethod(methodDef, pFunctionControl);
  methods[methodDef] = methodHandler;
  return methodHandler;
}

void RejitHandlerModule::Dump() {
  std::lock_guard<std::mutex> guard(methods_lock);

  Info("RejitHandlerModule [ModuleId = ", moduleId,
       ", Metadata = ", metadata != nullptr, "]");
  for (std::pair<const mdMethodDef, RejitHandlerModuleMethod*> pair :
       this->methods) {
    pair.second->Dump();
  }
}

RejitHandlerModule* RejitHandler::GetOrAddModule(ModuleID moduleId,
                                                 ModuleMetadata* metadata) {
  std::lock_guard<std::mutex> guard(modules_lock);

  if (modules.count(moduleId) > 0) {
    return modules[moduleId];
  }

  RejitHandlerModule* moduleHandler =
      new RejitHandlerModule(moduleId, metadata);
  modules[moduleId] = moduleHandler;
  return moduleHandler;
}

void RejitHandler::SetReJITParameters(
    ModuleID moduleId, mdMethodDef methodId,
    ICorProfilerFunctionControl* pFunctionControl, ModuleMetadata* metadata) {
  GetOrAddModule(moduleId, metadata)
      ->GetOrAddMethod(methodId, pFunctionControl);
}

void RejitHandler::ReJITCompilationStarted(FunctionID functionId,
    ReJITID rejitId) {
  ModuleID moduleId;
  mdToken function_token = mdTokenNil;

  HRESULT hr = profilerInfo->GetFunctionInfo(functionId, nullptr, &moduleId,
                                            &function_token);

  auto moduleHandler = GetOrAddModule(moduleId, NULL);
  auto methodHandler = moduleHandler->GetOrAddMethod(function_token, NULL);
  methodHandler->AddFunctionId(functionId);
}

void RejitHandler::Dump() {
  std::lock_guard<std::mutex> guard(this->modules_lock);
  for (std::pair<const ModuleID, RejitHandlerModule*> pair : this->modules) {
    pair.second->Dump();
  }
}

}  // namespace trace