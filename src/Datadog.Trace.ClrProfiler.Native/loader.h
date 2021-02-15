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
        bool is_iis_;

        std::mutex loaders_loaded_mutex_;
        std::unordered_set<AppDomainID> loaders_loaded_;

        std::function<void(const std::string& str)> log_debug_callback_ = nullptr;
        std::function<void(const std::string& str)> log_info_callback_ = nullptr;
        std::function<void(const std::string& str)> log_warn_callback_ = nullptr;

        void Debug(const std::string& str) {
          if (log_debug_callback_ != nullptr) {
            log_debug_callback_(str);
          }
        }
        void Info(const std::string& str) {
          if (log_info_callback_ != nullptr) {
            log_info_callback_(str);
          }
        }
        void Warn(const std::string& str) {
          if (log_warn_callback_ != nullptr) {
            log_warn_callback_(str);
          }
        }

    public:
        Loader(ICorProfilerInfo4* info, bool isIIS,
               std::function<void(const std::string& str)> log_debug_callback,
               std::function<void(const std::string& str)> log_info_callback,
               std::function<void(const std::string& str)> log_warn_callback_);

        HRESULT InjectLoaderToModuleInitializer(const ModuleID module_id);

        bool GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int* assemblySize,
                                        BYTE** pSymbolsArray, int* symbolsSize, AppDomainID appDomainId);
    };

    extern Loader* loader;  // global reference to loader

}  // namespace trace

#endif // DD_CLR_PROFILER_LOADER_H_