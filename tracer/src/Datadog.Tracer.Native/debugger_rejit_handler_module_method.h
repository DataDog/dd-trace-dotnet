#ifndef DD_CLR_PROFILER_DEBUGGER_REJIT_HANDLER_MODULE_METHOD_H_
#define DD_CLR_PROFILER_DEBUGGER_REJIT_HANDLER_MODULE_METHOD_H_

#include "rejit_handler.h"
#include "debugger_members.h"

using namespace trace;

namespace debugger
{

/// <summary>
/// Rejit handler representation of a method
/// </summary>
class DebuggerRejitHandlerModuleMethod : public RejitHandlerModuleMethod
{
private:
    // Probes are added from the ReJIT worker while preprocessing, removed from the managed thread that serves
    // RemoveProbes, and read from whichever thread the runtime calls GetReJITParameters on.
    mutable std::mutex m_probes_lock;
    std::vector<ProbeDefinition_S> m_probes;

public:
    DebuggerRejitHandlerModuleMethod(mdMethodDef methodDef, 
                                     RejitHandlerModule* module,
                                     const FunctionInfo& functionInfo,
                                     std::unique_ptr<MethodRewriter> methodRewriter);

    bool AddProbe(ProbeDefinition_S probe);
    bool RemoveProbe(const shared::WSTRING& probeId);
    std::vector<ProbeDefinition_S> GetProbes() const;
};

} // namespace debugger

#endif