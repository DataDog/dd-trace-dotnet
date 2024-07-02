#ifndef DD_CLR_PROFILER_DEBUGGER_METHOD_REWRITER_H_
#define DD_CLR_PROFILER_DEBUGGER_METHOD_REWRITER_H_

#include "../../../shared/src/native-src/util.h"
#include "cor_profiler.h"
#include "il_rewriter_wrapper.h"
#include "instrumenting_product.h"
#include "method_rewriter.h"
#include "module_metadata.h"

using namespace trace;

namespace debugger
{

class DebuggerMethodRewriter : public MethodRewriter
{
public:
    DebuggerMethodRewriter(CorProfiler* corProfiler) : MethodRewriter(corProfiler)
    {
    }

private:
    static HRESULT GetFunctionLocalSignature(const ModuleMetadata& module_metadata, ILRewriter& rewriter,
                                             FunctionLocalSignature& localSignature);
    HRESULT LoadArgument(bool isStatic, const ILRewriterWrapper& rewriterWrapper,
                                int argumentIndex, const TypeSignature& argument) const;
    HRESULT LoadLocal(const ILRewriterWrapper& rewriterWrapper, int localIndex,
                             const TypeSignature& argument) const;
    HRESULT WriteCallsToLogArgOrLocal(ModuleMetadata& moduleMetadata,
                                             DebuggerTokens* debuggerTokens, bool isStatic,
                                             const std::vector<TypeSignature>& methodArgsOrLocals, int numArgsOrLocals,
                                             ILRewriterWrapper& rewriterWrapper, ULONG callTargetStateIndex,
                                      ILInstr** beginCallInstruction, bool isArgs,
                                      ProbeType probeType,
                                      mdFieldDef isReEntryFieldTok = mdFieldDefNil) const;
    HRESULT WriteCallsToLogArg(ModuleMetadata& moduleMetadata,
                                      DebuggerTokens* debuggerTokens, bool isStatic,
                                      const std::vector<TypeSignature>& args, int numArgs,
                                      ILRewriterWrapper& rewriterWrapper, ULONG callTargetStateIndex, 
        ILInstr** beginCallInstruction, ProbeType probeType, mdFieldDef isReEntryFieldTok = mdFieldDefNil) const;
    HRESULT WriteCallsToLogLocal(ModuleMetadata& moduleMetadata,
                                        DebuggerTokens* debuggerTokens, bool isStatic,
                                        const std::vector<TypeSignature>& locals, int numLocals,
                                        ILRewriterWrapper& rewriterWrapper, ULONG callTargetStateIndex,
                                 ILInstr** beginCallInstruction, ProbeType probeType,
                                 mdFieldDef isReEntryFieldTok = mdFieldDefNil) const;
    static HRESULT LoadInstanceIntoStack(FunctionInfo* caller, bool isStatic, const ILRewriterWrapper& rewriterWrapper,
                                         ILInstr** outLoadArgumentInstr, CallTargetTokens* callTargetTokens);
    HRESULT CallLineProbe(int instrumentedMethodIndex, ModuleID module_id,
                                 ModuleMetadata& module_metadata, FunctionInfo* caller, DebuggerTokens* debuggerTokens,
                                 mdToken function_token, bool isStatic, std::vector<TypeSignature>& methodArguments,
                                 int numArgs, ILRewriter& rewriter, std::vector<TypeSignature>& methodLocals,
                                 int numLocals, ILRewriterWrapper& rewriterWrapper, ULONG lineProbeCallTargetStateIndex,
                                 std::vector<EHClause>& lineProbesEHClauses, const std::vector<ILInstr*>& branchTargets,
                                 const std::shared_ptr<LineProbeDefinition>& lineProbe, bool isAsyncMethod) const;

