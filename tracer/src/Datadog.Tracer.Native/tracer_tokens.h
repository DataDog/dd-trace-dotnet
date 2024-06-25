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
                             COR_SIGNATURE (&signatureBuffer)[BUFFER_SIZE], ULONG& signatureOffset,
                             ULONG& signatureSize, bool isAsyncMethod) override;

public:
    TracerTokens(ModuleMetadata* module_metadata_ptr, bool enableByRefInstrumentation,
                 bool enableCallTargetStateByRef);

    int GetAdditionalLocalsCount(const std::vector<TypeSignature>& methodTypeArguments) override;

    HRESULT WriteBeginMethod(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef, const TypeInfo* currentType,
                             const std::vector<TypeSignature>& methodArguments,
                             bool ignoreByRefInstrumentation, ILInstr** instruction);

    HRESULT WriteEndVoidReturnMemberRef(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                        const TypeInfo* currentType, ILInstr** instruction);

    HRESULT WriteEndReturnMemberRef(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef, const TypeInfo* currentType,
                                    TypeSignature* returnArgument, ILInstr** instruction);

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
