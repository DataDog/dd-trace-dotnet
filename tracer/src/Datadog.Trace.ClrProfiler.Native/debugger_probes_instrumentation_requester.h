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
    std::vector<MethodProbeDefinition> method_probes_;
    std::unique_ptr<DebuggerRejitPreprocessor> debugger_rejit_preprocessor = nullptr;

    static bool ShouldPerformInstrumentAll(const WSTRING& assemblyName);

public:
    DebuggerProbesInstrumentationRequester(std::shared_ptr<trace::RejitHandler> rejit_handler,
                                           std::shared_ptr<trace::RejitWorkOffloader> work_offloader);

    void InstrumentProbes(WCHAR* id, DebuggerMethodProbeDefinition* items, int size);
    void PerformInstrumentAllIfNeeded(const ModuleInfo& module_info, const ModuleID& module_id, const mdToken& function_token);
    const std::vector<MethodProbeDefinition>& GetProbes() const;
    DebuggerRejitPreprocessor* GetPreprocessor();
};

} // namespace debugger

#endif // DD_CLR_PROFILER_DEBUGGER_PROBES_INSTRUMENTATION_H_