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

    HRESULT WriteBeginMethodWithArgumentsArray(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                               const TypeInfo* currentType, ILInstr** instruction);

protected:
    const shared::WSTRING& GetCallTargetType() override;
    const shared::WSTRING& GetCallTargetStateType() override;
    const shared::WSTRING& GetCallTargetReturnType() override;
    const shared::WSTRING& GetCallTargetReturnGenericType() override;

public:
    TracerTokens(ModuleMetadata* module_metadata_ptr, const bool enableByRefInstrumentation,
                 const bool enableCallTargetStateByRef);

    HRESULT WriteBeginMethod(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef, const TypeInfo* currentType,
                             const std::vector<TypeSignature>& methodArguments,
                             const bool ignoreByRefInstrumentation, ILInstr** instruction);

    HRESULT WriteEndVoidReturnMemberRef(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                        const TypeInfo* currentType, ILInstr** instruction);

    HRESULT WriteEndReturnMemberRef(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef, const TypeInfo* currentType,
                                    TypeSignature* returnArgument, ILInstr** instruction);

    HRESULT WriteLogException(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef, const TypeInfo* currentType,
                              ILInstr** instruction);
};

} // namespace trace

#endif // DD_CLR_PROFILER_TRACER_TOKENS_H_