#ifndef DD_CLR_PROFILER_LOADER_H_
#define DD_CLR_PROFILER_LOADER_H_

#include <corhlpr.h>
#include <corprof.h>
#include <functional>
#include <mutex>
#include <unordered_set>
#include <utility>

#include "com_ptr.h"
#include "il_rewriter.h"
#include "il_rewriter_wrapper.h"
#include "string.h"
#include "pal.h"

namespace shared {

    struct RuntimeInfo {
        COR_PRF_RUNTIME_TYPE runtime_type;
        USHORT major_version;
        USHORT minor_version;
        USHORT build_version;
        USHORT qfe_version;

        RuntimeInfo()
            : runtime_type((COR_PRF_RUNTIME_TYPE)0x0),
            major_version(0),
            minor_version(0),
            build_version(0),
            qfe_version(0) {}

        RuntimeInfo(COR_PRF_RUNTIME_TYPE runtime_type, USHORT major_version,
            USHORT minor_version, USHORT build_version, USHORT qfe_version)
            : runtime_type(runtime_type),
            major_version(major_version),
            minor_version(minor_version),
            build_version(build_version),
            qfe_version(qfe_version) {}

        RuntimeInfo& operator=(const RuntimeInfo& other) {
            runtime_type = other.runtime_type;
            major_version = other.major_version;
            minor_version = other.minor_version;
            build_version = other.build_version;
            qfe_version = other.qfe_version;
            return *this;
        }

        bool is_desktop() const { return runtime_type == COR_PRF_DESKTOP_CLR; }
        bool is_core() const { return runtime_type == COR_PRF_CORE_CLR; }
    };

    struct LoaderResourceMonikerIDs
    {
        public:
            LoaderResourceMonikerIDs(void)
                : Net45_Datadog_AutoInstrumentation_ManagedLoader_dll(0),
                  NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll(0),
                  Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb(0),
                  NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb(0)
            {}

            LoaderResourceMonikerIDs(const LoaderResourceMonikerIDs& ids)
                : Net45_Datadog_AutoInstrumentation_ManagedLoader_dll(ids.Net45_Datadog_AutoInstrumentation_ManagedLoader_dll),
                  NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll(ids.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll),
                     Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb(ids.Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb),
                  NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb(ids.NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb)
            {}

            std::int32_t Net45_Datadog_AutoInstrumentation_ManagedLoader_dll;
            std::int32_t NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_dll;
            std::int32_t Net45_Datadog_AutoInstrumentation_ManagedLoader_pdb;
            std::int32_t NetCoreApp20_Datadog_AutoInstrumentation_ManagedLoader_pdb;
    };

    struct AssemblyMetadata {
        AppDomainID app_domain_id;
        ModuleID module_id;
        AssemblyID id;
        mdAssembly token = mdAssemblyNil;
        ASSEMBLYMETADATA metadata{};
        WSTRING name;
        DWORD flags;
        const void* public_key;
        ULONG public_key_length;
        ULONG hash_alg_id;
    };

    class Loader {
    private:
        RuntimeInfo runtime_information_;
        ICorProfilerInfo4* info_;

        std::mutex loaders_loaded_mutex_;
        std::unordered_set<AppDomainID> loaders_loaded_;
        AssemblyMetadata corlib_metadata{};

        std::vector<WSTRING> assembly_string_default_appdomain_vector_;
        std::vector<WSTRING> assembly_string_nondefault_appdomain_vector_;

        std::function<void(const std::string& str)> log_debug_callback_ = nullptr;
        std::function<void(const std::string& str)> log_info_callback_ = nullptr;
        std::function<void(const std::string& str)> log_warn_callback_ = nullptr;

        LoaderResourceMonikerIDs resourceMonikerIDs_;

        WCHAR const* native_profiler_library_filename_;

        static Loader* s_singeltonInstance;

