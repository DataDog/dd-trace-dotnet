#include "debugger_rejit_handler_module_method.h"
#include "debugger_method_rewriter.h"

namespace debugger
{

DebuggerRejitHandlerModuleMethod::DebuggerRejitHandlerModuleMethod(
                                                    mdMethodDef methodDef, 
                                                    RejitHandlerModule* module,
                                                    const FunctionInfo& functionInfo) :
    RejitHandlerModuleMethod(methodDef, module, functionInfo)
{
}

MethodRewriter* DebuggerRejitHandlerModuleMethod::GetMethodRewriter()
{
    return DebuggerMethodRewriter::Instance();
}

void DebuggerRejitHandlerModuleMethod::AddProbe(ProbeDefinition_S probe)
{
    m_probes.push_back(probe);
}

bool DebuggerRejitHandlerModuleMethod::RemoveProbe(const shared::WSTRING& probeId)
{
    for (auto probe = m_probes.begin(); probe != m_probes.end(); ++probe)
    {
        if ((*probe)->probeId == probeId)
        {
            m_probes.erase(probe);
            return true;
        }
    }

    return false;
}

std::vector<ProbeDefinition_S>& DebuggerRejitHandlerModuleMethod::GetProbes()
{
    return m_probes;
}

} // namespace debugger