#ifndef DD_CLR_PROFILER_DEBUGGER_PROBES_INSTRUMENTATION_H_
#define DD_CLR_PROFILER_DEBUGGER_PROBES_INSTRUMENTATION_H_

#include "corhlpr.h"
#include "../../../shared/src/native-src/string.h"
#include <corprof.h>
#include "debugger_members.h"

namespace debugger
{

class DebuggerProbesInstrumentationRequester
{
private:
    CorProfiler* m_corProfiler;
    std::recursive_mutex m_probes_mutex;
    std::vector<ProbeDefinition_S> m_probes;
    std::unique_ptr<DebuggerRejitPreprocessor> m_debugger_rejit_preprocessor = nullptr;
    std::shared_ptr<RejitHandler> m_rejit_handler = nullptr;
    std::shared_ptr<RejitWorkOffloader> m_work_offloader = nullptr;
    bool is_debugger_enabled = false;
    bool is_fault_tolerant_instrumentation_enabled = false;
    bool flag = true;

    static bool IsCoreLibOr3rdParty(const WSTRING& assemblyName);
    static std::wstring GenerateRandomProbeId();

    void RemoveProbes(debugger::DebuggerRemoveProbesDefinition* removeProbes, int removeProbesLength,
                      std::set<MethodIdentifier>& revertRequests);
    void AddMethodProbes(debugger::DebuggerMethodProbeDefinition* methodProbes, int methodProbesLength,
                         debugger::DebuggerMethodSpanProbeDefinition* spanProbes, int spanProbesLength,
                         std::set<trace::MethodIdentifier>& rejitRequests);
    void AddLineProbes(debugger::DebuggerLineProbeDefinition* lineProbes, int lineProbesLength,
                       std::set<MethodIdentifier>& rejitRequests);
    void DetermineReInstrumentProbes(std::set<MethodIdentifier>& revertRequests, std::set<MethodIdentifier>& reInstrumentRequests) const;

    bool ProbeIdExists(const WCHAR* probeId);

    void HandleFaultTolerantInstrumentationIfEnabled(ModuleID moduleId, const ModuleInfo& moduleInfo,
                                            ComPtr<IMetaDataImport2> metadataImport,
                                                     ComPtr<IMetaDataEmit2> metadataEmit, mdTypeDef typeDef) const;

public:
    DebuggerProbesInstrumentationRequester(CorProfiler* corProfiler, std::shared_ptr<trace::RejitHandler> rejit_handler,
                                           std::shared_ptr<trace::RejitWorkOffloader> work_offloader);

    void InstrumentProbes(debugger::DebuggerMethodProbeDefinition* methodProbes, int methodProbesLength,
                   debugger::DebuggerLineProbeDefinition* lineProbes, int lineProbesLength,
                   debugger::DebuggerMethodSpanProbeDefinition* spanProbes, int spanProbesLength,
                   debugger::DebuggerRemoveProbesDefinition* removeProbes, int removeProbesLength);
    static int GetProbesStatuses(WCHAR** probeIds, int probeIdsLength, debugger::DebuggerProbeStatus* probeStatuses);
    void PerformInstrumentAllIfNeeded(const ModuleID& module_id, const mdToken& function_token);
    const std::vector<std::shared_ptr<ProbeDefinition>>& GetProbes() const;
    DebuggerRejitPreprocessor* GetPreprocessor();
    void RequestRejitForLoadedModule(ModuleID moduleId);

    void ModuleLoadFinished_AddMetadataToModule(ModuleID moduleId);
    HRESULT STDMETHODCALLTYPE ModuleLoadFinished(ModuleID moduleId);

    static HRESULT NotifyReJITError(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId, HRESULT hrStatus);
};

} // namespace debugger

#endif // DD_CLR_PROFILER_DEBUGGER_PROBES_INSTRUMENTATION_H_