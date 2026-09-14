#include "debugger_rejit_handler_module_method.h"
#include "debugger_method_rewriter.h"

namespace debugger
{

DebuggerRejitHandlerModuleMethod::DebuggerRejitHandlerModuleMethod(
                                                    mdMethodDef methodDef, 
                                                    RejitHandlerModule* module,
                                                    const FunctionInfo& functionInfo,
                                                    std::unique_ptr<MethodRewriter> methodRewriter) :
    RejitHandlerModuleMethod(methodDef, module, functionInfo, std::move(methodRewriter))
{
}

bool DebuggerRejitHandlerModuleMethod::AddProbe(ProbeDefinition_S probe)
{
    std::lock_guard lock(m_probes_lock);

    for (const auto& currentProbe : m_probes)
    {
        if (currentProbe->probeId == probe->probeId)
        {
            return false;
        }
    }

    m_probes.push_back(probe);
    return true;
}

bool DebuggerRejitHandlerModuleMethod::RemoveProbe(const shared::WSTRING& probeId)
{
    std::lock_guard lock(m_probes_lock);

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

std::vector<ProbeDefinition_S> DebuggerRejitHandlerModuleMethod::GetProbes() const
{
    std::lock_guard lock(m_probes_lock);
    return m_probes;
}

} // namespace debugger