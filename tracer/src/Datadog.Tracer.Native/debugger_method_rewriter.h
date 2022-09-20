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

    // Holds incremental index that is used on the managed side for grabbing an InstrumentedMethodInfo instance (per instrumented method)
    inline static std::atomic<int> _nextInstrumentedMethodIndex{0};

    static int GetNextInstrumentedMethodIndex();

    static HRESULT GetFunctionLocalSignature(const ModuleMetadata& module_metadata, ILRewriter& rewriter, FunctionLocalSignature& localSignature);
    static HRESULT LoadArgument(CorProfiler* corProfiler, bool isStatic, const ILRewriterWrapper& rewriterWrapper, int argumentIndex,
                                const TypeSignature& argument);
    static HRESULT LoadLocal(CorProfiler* corProfiler, const ILRewriterWrapper& rewriterWrapper, int localIndex,
                             const TypeSignature& argument);
    static HRESULT WriteCallsToLogArgOrLocal(CorProfiler* corProfiler, DebuggerTokens* debuggerTokens, bool isStatic,
                                        const std::vector<TypeSignature>& methodArgsOrLocals, int numArgsOrLocals,
                                        ILRewriterWrapper& rewriterWrapper, ULONG callTargetStateIndex,
                                             ILInstr** beginCallInstruction, bool isArgs, ProbeType probeType);
    static HRESULT WriteCallsToLogArg(CorProfiler* corProfiler, DebuggerTokens* debuggerTokens, bool isStatic,
                                const std::vector<TypeSignature>& args, int numArgs, ILRewriterWrapper& rewriterWrapper,
                                ULONG callTargetStateIndex,
                                ILInstr** beginCallInstruction,
                                ProbeType probeType);
    static HRESULT WriteCallsToLogLocal(CorProfiler* corProfiler, DebuggerTokens* debuggerTokens, bool isStatic,
                                  const std::vector<TypeSignature>& locals, int numLocals,
                                  ILRewriterWrapper& rewriterWrapper, ULONG callTargetStateIndex,
                                        ILInstr** beginCallInstruction, ProbeType probeType);
    static HRESULT LoadInstanceIntoStack(FunctionInfo* caller, bool isStatic, const ILRewriterWrapper& rewriterWrapper,
                                         ILInstr** outLoadArgumentInstr, CallTargetTokens* callTargetTokens);
    static HRESULT CallLineProbe(int instrumentedMethodIndex, CorProfiler* corProfiler, ModuleID module_id,
                                 ModuleMetadata& module_metadata, FunctionInfo* caller, DebuggerTokens* debuggerTokens,
                                 mdToken function_token, bool isStatic, std::vector<TypeSignature>& methodArguments,
                                 int numArgs, ILRewriter& rewriter, std::vector<TypeSignature>& methodLocals,
                                 int numLocals, ILRewriterWrapper& rewriterWrapper, ULONG lineProbeCallTargetStateIndex,
                                 std::vector<EHClause>& lineProbesEHClauses, const std::vector<ILInstr*>& branchTargets,
                                 const std::shared_ptr<LineProbeDefinition>& lineProbe);
    HRESULT ApplyLineProbes(int instrumentedMethodIndex, const LineProbeDefinitions& lineProbes,
                           CorProfiler* corProfiler, ModuleID module_id, ModuleMetadata& module_metadata,
                           FunctionInfo* caller, DebuggerTokens* debuggerTokens, mdToken function_token, bool isStatic,
                           std::vector<TypeSignature>& methodArguments, int numArgs, ILRewriter& rewriter,
                           std::vector<TypeSignature>& methodLocals, int numLocals, ILRewriterWrapper& rewriterWrapper,
                           ULONG lineProbeCallTargetStateIndex, std::vector<EHClause>& lineProbesEHClauses) const;
    HRESULT ApplyMethodProbe(CorProfiler* corProfiler, ModuleID module_id, ModuleMetadata& module_metadata,
                          FunctionInfo* caller, DebuggerTokens* debuggerTokens, mdToken function_token,
                          TypeSignature retFuncArg, bool isVoid, bool isStatic,
                          std::vector<TypeSignature> methodArguments,
                          int numArgs, const shared::WSTRING& methodProbeId, ILRewriter& rewriter,
                          std::vector<TypeSignature>& methodLocals, int numLocals, ILRewriterWrapper& rewriterWrapper,
                          ULONG callTargetStateIndex, ULONG exceptionIndex, ULONG callTargetReturnIndex,
                          ULONG returnValueIndex, mdToken callTargetReturnToken, ILInstr* firstInstruction,
                             int instrumentedMethodIndex, ILInstr* const& beforeLineProbe, std::vector<EHClause>& newClauses) const;
    static HRESULT EndAsyncMethodProbe(CorProfiler* corProfiler, ILRewriterWrapper& rewriterWrapper,
                                       ModuleMetadata& moduleMetadata,
                                       DebuggerTokens* debuggerTokens, FunctionInfo* caller, bool isStatic, TypeSignature* methodReturnType,
                                       const std::vector<TypeSignature>& methodLocals, int numLocals, ULONG callTargetStateIndex,
                                       ULONG callTargetReturnIndex, std::vector<EHClause>& newClauses,
                                       ILInstr** setResultEndMethodTryStartInstr, ILInstr** endMethodOriginalCodeFirstInstr);
    static HRESULT LoadProbeIdIntoStack(ModuleID moduleId, const ModuleMetadata& moduleMetadata, mdToken functionToken,
                                        const shared::WSTRING& methodProbeId, const ILRewriterWrapper& rewriterWrapper);
    void LogDebugCallerInfo(const FunctionInfo* caller, int instrumentedMethodIndex) const;
    HRESULT ApplyAsyncMethodProbe(CorProfiler* corProfiler, ModuleID moduleId, ModuleMetadata& moduleMetadata,
                                  FunctionInfo* caller, DebuggerTokens* debuggerTokens, mdToken functionToken,
                                  bool isStatic, TypeSignature* methodReturnType,
                                  const shared::WSTRING& methodProbeId,
                                  const std::vector<TypeSignature>& methodLocals, int numLocals, ILRewriterWrapper& rewriterWrapper,
                                  ULONG asyncMethodStateIndex, ULONG callTargetReturnIndex,
                                  ULONG returnValueIndex, mdToken callTargetReturnToken, ILInstr* firstInstruction,
                                  const int instrumentedMethodIndex, ILInstr* const& beforeLineProbe, std::vector<EHClause>& newClauses) const;

    HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler,
                                                        MethodProbeDefinitions& methodProbes,
                                                        LineProbeDefinitions& lineProbes) const;
    static std::vector<ILInstr*> GetBranchTargets(ILRewriter* pRewriter);
    static void AdjustBranchTargets(ILInstr* pFromInstr, ILInstr* pToInstr, const std::vector<ILInstr*>& branchTargets);
    static void AdjustExceptionHandlingClauses(ILInstr* pFromInstr, ILInstr* pToInstr, ILRewriter* pRewriter);

public:
    HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) override;
    static HRESULT IsTypeImplementIAsyncStateMachine(const ComPtr<IMetaDataImport2>& metadataImport, const ULONG32 typeToken, bool& isTypeImplementIAsyncStateMachine);
    HRESULT IsAsyncMethodProbe(const ComPtr<IMetaDataImport2>& metadataImport, const FunctionInfo* caller, bool& isAsyncMethod) const;
    static HRESULT GetTaskReturnType(const ILInstr* instruction, ModuleMetadata& moduleMetadata, const std::vector<TypeSignature>& methodLocals, TypeSignature* returnType);
};

} // namespace debugger

#endif