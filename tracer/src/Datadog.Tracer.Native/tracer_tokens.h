#ifndef DD_CLR_PROFILER_TRACER_TOKENS_H_
#define DD_CLR_PROFILER_TRACER_TOKENS_H_

#include "calltarget_tokens.h"

#define FASTPATH_COUNT 9

namespace trace
{

class TracerTokens : public CallTargetTokens
{
private:
    mdMemberRef beginArrayMemberRef = mdMemberRefNil;
    mdMemberRef beginMethodFastPathRefs[FASTPATH_COUNT];
    mdMemberRef endVoidMemberRef = mdMemberRefNil;
    mdMemberRef logExceptionRef = mdMemberRefNil;
    mdTypeRef bubbleUpExceptionTypeRef = mdTypeRefNil;

    HRESULT WriteBeginMethodWithArgumentsArray(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                               const TypeInfo* currentType, ILInstr** instruction);

protected:
    const shared::WSTRING& GetCallTargetType() override;
    const shared::WSTRING& GetCallTargetStateType() override;
    const shared::WSTRING& GetCallTargetReturnType() override;
    const shared::WSTRING& GetCallTargetReturnGenericType() override;
    HRESULT EnsureBaseCalltargetTokens() override;
    void AddAdditionalLocals(COR_SIGNATURE (&signatureBuffer)[500], ULONG& signatureOffset, ULONG& signatureSize, bool isAsyncMethod) override;

public:
    TracerTokens(ModuleMetadata* module_metadata_ptr, bool enableByRefInstrumentation,
                 bool enableCallTargetStateByRef);

    int GetAdditionalLocalsCount() override;

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

    const shared::WSTRING& GetTraceAttributeType();
};

} // namespace trace

#endif // DD_CLR_PROFILER_TRACER_TOKENS_H_