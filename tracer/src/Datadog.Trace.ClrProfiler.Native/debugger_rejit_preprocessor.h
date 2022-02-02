#ifndef DD_CLR_PROFILER_DEBUGGER_REJIT_PREPROCESSOR_H_
#define DD_CLR_PROFILER_DEBUGGER_REJIT_PREPROCESSOR_H_

#include "rejit_preprocessor.h"
#include "debugger_members.h"

using namespace trace;

namespace debugger
{
/// <summary>
/// DebuggerRejitPreprocessor
/// </summary>
class DebuggerRejitPreprocessor : public RejitPreprocessor<MethodProbeDefinition>
{
public:  
    using RejitPreprocessor::RejitPreprocessor;

protected:
    virtual const MethodReference& GetTargetMethod(const MethodProbeDefinition& methodProbe) final;
    virtual const bool GetIsDerived(const MethodProbeDefinition& definition) final;
    virtual const std::unique_ptr<RejitHandlerModuleMethod> CreateMethod(const mdMethodDef methodDef, RejitHandlerModule* module, const FunctionInfo& functionInfo,
                 const MethodProbeDefinition& methodProbeDefinition) final;
};

} // namespace debugger

#endif // DD_CLR_PROFILER_DEBUGGER_REJIT_PREPROCESSOR_H_