    HRESULT ApplyLineProbes(int instrumentedMethodIndex, LineProbeDefinitions& lineProbes,
                            ModuleID module_id, ModuleMetadata& module_metadata, FunctionInfo* caller,
                            DebuggerTokens* debuggerTokens, mdToken function_token, bool isStatic,
                            std::vector<TypeSignature>& methodArguments, int numArgs, ILRewriter& rewriter,
                            std::vector<TypeSignature>& methodLocals, int numLocals, ILRewriterWrapper& rewriterWrapper,
                            ULONG lineProbeCallTargetStateIndex, std::vector<EHClause>& lineProbesEHClauses,
                            bool isAsyncMethod) const;

    HRESULT ApplyMethodProbe(const MethodProbeDefinitions& methodProbes, ModuleID module_id,
                             ModuleMetadata& module_metadata,
                             FunctionInfo* caller, DebuggerTokens* debuggerTokens, mdToken function_token,
                             TypeSignature retFuncArg, bool isVoid, bool isStatic,
                             const std::vector<TypeSignature>& methodArguments, int numArgs, ILRewriter& rewriter,
                             const std::vector<TypeSignature>& methodLocals, int numLocals,
                             ILRewriterWrapper& rewriterWrapper, ULONG callTargetStateIndex, ULONG exceptionIndex,
                             ULONG callTargetReturnIndex, ULONG returnValueIndex, ULONG multiProbeStatesIndex, mdToken callTargetReturnToken,
                             ILInstr* firstInstruction, int instrumentedMethodIndex, ILInstr* const& beforeLineProbe,
                             std::vector<EHClause>& newClauses) const;

    HRESULT ApplyMethodSpanProbe(ModuleID module_id, ModuleMetadata& module_metadata, FunctionInfo* caller,
                            DebuggerTokens* debuggerTokens, mdToken function_token, TypeSignature retFuncArg,
                            bool isVoid, bool isStatic, const std::vector<TypeSignature>& methodArguments, int numArgs, 
                            const std::shared_ptr<SpanProbeOnMethodDefinition>& spanProbe, ILRewriter& rewriter,
                            const std::vector<TypeSignature>& methodLocals, int numLocals,
                                 ILRewriterWrapper& rewriterWrapper, ULONG spanMethodStateIndex, ULONG exceptionIndex,
                            ULONG callTargetReturnIndex, ULONG returnValueIndex, mdToken callTargetReturnToken,
                            int instrumentedMethodIndex, ILInstr*& beforeLineProbe, std::vector<EHClause>& newClauses) const;

