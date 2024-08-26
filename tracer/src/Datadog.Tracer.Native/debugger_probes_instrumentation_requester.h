#ifndef DD_CLR_PROFILER_DEBUGGER_PROBES_INSTRUMENTATION_H_
#define DD_CLR_PROFILER_DEBUGGER_PROBES_INSTRUMENTATION_H_

#include "corhlpr.h"
#include "../../../shared/src/native-src/string.h"
#include <corprof.h>
#include "debugger_members.h"
#include "fault_tolerant_method_duplicator.h"

// forward declaration

namespace fault_tolerant
{
class FaultTolerantMethodDuplicator;
} // namespace fault_tolerant

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
    std::shared_ptr<fault_tolerant::FaultTolerantMethodDuplicator> m_fault_tolerant_method_duplicator = nullptr;
    bool is_debugger_or_exception_debugging_enabled = false;

    static bool IsCoreLibOr3rdParty(const WSTRING& assemblyName);
    static WSTRING GenerateRandomProbeId();

    void RemoveProbes(debugger::DebuggerRemoveProbesDefinition* removeProbes, int removeProbesLength,
                      std::set<MethodIdentifier>& revertRequests);
    void AddMethodProbes(debugger::DebuggerMethodProbeDefinition* methodProbes, int methodProbesLength,
                         debugger::DebuggerMethodSpanProbeDefinition* spanProbes, int spanProbesLength,
                         std::set<trace::MethodIdentifier>& rejitRequests);
    void AddLineProbes(debugger::DebuggerLineProbeDefinition* lineProbes, int lineProbesLength,
                       std::set<MethodIdentifier>& rejitRequests);
    void DetermineReInstrumentProbes(std::set<MethodIdentifier>& revertRequests,
                                     std::set<MethodIdentifier>& reInstrumentRequests) const;

    bool ProbeIdExists(const WCHAR* probeId);

public:
    DebuggerProbesInstrumentationRequester(CorProfiler* corProfiler, std::shared_ptr<trace::RejitHandler> rejit_handler,
                                           std::shared_ptr<trace::RejitWorkOffloader> work_offloader, std::shared_ptr<fault_tolerant::FaultTolerantMethodDuplicator> fault_tolerant_method_duplicator);

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
    HRESULT STDMETHODCALLTYPE ModuleLoadFinished(const ModuleID moduleId);

    static HRESULT NotifyReJITError(ModuleID moduleId, mdMethodDef methodId, FunctionID functionId, HRESULT hrStatus);
};

} // namespace debugger

#endif // DD_CLR_PROFILER_DEBUGGER_PROBES_INSTRUMENTATION_H_