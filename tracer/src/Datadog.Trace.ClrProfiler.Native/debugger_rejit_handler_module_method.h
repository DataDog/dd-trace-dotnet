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
    std::unique_ptr<MethodProbeDefinition> m_methodProbeDefinition;

public:
    DebuggerRejitHandlerModuleMethod(mdMethodDef methodDef, 
                                     RejitHandlerModule* module,
                                     const FunctionInfo& functionInfo,
                                     const MethodProbeDefinition& methodProbe);

    MethodProbeDefinition* GetMethodProbeDefinition();
    MethodRewriter* GetMethodRewriter() override;
};

} // namespace debugger

#endif