#include "debugger_rejit_handler_module_method.h"
#include "debugger_method_rewriter.h"

namespace debugger
{

DebuggerRejitHandlerModuleMethod::DebuggerRejitHandlerModuleMethod(
                                                    mdMethodDef methodDef, 
                                                    RejitHandlerModule* module,
                                                    const FunctionInfo& functionInfo,
                                                    const MethodProbeDefinition& methodProbe) :
    RejitHandlerModuleMethod(methodDef, module, functionInfo),
    m_methodProbeDefinition(std::make_unique<debugger::MethodProbeDefinition>(methodProbe))
{
}

MethodProbeDefinition* DebuggerRejitHandlerModuleMethod::GetMethodProbeDefinition()
{
    return m_methodProbeDefinition.get();
}

MethodRewriter* DebuggerRejitHandlerModuleMethod::GetMethodRewriter()
{
    return DebuggerMethodRewriter::Instance();
}

} // namespace debugger