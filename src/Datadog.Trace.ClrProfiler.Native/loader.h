#ifndef DD_CLR_PROFILER_LOADER_H_
#define DD_CLR_PROFILER_LOADER_H_

#include <mutex>
#include <unordered_set>

#include "clr_helpers.h"

namespace trace {

    class Loader {
    private:
        RuntimeInformation runtime_information_;
        ICorProfilerInfo4* info_;

        std::mutex loaders_loaded_mutex_;
        std::unordered_set<AppDomainID> loaders_loaded_;

    public:
        Loader(ICorProfilerInfo4* info);

        HRESULT InjectLoaderToModuleInitializer(const ModuleID module_id);

        bool GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int* assemblySize,
                                        BYTE** pSymbolsArray, int* symbolsSize, AppDomainID appDomainId);
    };

    extern Loader* loader;  // global reference to loader

}  // namespace trace

#endif // DD_CLR_PROFILER_LOADER_H_