#ifndef DD_CLR_PROFILER_LOADER_H_
#define DD_CLR_PROFILER_LOADER_H_

#include "module_metadata.h"

namespace trace {

class Loader {
 private:
  RuntimeInformation runtime_information_;
  ICorProfilerInfo4* info_;

  HRESULT GenerateVoidILStartupMethod(const ModuleID module_id,
                                      mdMethodDef* ret_method_token);
 public:
  Loader(ICorProfilerInfo4* info);

  void GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int* assemblySize,
                                  BYTE** pSymbolsArray, int* symbolsSize) const;


  HRESULT RunILStartupHook(const ComPtr<IMetaDataEmit2>&,
                           const ModuleID module_id,
                           const mdToken function_token);
};

extern Loader* loader;  // global reference to loader

}  // namespace trace

#endif // DD_CLR_PROFILER_LOADER_H_