    HRESULT EndAsyncMethodProbe(ILRewriterWrapper& rewriterWrapper,
                                       ModuleMetadata& module_metadata, DebuggerTokens* debuggerTokens,
                                       FunctionInfo* caller, bool isStatic, TypeSignature* methodReturnType,
                                       const std::vector<TypeSignature>& methodLocals, int numLocals, 
                                       ULONG callTargetReturnIndex, mdFieldDef isReEntryFieldTok,
                                       std::vector<EHClause>& newClauses, const ProbeType& probeType) const;
    HRESULT EndAsyncMethodSpanProbe(ILRewriterWrapper& rewriterWrapper, ModuleMetadata& module_metadata,
                                DebuggerTokens* debuggerTokens, FunctionInfo* caller, bool isStatic,
                                TypeSignature* methodReturnType, const std::vector<TypeSignature>& methodLocals,
                                int numLocals, ULONG callTargetReturnIndex,
                                mdFieldDef isReEntryFieldTok,
                                std::vector<EHClause>& newClauses) const;
    static HRESULT LoadProbeIdIntoStack(ModuleID moduleId, const ModuleMetadata& moduleMetadata, mdToken functionToken,
                                        const shared::WSTRING& methodProbeId, const ILRewriterWrapper& rewriterWrapper,
                                        ILInstr** outLoadStrInstr);
    static void LogDebugCallerInfo(const FunctionInfo* caller, int instrumentedMethodIndex) ;
    HRESULT ApplyAsyncMethodProbe(MethodProbeDefinitions& methodProbes, ModuleID module_id,
                                  ModuleMetadata& module_metadata,
                                  FunctionInfo* caller, DebuggerTokens* debugger_tokens, mdToken function_token,
                                  bool isStatic, TypeSignature* methodReturnType,
                                  const std::vector<TypeSignature>& methodLocals, int numLocals,
                                  ILRewriterWrapper& rewriterWrapper,
                                  ULONG callTargetReturnIndex, ULONG returnValueIndex, mdToken callTargetReturnToken,
                                  ILInstr* firstInstruction, int instrumentedMethodIndex,
                                  ILInstr* const& beforeLineProbe, std::vector<EHClause>& newClauses) const;
    HRESULT ApplyAsyncMethodSpanProbe(const std::shared_ptr<SpanProbeOnMethodDefinition>& spanProbe,
                                  ModuleID moduleId, ModuleMetadata& moduleMetadata, FunctionInfo* caller,
                                  DebuggerTokens* debuggerTokens, mdToken functionToken, bool isStatic,
                                  TypeSignature* methodReturnType, const std::vector<TypeSignature>& methodLocals,
                                  int numLocals, ILRewriterWrapper& rewriterWrapper, ULONG callTargetReturnIndex,
                                  ULONG returnValueIndex, mdToken callTargetReturnToken, ILInstr* firstInstruction,
                                  int instrumentedMethodIndex, ILInstr* const& beforeLineProbe,
                                  std::vector<EHClause>& newClauses) const;
    static bool DoesILContainUnsupportedInstructions(ILRewriter& rewriter);
    HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler,
                    ICorProfilerFunctionControl* pFunctionControl, ICorProfilerInfo* pCorProfilerInfo,
                    MethodProbeDefinitions& methodProbes, LineProbeDefinitions& lineProbes,
                    SpanProbeOnMethodDefinitions& spanProbesOnMethod) const;
    static std::vector<ILInstr*> GetBranchTargets(ILRewriter* pRewriter);
    static void AdjustBranchTargets(ILInstr* pFromInstr, ILInstr* pToInstr, const std::vector<ILInstr*>& branchTargets);
    static void AdjustExceptionHandlingClauses(ILInstr* pFromInstr, ILInstr* pToInstr, ILRewriter* pRewriter);

public:
    HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler,
                    ICorProfilerFunctionControl* pFunctionControl, ICorProfilerInfo* pCorProfilerInfo) override;
    InstrumentingProducts GetInstrumentingProduct(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) override;
    WSTRING GetInstrumentationId(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) override;
    static HRESULT IsTypeImplementIAsyncStateMachine(const ComPtr<IMetaDataImport2>& metadataImport,
                                                     ULONG32 typeToken, bool& isTypeImplementIAsyncStateMachine);
    HRESULT IsAsyncMethodProbe(const ComPtr<IMetaDataImport2>& metadataImport, const FunctionInfo* caller,
                               bool& isAsyncMethod) const;
    static HRESULT GetTaskReturnType(const ILInstr* instruction, ModuleMetadata& moduleMetadata,
                                     const std::vector<TypeSignature>& methodLocals, TypeSignature* returnType);
    static void MarkAllProbesAsInstrumented(MethodProbeDefinitions& methodProbes, LineProbeDefinitions& lineProbes,
                                     SpanProbeOnMethodDefinitions& spanOnMethodProbes);
    static void MarkAllProbesAsError(MethodProbeDefinitions& methodProbes, LineProbeDefinitions& lineProbes,
                                     SpanProbeOnMethodDefinitions& spanOnMethodProbes,
                                     const WSTRING& reasoning);
    static void MarkAllLineProbesAsError(LineProbeDefinitions& lineProbes, const WSTRING& reasoning);
    static void MarkAllMethodProbesAsError(MethodProbeDefinitions& methodProbes, const WSTRING& reasoning);
    static void MarkAllSpanOnMethodProbesAsError(SpanProbeOnMethodDefinitions& spanProbes, const WSTRING& reasoning);
};

} // namespace debugger

#endif