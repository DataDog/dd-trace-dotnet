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
    std::vector<ProbeDefinition_S> m_probes;

public:
    DebuggerRejitHandlerModuleMethod(mdMethodDef methodDef, 
                                     RejitHandlerModule* module,
                                     const FunctionInfo& functionInfo);

    MethodRewriter* GetMethodRewriter() override;
    void AddProbe(ProbeDefinition_S probe);
    bool RemoveProbe(const shared::WSTRING& probeId);
    std::vector<ProbeDefinition_S>& GetProbes();
};

} // namespace debugger

#endif