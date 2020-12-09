#ifndef DD_CLR_PROFILER_LOADER_H_
#define DD_CLR_PROFILER_LOADER_H_

#include "module_metadata.h"

namespace trace {

class Loader {
 private:
  RuntimeInformation runtime_information_;
  ICorProfilerInfo4* info_;

 public:
  Loader(ICorProfilerInfo4* info);
  void GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int* assemblySize,
                                  BYTE** pSymbolsArray, int* symbolsSize) const;
};

extern Loader* loader;  // global reference to loader

}  // namespace trace

#endif // DD_CLR_PROFILER_LOADER_H_
