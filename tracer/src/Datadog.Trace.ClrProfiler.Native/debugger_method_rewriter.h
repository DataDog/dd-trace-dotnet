#ifndef DD_CLR_PROFILER_DEBUGGER_METHOD_REWRITER_H_
#define DD_CLR_PROFILER_DEBUGGER_METHOD_REWRITER_H_

#include "cor_profiler.h"
#include "il_rewriter_wrapper.h"
#include "method_rewriter.h"
#include "module_metadata.h"
#include "../../../shared/src/native-src/util.h"

using namespace trace;

namespace debugger
{

class DebuggerMethodRewriter : public MethodRewriter, public shared::Singleton<DebuggerMethodRewriter>
{
    friend class shared::Singleton<DebuggerMethodRewriter>;

private:
    DebuggerMethodRewriter(){}

    static HRESULT GetFunctionLocalSignature(const ModuleMetadata& module_metadata, ILRewriter& rewriter, FunctionLocalSignature& localSignature);
    static HRESULT LoadArgument(CorProfiler* corProfiler, bool isStatic, const ILRewriterWrapper& rewriterWrapper, int argumentIndex,
                                const TypeSignature& argument);
    static HRESULT LoadLocal(CorProfiler* corProfiler, const ILRewriterWrapper& rewriterWrapper, int localIndex,
                             const TypeSignature& argument);
    static void LoadDebuggerState(bool enableDebuggerStateByRef, const ILRewriterWrapper& rewriterWrapper,
                                  ULONG callTargetStateIndex);
    static HRESULT WriteCallsToLogArgOrLocal(CorProfiler* corProfiler, DebuggerTokens* debuggerTokens, bool isStatic,
                                        const std::vector<TypeSignature>& methodArgsOrLocals, int numArgsOrLocals,
                                        ILRewriterWrapper& rewriterWrapper, ULONG callTargetStateIndex,
                                        ILInstr** beginCallInstruction, bool isArgs);
    static HRESULT WriteCallsToLogArg(CorProfiler* corProfiler, DebuggerTokens* debuggerTokens, bool isStatic,
                                const std::vector<TypeSignature>& args, int numArgs, ILRewriterWrapper& rewriterWrapper,
                                ULONG callTargetStateIndex,
                                ILInstr** beginCallInstruction);
    static HRESULT WriteCallsToLogLocal(CorProfiler* corProfiler, DebuggerTokens* debuggerTokens, bool isStatic,
                                  const std::vector<TypeSignature>& locals, int numLocals,
                                  ILRewriterWrapper& rewriterWrapper, ULONG callTargetStateIndex,
                                  ILInstr** beginCallInstruction);
    static HRESULT LoadInstanceIntoStack(FunctionInfo* caller, bool isStatic, const ILRewriterWrapper& rewriterWrapper, ILInstr** outLoadArgumentInstr);

public:
    HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) override;
};

} // namespace debugger

#endif