#ifndef DD_CLR_PROFILER_LOADER_H_
#define DD_CLR_PROFILER_LOADER_H_

#include <mutex>
#include <unordered_set>

#include "clr_helpers.h"
#include "il_rewriter.h"

#ifdef _WIN32
#define WStr(value) L##value
#define WStrLen(value) (size_t) wcslen(value)
#else
#define WStr(value) u##value
#define WStrLen(value) (size_t) std::char_traits<char16_t>::length(value)
#endif

namespace trace {

    class Loader {
    private:
        RuntimeInformation runtime_information_;
        ICorProfilerInfo4* info_;

        std::mutex loaders_loaded_mutex_;
        std::unordered_set<AppDomainID> loaders_loaded_;

        std::vector<WSTRING> assembly_string_default_appdomain_vector_;
        std::vector<WSTRING> assembly_string_nondefault_appdomain_vector_;

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

        HRESULT WriteAssembliesStringArray(
            ILRewriter& rewriter, const ComPtr<IMetaDataEmit2> metadata_emit,
            const std::vector<WSTRING>& assembly_string_vector,
            ILInstr* pFirstInstr, mdTypeRef string_type_ref);

    public:
        Loader(ICorProfilerInfo4* info,
               WSTRING* assembly_string_default_appdomain_array,
               ULONG assembly_string_default_appdomain_array_length,
               WSTRING* assembly_string_nondefault_appdomain_array,
               ULONG assembly_string_nondefault_appdomain_array_length,
               std::function<void(const std::string& str)> log_debug_callback,
               std::function<void(const std::string& str)> log_info_callback,
               std::function<void(const std::string& str)> log_warn_callback);

        Loader(ICorProfilerInfo4* info,
               std::vector<WSTRING> assembly_string_default_appdomain_vector,
               std::vector<WSTRING> assembly_string_nondefault_appdomain_vector,
               std::function<void(const std::string& str)> log_debug_callback,
               std::function<void(const std::string& str)> log_info_callback,
               std::function<void(const std::string& str)> log_warn_callback);

        HRESULT InjectLoaderToModuleInitializer(const ModuleID module_id);

        bool GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int* assemblySize,
                                        BYTE** pSymbolsArray, int* symbolsSize, AppDomainID appDomainId);

        static Loader* CreateLoader(
            ICorProfilerInfo4* info, 
            WSTRING process_name,
            std::function<void(const std::string& str)> log_debug_callback,
            std::function<void(const std::string& str)> log_info_callback,
            std::function<void(const std::string& str)> log_warn_callback) {

          std::vector<WSTRING> assembly_string_default_appdomain_vector;
          std::vector<WSTRING> assembly_string_nondefault_appdomain_vector;
          const bool is_iis = process_name == WStr("w3wp.exe") ||
                              process_name == WStr("iisexpress.exe");

          if (is_iis) {

            assembly_string_default_appdomain_vector = {
                WStr("Datadog.Trace.ClrProfiler.Managed"),
                WStr("AppDomain default IIS"),
            };
            assembly_string_nondefault_appdomain_vector = {
                WStr("Datadog.Trace.ClrProfiler.Managed"),
                WStr("AppDomain non default IIS"),
            };

          } else {

            assembly_string_default_appdomain_vector = {
                WStr("Datadog.Trace.ClrProfiler.Managed"),
                WStr("AppDomain default normal process"),
            };
            assembly_string_nondefault_appdomain_vector = {
                WStr("Datadog.Trace.ClrProfiler.Managed"),
                WStr("AppDomain non default normal process"),
            };

          }

          return new Loader(info, assembly_string_default_appdomain_vector,
                            assembly_string_nondefault_appdomain_vector,
                            log_debug_callback, log_info_callback,
                            log_warn_callback);
        }
    };

    extern Loader* loader;  // global reference to loader

}  // namespace trace

#endif // DD_CLR_PROFILER_LOADER_H_