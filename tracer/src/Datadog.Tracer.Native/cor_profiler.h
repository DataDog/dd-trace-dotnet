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
#include "tracer_rejit_preprocessor.h"
#include "debugger_rejit_preprocessor.h"
#include "rejit_handler.h"
#include <unordered_set>
#include "clr_helpers.h"
#include "debugger_probes_instrumentation_requester.h"
#include "Synchronized.hpp"
#include "fault_tolerant_method_duplicator.h"
#include "fault_tolerant_rewriter.h"

#include "../../../shared/src/native-src/pal.h"

// forward declaration
namespace debugger
{
class DebuggerMethodRewriter;
class DebuggerProbesInstrumentationRequester;
}

namespace iast
{
class Dataflow;
}

namespace fault_tolerant
{
class FaultTolerantMethodDuplicator;
class FaultTolerantRewriter;
}

namespace trace
{
class CorProfiler : public CorProfilerBase
{
private:
    std::atomic_bool is_attached_ = {false};
    RuntimeInformation runtime_information_;
    std::vector<IntegrationDefinition> integration_definitions_; // All APM Calltargets
    std::deque<std::pair<ModuleID, std::vector<MethodReference>>> rejit_module_method_pairs;

    Synchronized<std::unordered_set<shared::WSTRING>> definitions_ids;

    // Startup helper variables
    bool first_jit_compilation_completed = false;

    bool corlib_module_loaded = false;
    AppDomainID corlib_app_domain_id = 0;
    bool managed_profiler_loaded_domain_neutral = false;
    std::unordered_map<AppDomainID, Version> managed_profiler_loaded_app_domains;
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
    bool call_target_bubble_up_exception_available = false;
    bool call_target_bubble_up_exception_function_available = false;

    //
    // Debugger Members
    //
    std::unique_ptr<debugger::DebuggerProbesInstrumentationRequester> debugger_instrumentation_requester = nullptr;

    //
    // Fault-Tolerant Instrumentation Members
    //
    std::shared_ptr<fault_tolerant::FaultTolerantMethodDuplicator> fault_tolerant_method_duplicator = nullptr;

    // Cor assembly properties
    AssemblyProperty corAssemblyProperty{};
    AssemblyReference* managed_profiler_assembly_reference = nullptr;

    //
    // OpCodes helper
    //
    std::vector<std::string> opcodes_names;

    //
    // Module helper variables
    //
    Synchronized<std::vector<ModuleID>> module_ids;

    ModuleID managedProfilerModuleId_;

    //
    // Dataflow members
    //
    iast::Dataflow* _dataflow = nullptr;

    //
    // Helper methods
    //
    static void RewritingPInvokeMaps(const ModuleMetadata& module_metadata, const shared::WSTRING& rewrite_reason,
                                     const shared::WSTRING& nativemethods_type_name,
                                     const shared::WSTRING& library_path = shared::WSTRING());
    static void __stdcall NativeLog(int32_t level, const WCHAR* message, int32_t length);
    bool GetIntegrationTypeRef(ModuleMetadata& module_metadata, ModuleID module_id,
                               const IntegrationDefinition& integration_definition, mdTypeRef& integration_type_ref);
    bool ProfilerAssemblyIsLoadedIntoAppDomain(AppDomainID app_domain_id);
    std::string GetILCodes(const std::string& title, ILRewriter* rewriter, const FunctionInfo& caller,
                           const ComPtr<IMetaDataImport2>& metadata_import);
    HRESULT RewriteForDistributedTracing(const ModuleMetadata& module_metadata, ModuleID module_id);
    HRESULT RewriteForTelemetry(const ModuleMetadata& module_metadata, ModuleID module_id);
    HRESULT RewriteIsManualInstrumentationOnly(const ModuleMetadata& module_metadata, ModuleID module_id);
    HRESULT EmitDistributedTracerTargetMethod(const ModuleMetadata& module_metadata, ModuleID module_id);
    HRESULT TryRejitModule(ModuleID module_id, std::vector<ModuleID>& modules);
    static bool TypeNameMatchesTraceAttribute(WCHAR type_name[], DWORD type_name_len);
    static bool EnsureCallTargetBubbleUpExceptionTypeAvailable(const ModuleMetadata& module_metadata, mdTypeDef* mdTypeDefToken);
    static bool EnsureIsCallTargetBubbleUpExceptionFunctionAvailable(const ModuleMetadata& module_metadata, mdTypeDef typeDef);
    