        static Loader* CreateNewLoaderInstance(
                    ICorProfilerInfo4* pCorProfilerInfo,
                    std::function<void(const std::string& str)> log_debug_callback,
                    std::function<void(const std::string& str)> log_info_callback,
                    std::function<void(const std::string& str)> log_warn_callback,
                    const LoaderResourceMonikerIDs& resourceMonikerIDs,
                    WCHAR const* native_profiler_library_filename,
                    const std::vector<WSTRING>& assembliesToLoad_adDefault_procNonIIS,
                    const std::vector<WSTRING>& assembliesToLoad_adNonDefault_procNonIIS,
                    const std::vector<WSTRING>& assembliesToLoad_adDefault_procIIS,
                    const std::vector<WSTRING>& assembliesToLoad_adNonDefault_procIIS);

        Loader(
                    ICorProfilerInfo4* info,
                    const std::vector<WSTRING>& assembly_string_default_appdomain_vector,
                    const std::vector<WSTRING>& assembly_string_nondefault_appdomain_vector,
                    std::function<void(const std::string& str)> log_debug_callback,
                    std::function<void(const std::string& str)> log_info_callback,
                    std::function<void(const std::string& str)> log_warn_callback,
                    const LoaderResourceMonikerIDs& resourceMonikerIDs,
                    WCHAR const* native_profiler_library_filename);

        inline void Debug(const std::string& str) {
            if (log_debug_callback_ != nullptr) {
                log_debug_callback_(str);
            }
        }

        inline void Info(const std::string& str) {
            if (log_info_callback_ != nullptr) {
                log_info_callback_(str);
            }
        }

        inline void Warn(const std::string& str) {
            if (log_warn_callback_ != nullptr) {
                log_warn_callback_(str);
            }
        }

        HRESULT WriteAssembliesStringArray(
                ILRewriterWrapper& rewriter_wrapper,
                const ComPtr<IMetaDataEmit2> metadata_emit,
                const std::vector<WSTRING>& assembly_string_vector,
                mdTypeRef string_type_ref);

        HRESULT GetGetAssemblyAndSymbolsBytesMethodDef(const ComPtr<IMetaDataEmit2> metadata_emit, mdTypeDef module_type_def, mdMemberRef securitycriticalattribute_ctor_member_ref, mdMethodDef* result_method_def);

        inline RuntimeInfo GetRuntimeInformation() {
            COR_PRF_RUNTIME_TYPE runtime_type;
            USHORT major_version;
            USHORT minor_version;
            USHORT build_version;
            USHORT qfe_version;

            HRESULT hr = info_->GetRuntimeInformation(nullptr, &runtime_type, &major_version, &minor_version, &build_version, &qfe_version, 0, nullptr, nullptr);
            if (FAILED(hr)) {
                return {};
            }

            return { runtime_type, major_version, minor_version, build_version, qfe_version };
        }

    public:

        static void CreateNewSingeltonInstance(
                    ICorProfilerInfo4* pCorProfilerInfo,
                    std::function<void(const std::string& str)> log_debug_callback,
                    std::function<void(const std::string& str)> log_info_callback,
                    std::function<void(const std::string& str)> log_warn_callback,
                    const LoaderResourceMonikerIDs& resource_moniker_ids,
                    WCHAR const * native_profiler_library_filename,
                    const std::vector<WSTRING>& assembliesToLoad_adDefault_procNonIIS,
                    const std::vector<WSTRING>& assembliesToLoad_adNonDefault_procNonIIS,
                    const std::vector<WSTRING>& assembliesToLoad_adDefault_procIIS,
                    const std::vector<WSTRING>& assembliesToLoad_adNonDefault_procIIS);

        static Loader* GetSingeltonInstance();
        static void DeleteSingeltonInstance(void);

        HRESULT InjectLoaderToModuleInitializer(const ModuleID module_id);

        bool GetAssemblyAndSymbolsBytes(void** pAssemblyArray, int* assemblySize, void** pSymbolsArray, int* symbolsSize, WCHAR* moduleName);
    };

}  // namespace shared

#endif // DD_CLR_PROFILER_LOADER_H_