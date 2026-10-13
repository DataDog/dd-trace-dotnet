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

    // Pass nullptr for declaredRuntimeAsyncReturn for an ordinary method. A non-null value means
    // two things at once, which is why it is one parameter and not a bool plus a pointer: the
    // target is a .NET 11 runtime-async method, and this is the Task/ValueTask it *declares*, as
    // opposed to the unwrapped type its body actually leaves on the stack (which is what
    // returnArgument carries below).
    //
    // That selects CallTargetInvoker.EndMethodRuntimeAsync instead of EndMethod, which dispatches
    // to the handler that calls OnAsyncMethodEnd directly rather than via a continuation. It takes
    // one extra generic parameter, TDeclaredReturn, bound to the declared type, so that an
    // integration's OnMethodEnd sees the same shape whether or not its target is runtime-async.
    HRESULT WriteEndVoidReturnMemberRef(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                        const TypeInfo* currentType, ILInstr** instruction,
                                        const TypeSignature* declaredRuntimeAsyncReturn);

    HRESULT WriteEndReturnMemberRef(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef, const TypeInfo* currentType,
                                    TypeSignature* returnArgument, ILInstr** instruction,
                                    const TypeSignature* declaredRuntimeAsyncReturn);

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