    //
    // Startup methods
    //
    HRESULT RunILStartupHook(const ComPtr<IMetaDataEmit2>&, ModuleID module_id, mdToken function_token, const FunctionInfo& caller, const ModuleMetadata& module_metadata);
    HRESULT GenerateVoidILStartupMethod(ModuleID module_id, mdMethodDef* ret_method_token);
    HRESULT AddIISPreStartInitFlags(ModuleID module_id, mdToken function_token);

    //
    // Initialization methods
    //
    void InternalAddInstrumentation(WCHAR* id, CallTargetDefinition* items, int size, bool isDerived, bool isInterface, bool enable = true);

public:
    CorProfiler() = default;

    bool IsAttached() const;

    void GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int* assemblySize, BYTE** pSymbolsArray,
                                    int* symbolsSize) ;

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
    HRESULT STDMETHODCALLTYPE GetReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                                 ICorProfilerFunctionControl* pFunctionControl) override;

    HRESULT STDMETHODCALLTYPE ReJITError(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId,
                                         HRESULT hrStatus) override;

    HRESULT STDMETHODCALLTYPE JITCachedFunctionSearchStarted(FunctionID functionId, BOOL* pbUseCachedFunction) override;

    //
    // ICorProfilerCallback6 methods
    //
    HRESULT STDMETHODCALLTYPE GetAssemblyReferences(const WCHAR* wszAssemblyPath,
                                                    ICorProfilerAssemblyReferenceProvider* pAsmRefProvider) override;

    //
    // Legacy Add Integrations methods
    //
    void InitializeProfiler(WCHAR* id, CallTargetDefinition* items, int size);
    void RemoveCallTargetDefinitions(WCHAR* id, CallTargetDefinition* items, int size);
    void EnableByRefInstrumentation();
    void EnableCallTargetStateByRef();
    void AddDerivedInstrumentations(WCHAR* id, CallTargetDefinition* items, int size);
    void AddInterfaceInstrumentations(WCHAR* id, CallTargetDefinition* items, int size);
    void AddTraceAttributeInstrumentation(WCHAR* id, WCHAR* integration_assembly_name_ptr,
                                          WCHAR* integration_type_name_ptr);

    //
    // Tracer Integration methods #2
    //
    long RegisterCallTargetDefinitions(WCHAR* id, CallTargetDefinition2* items, int size,
                                       UINT32 enabledCategories = -1);
    long EnableCallTargetDefinitions(UINT32 enabledCategories);
    long DisableCallTargetDefinitions(UINT32 disabledCategories);

    //
    // Register Aspects into Dataflow
    //
    int RegisterIastAspects(WCHAR** aspects, int aspectsLength);


    //
    // Live Debugger Integration methods
    //
    void InitializeTraceMethods(WCHAR* id, WCHAR* integration_assembly_name_ptr, WCHAR* integration_type_name_ptr,
                                WCHAR* configuration_string_ptr);
    void InstrumentProbes(debugger::DebuggerMethodProbeDefinition* methodProbes, int methodProbesLength,
                   debugger::DebuggerLineProbeDefinition* lineProbes, int lineProbesLength,
                   debugger::DebuggerMethodSpanProbeDefinition* spanProbes, int spanProbesLength,
                   debugger::DebuggerRemoveProbesDefinition* revertProbes, int revertProbesLength) const;
    int GetProbesStatuses(WCHAR** probeIds, int probeIdsLength, debugger::DebuggerProbeStatus* probeStatuses);

    //
    // Fault-Tolerant Instrumentation methods
    //
    void ReportSuccessfulInstrumentation(ModuleID moduleId, int methodToken, const WCHAR* instrumentationId, int products);
    bool ShouldHeal(ModuleID moduleId, int methodToken, const WCHAR* instrumentationId, int products);

    //
    // Disable profiler
    //
    void DisableTracerCLRProfiler();

    //
    // Propagate settings from RCM
    //
    void UpdateSettings(WCHAR* keys[], WCHAR* values[], int length);

    friend class debugger::DebuggerProbesInstrumentationRequester;
    friend class debugger::DebuggerMethodRewriter;
    friend class TracerMethodRewriter;
    friend class MethodRewriter;
    friend class fault_tolerant::FaultTolerantMethodDuplicator;
    friend class fault_tolerant::FaultTolerantRewriter;

    //
    // Getters for exception filter
    //
    bool IsCallTargetBubbleUpExceptionTypeAvailable() const;
    bool IsCallTargetBubbleUpFunctionAvailable() const;
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