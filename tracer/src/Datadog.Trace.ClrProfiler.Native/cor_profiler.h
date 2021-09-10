#ifndef DD_CLR_PROFILER_COR_PROFILER_H_
#define DD_CLR_PROFILER_COR_PROFILER_H_

#include "cor.h"
#include "corprof.h"
#include <atomic>
#include <mutex>
#include <string>
#include <unordered_map>
#include <vector>

#include "cor_profiler_base.h"
#include "environment_variables.h"
#include "il_rewriter.h"
#include "integration.h"
#include "module_metadata.h"
#include "pal.h"
#include "rejit_handler.h"

namespace trace
{

class CorProfiler : public CorProfilerBase
{
private:
    std::atomic_bool is_attached_ = {false};
    RuntimeInformation runtime_information_;
    std::vector<IntegrationMethod> integration_methods_;

    // Startup helper variables
    bool first_jit_compilation_completed = false;

    bool instrument_domain_neutral_assemblies = false;
    bool corlib_module_loaded = false;
    AppDomainID corlib_app_domain_id = 0;
    bool managed_profiler_loaded_domain_neutral = false;
    std::unordered_set<AppDomainID> managed_profiler_loaded_app_domains;
    std::unordered_set<AppDomainID> first_jit_compilation_app_domains;
    bool in_azure_app_services = false;
    bool is_desktop_iis = false;
    bool is_net46_or_greater = false;

    //
    // CallTarget Members
    //
    RejitHandler* rejit_handler = nullptr;

    // Cor assembly properties
    AssemblyProperty corAssemblyProperty{};
    AssemblyReference* managed_profiler_assembly_reference;

    //
    // OpCodes helper
    //
    std::vector<std::string> opcodes_names;

    //
    // Module helper variables
    //
    std::mutex module_id_to_info_map_lock_;
    std::unordered_map<ModuleID, ModuleMetadata*> module_id_to_info_map_;

    //
    // Helper methods
    //
    void RewritingPInvokeMaps(ComPtr<IUnknown> metadata_interfaces, ModuleMetadata* module_metadata, WSTRING nativemethods_type_name);
    WSTRING GetCoreCLRProfilerPath();
    bool GetWrapperMethodRef(ModuleMetadata* module_metadata, ModuleID module_id,
                             const MethodReplacement& method_replacement, mdMemberRef& wrapper_method_ref,
                             mdTypeRef& wrapper_type_ref);
    HRESULT ProcessReplacementCalls(ModuleMetadata* module_metadata, const FunctionID function_id,
                                    const ModuleID module_id, const mdToken function_token, const FunctionInfo& caller,
                                    const std::vector<MethodReplacement> method_replacements);
    HRESULT ProcessInsertionCalls(ModuleMetadata* module_metadata, const FunctionID function_id,
                                  const ModuleID module_id, const mdToken function_token, const FunctionInfo& caller,
                                  const std::vector<MethodReplacement> method_replacements);
    bool ProfilerAssemblyIsLoadedIntoAppDomain(AppDomainID app_domain_id);
    std::string GetILCodes(const std::string& title, ILRewriter* rewriter, const FunctionInfo& caller,
                           ModuleMetadata* module_metadata);
    //
    // Startup methods
    //
    HRESULT RunILStartupHook(const ComPtr<IMetaDataEmit2>&, const ModuleID module_id, const mdToken function_token);
    HRESULT GenerateVoidILStartupMethod(const ModuleID module_id, mdMethodDef* ret_method_token);
    HRESULT AddIISPreStartInitFlags(const ModuleID module_id, const mdToken function_token);

    //
    // CallTarget Methods
    //
    size_t CallTarget_RequestRejitForModule(ModuleID module_id, ModuleMetadata* module_metadata,
                                            const std::vector<IntegrationMethod>& integrations);
    HRESULT CallTarget_RewriterCallback(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler);

public:
    CorProfiler() = default;

    bool IsAttached() const;

    void GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int* assemblySize, BYTE** pSymbolsArray,
                                    int* symbolsSize) const;

    //
    // ICorProfilerCallback methods
    //
    HRESULT STDMETHODCALLTYPE Initialize(IUnknown* cor_profiler_info_unknown) override;

    HRESULT STDMETHODCALLTYPE AssemblyLoadFinished(AssemblyID assembly_id, HRESULT hr_status) override;

    HRESULT STDMETHODCALLTYPE ModuleLoadFinished(ModuleID module_id, HRESULT hr_status) override;

    HRESULT STDMETHODCALLTYPE ModuleUnloadStarted(ModuleID module_id) override;

    HRESULT STDMETHODCALLTYPE JITCompilationStarted(FunctionID function_id, BOOL is_safe_to_block) override;

    HRESULT STDMETHODCALLTYPE AppDomainShutdownFinished(AppDomainID appDomainId, HRESULT hrStatus) override;

    HRESULT STDMETHODCALLTYPE Shutdown() override;

    HRESULT STDMETHODCALLTYPE ProfilerDetachSucceeded() override;

    HRESULT STDMETHODCALLTYPE JITInlining(FunctionID callerId, FunctionID calleeId, BOOL* pfShouldInline) override;
    //
    // ReJIT Methods
    //

    HRESULT STDMETHODCALLTYPE ReJITCompilationStarted(FunctionID functionId, ReJITID rejitId,
                                                      BOOL fIsSafeToBlock) override;

    HRESULT STDMETHODCALLTYPE GetReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                                 ICorProfilerFunctionControl* pFunctionControl) override;

    HRESULT STDMETHODCALLTYPE ReJITCompilationFinished(FunctionID functionId, ReJITID rejitId, HRESULT hrStatus,
                                                       BOOL fIsSafeToBlock) override;

    HRESULT STDMETHODCALLTYPE ReJITError(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId,
                                         HRESULT hrStatus) override;

    HRESULT STDMETHODCALLTYPE JITCachedFunctionSearchStarted(FunctionID functionId, BOOL* pbUseCachedFunction) override;

    //
    // ICorProfilerCallback6 methods
    //
    HRESULT STDMETHODCALLTYPE GetAssemblyReferences(const WCHAR* wszAssemblyPath,
                                                    ICorProfilerAssemblyReferenceProvider* pAsmRefProvider) override;

    //
    // Add Integrations methods
    //
    void InitializeProfiler(CallTargetDefinition* items, int size);
};

// Note: Generally you should not have a single, global callback implementation,
// as that prevents your profiler from analyzing multiply loaded in-process
// side-by-side CLRs. However, this profiler implements the "profile-first"
// alternative of dealing with multiple in-process side-by-side CLR instances.
// First CLR to try to load us into this process wins; so there can only be one
// callback implementation created. (See ProfilerCallback::CreateObject.)
extern CorProfiler* profiler; // global reference to callback object

} // namespace trace

#endif // DD_CLR_PROFILER_COR_PROFILER_H_
