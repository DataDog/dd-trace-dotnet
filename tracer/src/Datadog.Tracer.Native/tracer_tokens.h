#ifndef DD_CLR_PROFILER_TRACER_TOKENS_H_
#define DD_CLR_PROFILER_TRACER_TOKENS_H_

#include "calltarget_tokens.h"

#define FASTPATH_COUNT 9

using namespace shared;

namespace trace
{

class TracerTokens : public CallTargetTokens
{
private:
    ICorProfilerInfo4* _profiler_info;
    mdMemberRef beginArrayMemberRef = mdMemberRefNil;
    mdMemberRef beginMethodFastPathRefs[FASTPATH_COUNT];
    mdMemberRef endVoidMemberRef = mdMemberRefNil;
    mdMemberRef endVoidRuntimeAsyncMemberRef = mdMemberRefNil;
    mdMemberRef logExceptionRef = mdMemberRefNil;
    mdTypeRef bubbleUpExceptionTypeRef = mdTypeRefNil;
    mdMemberRef bubbleUpExceptionFunctionRef = mdMemberRefNil;
    mdMemberRef createRefStructMemberRef = mdMemberRefNil;

    HRESULT WriteBeginMethodWithArgumentsArray(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                               const TypeInfo* currentType, ILInstr** instruction);

protected:
    const shared::WSTRING& GetCallTargetType() override;
    const shared::WSTRING& GetCallTargetStateType() override;
    const shared::WSTRING& GetCallTargetReturnType() override;
    const shared::WSTRING& GetCallTargetReturnGenericType() override;
    const shared::WSTRING& GetCallTargetRefStructType() override;

    HRESULT EnsureBaseCalltargetTokens() override;
    void AddAdditionalLocals(TypeSignature* methodReturnValue, std::vector<TypeSignature>* methodTypeArguments,
                             SignatureBuilder& signature, bool isAsyncMethod) override;

public:
    TracerTokens(ModuleMetadata* module_metadata_ptr, bool enableByRefInstrumentation,
                 bool enableCallTargetStateByRef);

    int GetAdditionalLocalsCount(const std::vector<TypeSignature>& methodTypeArguments) override;

    HRESULT WriteBeginMethod(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef, const TypeInfo* currentType,
                             const std::vector<TypeSignature>& methodArguments,
                             bool ignoreByRefInstrumentation, ILInstr** instruction);

    // isRuntimeAsync selects CallTargetInvoker.EndMethodRuntimeAsync instead of EndMethod, which
    // dispatches to the handler that calls OnAsyncMethodEnd directly rather than via a continuation.
    // It takes one extra generic parameter, TDeclaredReturn: the Task/ValueTask the method declares,
    // as opposed to the unwrapped type its body actually returns. The managed handler binds
    // OnMethodEnd against that, so an integration sees the same shape whether or not its target
    // happens to be runtime-async. declaredReturnArgument is required when isRuntimeAsync is true.
    HRESULT WriteEndVoidReturnMemberRef(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                        const TypeInfo* currentType, ILInstr** instruction,
                                        bool isRuntimeAsync = false, TypeSignature* declaredReturnArgument = nullptr);

    HRESULT WriteEndReturnMemberRef(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef, const TypeInfo* currentType,
                                    TypeSignature* returnArgument, ILInstr** instruction,
                                    bool isRuntimeAsync = false, TypeSignature* declaredReturnArgument = nullptr);

    HRESULT WriteLogException(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef, const TypeInfo* currentType,
                              ILInstr** instruction);

    mdTypeRef GetBubbleUpExceptionTypeRef() const;

    mdMemberRef GetBubbleUpExceptionFunctionDef() const;

    const shared::WSTRING& GetTraceAttributeType();

    void SetCorProfilerInfo(ICorProfilerInfo4* profilerInfo);

    HRESULT WriteRefStructCall(void* rewriterWrapperPtr, mdTypeRef refStructTypeRef, int refStructIndex);
};

} // namespace trace

#endif // DD_CLR_PROFILER_TRACER_TOKENS_H_
