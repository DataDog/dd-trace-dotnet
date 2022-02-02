#include "debugger_rejit_preprocessor.h"
#include "debugger_rejit_handler_module_method.h"

namespace debugger
{

// DebuggerRejitPreprocessor

const MethodReference& DebuggerRejitPreprocessor::GetTargetMethod(const debugger::MethodProbeDefinition& methodProbe)
{
    return methodProbe.target_method;
}

const bool DebuggerRejitPreprocessor::GetIsDerived(const debugger::MethodProbeDefinition& methodProbe)
{
    return false; // TODO
}

const std::unique_ptr<RejitHandlerModuleMethod> DebuggerRejitPreprocessor::CreateMethod(
                                        const mdMethodDef methodDef, 
                                        RejitHandlerModule* module,
                                        const FunctionInfo& functionInfo,
                                        const MethodProbeDefinition& methodProbeDefinition)
{
    return std::make_unique<DebuggerRejitHandlerModuleMethod>(methodDef, module, functionInfo, methodProbeDefinition);
}

} // namespace debugger