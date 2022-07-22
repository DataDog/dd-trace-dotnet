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
#include "rejit_preprocessor.h"
#include "debugger_rejit_preprocessor.h"
#include "rejit_handler.h"
#include <unordered_set>
#include "clr_helpers.h"
#include "debugger_probes_instrumentation_requester.h"

#include "../../../shared/src/native-src/pal.h"

// forward declaration
namespace debugger
{
class DebuggerMethodRewriter;
}

namespace trace
{

class CorProfiler : public CorProfilerBase
{
private:
    std::atomic_bool is_attached_ = {false};
    RuntimeInformation runtime_information_;
    std::vector<IntegrationDefinition> integration_definitions_;
    std::deque<std::pair<ModuleID, std::vector<MethodReference>>> rejit_module_method_pairs;

    std::unordered_set<shared::WSTRING> definitions_ids_;
    std::mutex definitions_ids_lock_;

    // Startup helper variables
    bool first_jit_compilation_completed = false;

    bool corlib_module_loaded = false;
    AppDomainID corlib_app_domain_id = 0;
    bool managed_profiler_loaded_domain_neutral = false;
    std::unordered_set<AppDomainID> managed_profiler_loaded_app_domains;
    std::unordered_set<AppDomainID> first_jit_compilation_app_domains;
    bool is_desktop_iis = false;

    //
    // CallTarget Members
    //
    std::shared_ptr<RejitHandler> rejit_handler = nullptr;
    bool enable_by_ref_instrumentation = false;
    bool enable_calltarget_state_by_ref = false;
    std::unique_ptr<TypeReference> trace_annotation_integration_type = nullptr;
    std::unique_ptr<TracerRejitPreprocessor> tracer_integration_preprocessor = nullptr;
    bool trace_annotations_enabled = false;

    //
    // Debugger Members
    //
    std::unique_ptr<debugger::DebuggerProbesInstrumentationRequester> debugger_instrumentation_requester = nullptr;

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
    std::mutex module_ids_lock_;
    std::vector<ModuleID> module_ids_;

    //
    // Helper methods
    //
    void RewritingPInvokeMaps(const ModuleMetadata& module_metadata, const shared::WSTRING& nativemethods_type_name, const shared::WSTRING& library_path = shared::WSTRING());
    bool GetIntegrationTypeRef(ModuleMetadata& module_metadata, ModuleID module_id,
                               const IntegrationDefinition& integration_definition, mdTypeRef& integration_type_ref);
    bool ProfilerAssemblyIsLoadedIntoAppDomain(AppDomainID app_domain_id);
    std::string GetILCodes(const std::string& title, ILRewriter* rewriter, const FunctionInfo& caller,
                           const ComPtr<IMetaDataImport2>& metadata_import);
    HRESULT RewriteForDistributedTracing(const ModuleMetadata& module_metadata, ModuleID module_id);
    HRESULT EmitDistributedTracerTargetMethod(const ModuleMetadata& module_metadata, ModuleID module_id);
    HRESULT TryRejitModule(ModuleID module_id);
    bool TypeNameMatchesTraceAttribute(WCHAR type_name[], DWORD type_name_len);
    //
    // Startup methods
    //
    HRESULT RunILStartupHook(const ComPtr<IMetaDataEmit2>&, const ModuleID module_id, const mdToken function_token, const FunctionInfo& caller, const ModuleMetadata& module_metadata);
    HRESULT GenerateVoidILStartupMethod(const ModuleID module_id, mdMethodDef* ret_method_token);
    HRESULT AddIISPreStartInitFlags(const ModuleID module_id, const mdToken function_token);

    //
    // Initialization methods
    //
    void InternalAddInstrumentation(WCHAR* id, CallTargetDefinition* items, int size, bool isDerived);

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
    void InitializeProfiler(WCHAR* id, CallTargetDefinition* items, int size);
    void EnableByRefInstrumentation();
    void EnableCallTargetStateByRef();
    void AddDerivedInstrumentations(WCHAR* id, CallTargetDefinition* items, int size);
    void AddTraceAttributeInstrumentation(WCHAR* id, WCHAR* integration_assembly_name_ptr,
                                          WCHAR* integration_type_name_ptr);
    void InitializeTraceMethods(WCHAR* id, WCHAR* integration_assembly_name_ptr, WCHAR* integration_type_name_ptr,
                                WCHAR* configuration_string_ptr);
    void InstrumentProbes(debugger::DebuggerMethodProbeDefinition* methodProbes, int methodProbesLength,
                   debugger::DebuggerLineProbeDefinition* lineProbes, int lineProbesLength,
                   debugger::DebuggerRemoveProbesDefinition* revertProbes, int revertProbesLength) const;
    int GetProbesStatuses(WCHAR** probeIds, int probeIdsLength, debugger::DebuggerProbeStatus* probeStatuses);

    friend class debugger::DebuggerProbesInstrumentationRequester;
    friend class debugger::DebuggerMethodRewriter;
    friend class TracerMethodRewriter;
    friend class MethodRewriter;
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
